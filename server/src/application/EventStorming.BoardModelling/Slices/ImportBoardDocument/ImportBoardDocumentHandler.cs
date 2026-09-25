using EventStorming.BoardModelling.Drafting;
using EventStorming.BoardModelling.Model;
using EventStorming.BoardModelling.Shared;
using EventStorming.SharedKernel;
using FluentValidation;
using FluentValidation.Results;

namespace EventStorming.BoardModelling.Slices.ImportBoardDocument;

public sealed class ImportBoardDocumentCommandHandler(
    IValidator<BoardDocument> validator,
    IBoardAccess access,
    IElementTypeRegistry registry,
    IImportBoardDocumentStore store,
    IBoardChangeBroadcaster broadcaster,
    IClock clock) : IImportBoardDocumentCommandHandler
{
    public async Task<ImportBoardDocumentResult> Handle(ImportBoardDocumentCommand command, CancellationToken cancellationToken)
    {
        if (command.TeamId is null == command.BoardId is null)
        {
            throw new ArgumentException("Give exactly one of TeamId (to create a board) or BoardId (to replace one).", nameof(command));
        }

        var document = command.Document;
        var validation = await validator.ValidateAsync(document, cancellationToken);
        var failures = validation.ToFailures().ToList();

        Board? existingBoard = null;
        if (command.TeamId is { } teamId)
        {
            if (BoardRules.RequireTeamEdit(await access.ForTeam(teamId, command.Actor, cancellationToken)) is { } refused)
            {
                return ImportBoardDocumentResult.Failed(refused);
            }

            if (string.IsNullOrWhiteSpace(document.Board?.Name))
            {
                failures.Add(Failures.Invalid("board.name", "required", "A new board needs a name.", "Add \"board\": { \"name\": \"…\", \"level\": \"big-picture\" }."));
            }

            if (document.Board?.Level is null)
            {
                failures.Add(Failures.Invalid("board.level", "required", "A new board needs a level.",
                    "Set board.level to \"big-picture\", \"process-modelling\" or \"software-design\"."));
            }
        }
        else
        {
            if (BoardRules.RequireEdit(await access.ForBoard(command.BoardId!.Value, command.Actor, cancellationToken)) is { } refused)
            {
                return ImportBoardDocumentResult.Failed(refused);
            }

            existingBoard = await store.FindBoard(command.BoardId.Value, cancellationToken);
            if (existingBoard is null)
            {
                return ImportBoardDocumentResult.Failed(Failures.NotFound("The board"));
            }

            if (document.Board?.Level is { } level && BoardLevels.IsValid(level) && BoardLevels.Parse(level) != existingBoard.Level)
            {
                failures.Add(Failures.Invalid("board.level", "level-immutable", "A board's level cannot be changed.",
                    $"Remove board.level, or set it to \"{BoardLevels.Name(existingBoard.Level)}\" (this board's level)."));
            }
        }

        if (failures.Count > 0)
        {
            return ImportBoardDocumentResult.Failed(failures);
        }

        var now = clock.UtcNow;
        var boardId = existingBoard?.Id ?? Guid.CreateVersion7();
        var elements = document.Elements ?? [];
        var connections = document.Connections ?? [];

        var plan = DraftPlanner.Plan(
            elements.Select((element, index) => new ElementDraft(
                $"elements[{index}]", null, element.Key, element.Type, element.Text, element.Position, element.Size,
                element.Swimlane, element.Boundary, element.Anchor, element.Pivotal ?? false, element.Color)).ToList(),
            connections.Select((connection, index) => new ConnectionDraft(
                $"connections[{index}]", null, connection.From, connection.To, connection.Label)).ToList(),
            registry,
            new DraftContext(boardId, command.Actor.Ref, now, new Position(0, 0), new Dictionary<Guid, Element>()));

        if (plan.Content is null)
        {
            return ImportBoardDocumentResult.Failed(plan.Failures);
        }

        var content = plan.Content;
        if (existingBoard is null)
        {
            var board = new Board(
                boardId,
                command.TeamId!.Value,
                document.Board!.Name!.Trim(),
                BoardLevels.Parse(document.Board.Level!),
                Revision: 1,
                content.Elements.Count,
                now,
                command.Actor.Ref,
                now,
                command.Actor.Ref,
                ArchivedAt: null);

            await store.InsertNew(board, content.Elements, content.Connections, cancellationToken);
            return ImportBoardDocumentResult.Succeeded(new ImportedBoard(board, content.KeyedIds, content.Elements.Count, content.Connections.Count));
        }

        var newName = string.IsNullOrWhiteSpace(document.Board?.Name) ? null : document.Board.Name.Trim();
        var replaced = await store.ReplaceContents(boardId, newName, content.Elements, content.Connections, command.Actor.Ref, now, cancellationToken);
        if (replaced is null)
        {
            return ImportBoardDocumentResult.Failed(Failures.NotFound("The board"));
        }

        broadcaster.ContentReplaced(replaced.Id, replaced.Revision, command.Actor.Ref);
        if (newName is not null && newName != existingBoard.Name)
        {
            broadcaster.DetailsChanged(replaced);
        }

        return ImportBoardDocumentResult.Succeeded(new ImportedBoard(replaced, content.KeyedIds, content.Elements.Count, content.Connections.Count));
    }
}

