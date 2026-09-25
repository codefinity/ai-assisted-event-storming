using EventStorming.SharedKernel;
using EventStorming.Teams.Model;
using EventStorming.Teams.Shared;
using FluentValidation;

namespace EventStorming.Teams.Slices.CreateInvitation;

public sealed class CreateInvitationCommandHandler(
    IValidator<CreateInvitationCommand> validator,
    ICreateInvitationStore store,
    IInvitationTokenGenerator tokens,
    IInvitationMailer mailer,
    IClock clock) : ICreateInvitationCommandHandler
{
    public async Task<CreateInvitationResult> Handle(CreateInvitationCommand command, CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
        {
            return CreateInvitationResult.Failed(validation.ToFailures());
        }

        var team = await store.FindTeam(command.TeamId, cancellationToken);
        var myRole = team is null || command.Actor.Kind != ActorKind.Account ? null : team.RoleOf(command.Actor.Id);
        if (team is null || myRole is null)
        {
            return CreateInvitationResult.Failed(Failures.NotFound("The team"));
        }

        if (myRole != TeamRole.Owner)
        {
            return CreateInvitationResult.Failed(Failures.Forbidden("Only a team Owner can invite people.", "Ask an Owner of this team to send the invitation."));
        }

        var now = clock.UtcNow;
        var token = tokens.Generate();
        var email = string.IsNullOrWhiteSpace(command.Email) ? null : command.Email.Trim().ToLowerInvariant();
        var invitation = new Invitation(
            Guid.CreateVersion7(),
            team.Id,
            email is null ? InvitationKind.Link : InvitationKind.Email,
            email,
            TeamRoles.Parse(command.Role),
            token.Hash,
            command.Actor.Id,
            now,
            now + InvitationPolicy.Lifetime);

        await store.Insert(invitation, cancellationToken);

        if (email is not null)
        {
            await mailer.Send(new InvitationMail(email, team.Name, command.Actor.DisplayName, invitation.Role, token.Value, invitation.ExpiresAt), cancellationToken);
        }

        return CreateInvitationResult.Succeeded(new CreatedInvitation(InvitationView.From(invitation), token.Value));
    }
}

public sealed class CreateInvitationCommandValidator : AbstractValidator<CreateInvitationCommand>
{
    public CreateInvitationCommandValidator()
    {
        RuleFor(command => command.Role)
            .Must(TeamRoles.IsValid).WithErrorCode("unknown-role").WithMessage("The role must be owner, editor or viewer.")
            .WithFix("Send \"editor\" for someone who will model, or \"viewer\" for someone who only watches.");

        RuleFor(command => command.Email)
            .EmailAddress().When(command => !string.IsNullOrWhiteSpace(command.Email))
            .WithErrorCode("invalid-email").WithMessage("The email is not a valid email address.")
            .WithFix("Send a valid address, or leave it out to create a shareable link instead.");
    }
}
