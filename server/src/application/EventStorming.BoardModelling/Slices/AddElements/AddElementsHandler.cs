using EventStorming.BoardModelling.Drafting;
using EventStorming.BoardModelling.Model;
using EventStorming.BoardModelling.Shared;
using EventStorming.SharedKernel;
using FluentValidation;

namespace EventStorming.BoardModelling.Slices.AddElements;

public sealed class AddElementsCommandHandler(
    IValidator<AddElementsCommand> validator,
    IBoardAccess access,
    IElementTypeRegistry registry,
    IAddElementsStore store,
    IBoardChangeBroadcaster broadcaster,
    IClock clock) : IAddElementsCommandHandler
{
    /// <summary>Auto-placed elements continue the timeline this far to the right of what is already there.</summary>
    private const double ContinuationGap = 160;

    public async Task<AddElementsResult> Handle(AddElementsCommand command, CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
        {
            return AddElementsResult.Failed(validation.ToFailures());
        }

        if (BoardRules.RequireEdit(await access.ForBoard(command.BoardId, command.Actor, cancellationToken)) is { } refused)
        {
            return AddElementsResult.Failed(refused);
        }

        var connections = command.Connections ?? [];
        var keys = command.Elements.Where(element => element.Key is not null).Select(element => element.Key!).ToHashSet(StringComparer.Ordinal);
        var referencedIds = command.Elements
            .SelectMany(element => new[] { element.Swimlane, element.Boundary, element.Anchor })
            .Concat(connections.SelectMany(connection => new[] { connection.From, connection.To }))
            .Where(reference => reference is not null && !keys.Contains(reference))
            .Select(reference => Guid.TryParse(reference, out var id) ? id : (Guid?)null)
            .OfType<Guid>()
            .Distinct()
            .ToList();

        var existing = referencedIds.Count == 0
            ? new Dictionary<Guid, Element>()
            : (await store.FindElements(command.BoardId, referencedIds, cancellationToken)).ToDictionary(element => element.Id);

        var stats = await store.Stats(command.BoardId, cancellationToken);
        if (stats.ElementCount + command.Elements.Count > BoardLimits.MaxElements)
        {
            return AddElementsResult.Failed(new Failure(FailureKind.LimitExceeded, "board-limit-exceeded",
                $"A board holds at most {BoardLimits.MaxElements} elements; this one has {stats.ElementCount}.", "elements",
                "Delete elements that are no longer needed, or continue on a new board."));
        }

        if (stats.ConnectionCount + connections.Count > BoardLimits.MaxConnections)
        {
            return AddElementsResult.Failed(new Failure(FailureKind.LimitExceeded, "board-limit-exceeded",
                $"A board holds at most {BoardLimits.MaxConnections} connections; this one has {stats.ConnectionCount}.", "connections",
                "Delete connections that are no longer needed."));
        }

        var now = clock.UtcNow;
        var origin = stats.Right is { } right
            ? new Position(right + ContinuationGap, stats.Top ?? 0)
            : new Position(0, 0);

        var plan = DraftPlanner.Plan(
            command.Elements.Select((element, index) => new ElementDraft(
                $"elements[{index}]", element.Id, element.Key, element.Type, element.Text, element.Position, element.Size,
                element.Swimlane, element.Boundary, element.Anchor, element.Pivotal, element.Color)).ToList(),
            connections.Select((connection, index) => new ConnectionDraft(
                $"connections[{index}]", connection.Id, connection.From, connection.To, connection.Label)).ToList(),
            registry,
            new DraftContext(command.BoardId, command.Actor.Ref, now, origin, existing));

        if (plan.Content is null)
        {
            return AddElementsResult.Failed(plan.Failures);
        }

        var inserted = await store.Insert(command.BoardId, plan.Content.Elements, plan.Content.Connections, command.Actor.Ref, now, cancellationToken);
        var changes = new BoardChangeSet(command.BoardId, inserted.Revision, command.OperationId, command.Actor.Ref, inserted.Elements, [], inserted.Connections, []);

        broadcaster.ContentChanged(changes);
        return AddElementsResult.Succeeded(new AddedElements(changes, plan.Content.KeyedIds));
    }
}