/// <summary>Validates a Board Document with paths relative to the document itself, since the document is the request body.</summary>
public sealed class BoardDocumentValidator : AbstractValidator<BoardDocument>
{
    public BoardDocumentValidator()
    {
        RuleFor(document => document.Version)
            .Must(version => version is null or 1).WithErrorCode("unsupported-version")
            .WithMessage("This API understands Board Document version 1 only.").WithFix("Send \"version\": 1, or leave it out.");

        RuleFor(document => document.Board!.Name)
            .MaximumLength(BoardLimits.MaxBoardNameLength).When(document => document.Board is not null)
            .OverridePropertyName("board.name")
            .WithErrorCode("too-long").WithMessage($"A board name must be at most {BoardLimits.MaxBoardNameLength} characters.");

        RuleFor(document => document.Board!.Level)
            .Must(level => level is null || BoardLevels.IsValid(level)).When(document => document.Board is not null)
            .OverridePropertyName("board.level")
            .WithErrorCode("unknown-level").WithMessage("The level must be big-picture, process-modelling or software-design.")
            .WithFix("Use \"big-picture\" to explore a whole domain, \"process-modelling\" for one process, or \"software-design\" to design aggregates.");

        RuleFor(document => document.Elements)
            .Must(elements => elements is null || elements.Count <= BoardLimits.MaxElements)
            .WithErrorCode("too-many").WithMessage($"A board holds at most {BoardLimits.MaxElements} elements.");

        RuleFor(document => document.Connections)
            .Must(connections => connections is null || connections.Count <= BoardLimits.MaxConnections)
            .WithErrorCode("too-many").WithMessage($"A board holds at most {BoardLimits.MaxConnections} connections.");

        RuleFor(document => document.Elements).Custom((elements, context) =>
        {
            var seen = new Dictionary<string, int>(StringComparer.Ordinal);
            for (var index = 0; elements is not null && index < elements.Count; index++)
            {
                if (elements[index]?.Key is not { Length: > 0 } key)
                {
                    continue;
                }

                if (seen.TryGetValue(key, out var first))
                {
                    context.AddFailure(new ValidationFailure($"Elements[{index}].Key", $"The key '{key}' is already used by elements[{first}].")
                    {
                        ErrorCode = "duplicate-key",
                        CustomState = "Give every element a different key.",
                    });
                }
                else
                {
                    seen[key] = index;
                }
            }
        });

        RuleForEach(document => document.Elements).SetValidator(new DocumentElementValidator());
        RuleForEach(document => document.Connections).SetValidator(new DocumentConnectionValidator());
    }
}

public sealed class DocumentElementValidator : AbstractValidator<DocumentElement>
{
    public DocumentElementValidator()
    {
        RuleFor(element => element.Type)
            .Must(type => !string.IsNullOrWhiteSpace(type)).WithErrorCode("required").WithMessage("Every element needs a type.")
            .WithFix("Send a type such as \"domain-event\". See GET /api/v1/element-types for all of them.");

        RuleFor(element => element.Text)
            .MaximumLength(BoardLimits.MaxTextLength).WithErrorCode("too-long")
            .WithMessage($"Text must be at most {BoardLimits.MaxTextLength} characters.");

        RuleFor(element => element.Key)
            .MaximumLength(BoardLimits.MaxKeyLength).WithErrorCode("too-long")
            .WithMessage($"A key must be at most {BoardLimits.MaxKeyLength} characters.");

        RuleFor(element => element.Position!).SetValidator(new PositionValidator()).When(element => element.Position is not null);
        RuleFor(element => element.Size!).SetValidator(new SizeValidator()).When(element => element.Size is not null);

        RuleFor(element => element.Color)
            .Must(BoardRules.IsColor).WithErrorCode("invalid-color").WithMessage("A color must look like #RRGGBB.")
            .WithFix("Send a hex color such as \"#FFA94D\", or leave it out to use the type's color.");
    }
}

public sealed class DocumentConnectionValidator : AbstractValidator<DocumentConnection>
{
    public DocumentConnectionValidator()
    {
        RuleFor(connection => connection.From)
            .Must(from => !string.IsNullOrWhiteSpace(from)).WithErrorCode("required").WithMessage("A connection needs a 'from' key.")
            .WithFix("Send the key of an element in 'elements'.");

        RuleFor(connection => connection.To)
            .Must(to => !string.IsNullOrWhiteSpace(to)).WithErrorCode("required").WithMessage("A connection needs a 'to' key.")
            .WithFix("Send the key of an element in 'elements'.");

        RuleFor(connection => connection.Label)
            .MaximumLength(BoardLimits.MaxLabelLength).WithErrorCode("too-long")
            .WithMessage($"A label must be at most {BoardLimits.MaxLabelLength} characters.");
    }
}
