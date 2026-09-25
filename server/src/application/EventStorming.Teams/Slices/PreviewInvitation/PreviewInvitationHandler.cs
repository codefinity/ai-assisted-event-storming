using EventStorming.SharedKernel;
using EventStorming.Teams.Model;
using EventStorming.Teams.Shared;

namespace EventStorming.Teams.Slices.PreviewInvitation;

public sealed class PreviewInvitationQueryHandler(
    IPreviewInvitationStore store,
    IInvitationTokenGenerator tokens,
    IClock clock) : IPreviewInvitationQueryHandler
{
    public async Task<PreviewInvitationResult> Handle(PreviewInvitationQuery query, CancellationToken cancellationToken)
    {
        var found = string.IsNullOrWhiteSpace(query.Token)
            ? null
            : await store.FindByHash(tokens.HashOf(query.Token), cancellationToken);
        if (found is null)
        {
            return PreviewInvitationResult.Failed(Failures.NotFound("The invitation", "Check the link, or ask the team for a new invitation."));
        }

        var invitation = found.Invitation;
        return PreviewInvitationResult.Succeeded(new InvitationPreview(
            found.TeamName,
            found.InvitedBy,
            invitation.Role,
            invitation.Kind,
            invitation.Email,
            invitation.ExpiresAt,
            InvitationStatuses.Of(invitation, clock.UtcNow)));
    }
}