public sealed class AddElementsCommandValidator : AbstractValidator<AddElementsCommand>
{
    public AddElementsCommandValidator()
    {
        RuleFor(command => command.Elements)
            .NotNull().WithErrorCode("required").WithMessage("Send the elements to add.")
            .Must(elements => elements is null || elements.Count <= BoardLimits.MaxElementsPerRequest)
            .WithErrorCode("too-many").WithMessage($"At most {BoardLimits.MaxElementsPerRequest} elements can be added in one request.")
            .WithFix($"Split the elements into batches of {BoardLimits.MaxElementsPerRequest}.");

        RuleFor(command => command)
            .Must(command => (command.Elements?.Count ?? 0) + (command.Connections?.Count ?? 0) > 0)
            .WithName("elements").OverridePropertyName("elements")
            .WithErrorCode("required").WithMessage("Send at least one element or connection.");

        RuleFor(command => command.Elements)
            .Must(HaveUniqueKeys).When(command => command.Elements is not null)
            .WithErrorCode("duplicate-key").WithMessage("Two elements in this request have the same key.")
            .WithFix("Give every element a different key.");

        RuleFor(command => command.Elements)
            .Must(elements => elements.Where(element => element.Id is not null).Select(element => element.Id).Distinct().Count()
                              == elements.Count(element => element.Id is not null))
            .When(command => command.Elements is not null)
            .WithErrorCode("duplicate-id").WithMessage("Two elements in this request have the same id.");

        RuleForEach(command => command.Elements).SetValidator(new NewElementValidator());
        RuleForEach(command => command.Connections).SetValidator(new NewConnectionValidator());

        RuleFor(command => command.OperationId)
            .MaximumLength(BoardLimits.MaxOperationIdLength).WithErrorCode("too-long")
            .WithMessage($"An operation id must be at most {BoardLimits.MaxOperationIdLength} characters.");
    }

    private static bool HaveUniqueKeys(IReadOnlyList<NewElement> elements)
    {
        var keys = elements.Where(element => element.Key is not null).Select(element => element.Key).ToList();
        return keys.Distinct(StringComparer.Ordinal).Count() == keys.Count;
    }
}

public sealed class NewElementValidator : AbstractValidator<NewElement>
{
    public NewElementValidator()
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

        RuleFor(element => element.Position!)
            .SetValidator(new PositionValidator()).When(element => element.Position is not null);

        RuleFor(element => element.Size!)
            .SetValidator(new SizeValidator()).When(element => element.Size is not null);

        RuleFor(element => element.Color)
            .Must(BoardRules.IsColor).WithErrorCode("invalid-color").WithMessage("A color must look like #RRGGBB.")
            .WithFix("Send a hex color such as \"#FFA94D\", or leave it out to use the type's color.");
    }
}

public sealed class NewConnectionValidator : AbstractValidator<NewConnection>
{
    public NewConnectionValidator()
    {
        RuleFor(connection => connection.From)
            .Must(from => !string.IsNullOrWhiteSpace(from)).WithErrorCode("required").WithMessage("A connection needs a 'from' element.")
            .WithFix("Send the key of an element in this request, or the id of an existing element.");

        RuleFor(connection => connection.To)
            .Must(to => !string.IsNullOrWhiteSpace(to)).WithErrorCode("required").WithMessage("A connection needs a 'to' element.")
            .WithFix("Send the key of an element in this request, or the id of an existing element.");

        RuleFor(connection => connection.Label)
            .MaximumLength(BoardLimits.MaxLabelLength).WithErrorCode("too-long")
            .WithMessage($"A label must be at most {BoardLimits.MaxLabelLength} characters.");
    }
}
