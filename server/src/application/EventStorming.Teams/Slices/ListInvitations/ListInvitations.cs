using EventStorming.SharedKernel;
using EventStorming.Teams.Model;

namespace EventStorming.Teams.Slices.ListInvitations;

/// <summary>The invitations of a team that can still be used.</summary>
public sealed record ListInvitationsQuery(Actor Actor, Guid TeamId);

public sealed class ListInvitationsResult : IUseCaseResult
{
    private ListInvitationsResult(IReadOnlyList<InvitationView> invitations, IReadOnlyList<Failure> failures)
    {
        Invitations = invitations;
        Failures = failures;
    }

    public IReadOnlyList<InvitationView> Invitations { get; }

    public IReadOnlyList<Failure> Failures { get; }

    public bool Success => Failures.Count == 0;

    public static ListInvitationsResult Succeeded(IReadOnlyList<InvitationView> invitations) => new(invitations, []);

    public static ListInvitationsResult Failed(Failure failure) => new([], [failure]);
}

public interface IListInvitationsQueryHandler
{
    Task<ListInvitationsResult> Handle(ListInvitationsQuery query, CancellationToken cancellationToken);
}

public interface IListInvitationsStore
{
    Task<Team?> FindTeam(Guid teamId, CancellationToken cancellationToken);

    Task<IReadOnlyList<Invitation>> InvitationsOf(Guid teamId, CancellationToken cancellationToken);
}
