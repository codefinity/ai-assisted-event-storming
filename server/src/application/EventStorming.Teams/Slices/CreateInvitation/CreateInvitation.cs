using EventStorming.SharedKernel;
using EventStorming.Teams.Model;

namespace EventStorming.Teams.Slices.CreateInvitation;

/// <summary>Invites someone to the team with a role. With an email address it is sent there; without one it is a shareable link.</summary>
public sealed record CreateInvitationCommand(Actor Actor, Guid TeamId, string Role, string? Email);

/// <summary><see cref="Token"/> is shown exactly once, to the person who created the invitation.</summary>
public sealed record CreatedInvitation(InvitationView Invitation, string Token);

public sealed class CreateInvitationResult : IUseCaseResult
{
    private CreateInvitationResult(CreatedInvitation? created, IReadOnlyList<Failure> failures)
    {
        Created = created;
        Failures = failures;
    }

    public CreatedInvitation? Created { get; }

    public IReadOnlyList<Failure> Failures { get; }

    public bool Success => Failures.Count == 0;

    public static CreateInvitationResult Succeeded(CreatedInvitation created) => new(created, []);

    public static CreateInvitationResult Failed(IReadOnlyList<Failure> failures) => new(null, failures);

    public static CreateInvitationResult Failed(Failure failure) => new(null, [failure]);
}

public interface ICreateInvitationCommandHandler
{
    Task<CreateInvitationResult> Handle(CreateInvitationCommand command, CancellationToken cancellationToken);
}

public interface ICreateInvitationStore
{
    Task<Team?> FindTeam(Guid teamId, CancellationToken cancellationToken);

    Task Insert(Invitation invitation, CancellationToken cancellationToken);
}
