using EventStorming.SharedKernel;
using EventStorming.Teams.Model;
using EventStorming.Teams.Shared;

namespace EventStorming.Teams.Slices.AcceptInvitation;

public sealed class AcceptInvitationCommandHandler(
    IAcceptInvitationStore store,
    IInvitationTokenGenerator tokens,
    IClock clock) : IAcceptInvitationCommandHandler
{
    public async Task<AcceptInvitationResult> Handle(AcceptInvitationCommand command, CancellationToken cancellationToken)
    {
        if (command.Actor.Kind != ActorKind.Account)
        {
            return AcceptInvitationResult.Failed(Failures.Forbidden("Invitations are accepted by people, not by API keys."));
        }

        var invitation = string.IsNullOrWhiteSpace(command.Token)
            ? null
            : await store.FindByHash(tokens.HashOf(command.Token), cancellationToken);
        var team = invitation is null ? null : await store.FindTeam(invitation.TeamId, cancellationToken);
        if (invitation is null || team is null)
        {
            return AcceptInvitationResult.Failed(Failures.NotFound("The invitation", "Check the link, or ask the team for a new invitation."));
        }

        // Accepting twice is harmless: an existing member keeps the role they already have.
        var existingRole = team.RoleOf(command.Actor.Id);
        if (existingRole is not null)
        {
            return AcceptInvitationResult.Succeeded(new TeamSummary(team.Id, team.Name, existingRole.Value, team.Members.Count));
        }

        var now = clock.UtcNow;
        switch (InvitationStatuses.Of(invitation, now))
        {
            case InvitationStatus.Revoked:
                return AcceptInvitationResult.Failed(Failures.Conflict("invitation-revoked", "This invitation has been revoked.", fix: "Ask the team for a new invitation."));
            case InvitationStatus.Used:
                return AcceptInvitationResult.Failed(Failures.Conflict("invitation-used", "This invitation has already been used.", fix: "Ask the team for a new invitation."));
            case InvitationStatus.Expired:
                return AcceptInvitationResult.Failed(Failures.Conflict("invitation-expired", "This invitation has expired.", fix: "Ask the team for a new invitation."));
        }

        if (invitation.Kind == InvitationKind.Email)
        {
            var email = await store.EmailOf(command.Actor.Id, cancellationToken);
            if (!string.Equals(email, invitation.Email, StringComparison.OrdinalIgnoreCase))
            {
                return AcceptInvitationResult.Failed(Failures.Forbidden(
                    "This invitation was sent to a different email address.",
                    $"Sign in with {invitation.Email} to accept it."));
            }
        }

        var membership = new Membership(command.Actor.Id, invitation.Role, now);
        var consume = invitation.Kind == InvitationKind.Email ? invitation.Id : (Guid?)null;
        var added = await store.AddMember(team.Id, membership, consume, now, cancellationToken);

        return AcceptInvitationResult.Succeeded(new TeamSummary(team.Id, team.Name, invitation.Role, team.Members.Count + (added ? 1 : 0)));
    }
}
