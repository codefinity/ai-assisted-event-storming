using EventStorming.PublicIntegration.Model;
using EventStorming.PublicIntegration.Shared;
using EventStorming.SharedKernel;
using FluentValidation;

namespace EventStorming.PublicIntegration.Slices.CreateApiKey;

public sealed class CreateApiKeyCommandHandler(
    IValidator<CreateApiKeyCommand> validator,
    ITeamOwnership ownership,
    IApiKeySecretGenerator secrets,
    ICreateApiKeyStore store,
    IClock clock) : ICreateApiKeyCommandHandler
{
    public async Task<CreateApiKeyResult> Handle(CreateApiKeyCommand command, CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(command, cancellationToken);
        var failures = validation.ToFailures().ToList();
        var now = clock.UtcNow;
        if (command.ExpiresAt is { } expiresAt && expiresAt <= now)
        {
            failures.Add(Failures.Invalid("expiresAt", "in-the-past", "The expiry must be in the future.", "Leave expiresAt out for a key that never expires."));
        }

        if (failures.Count > 0)
        {
            return CreateApiKeyResult.Failed(failures);
        }

        if (command.Actor.Kind != ActorKind.Account || !await ownership.IsOwner(command.TeamId, command.Actor.Id, cancellationToken))
        {
            return CreateApiKeyResult.Failed(Failures.Forbidden("Only a team Owner can create API keys.", "Ask an Owner of this team to create the key."));
        }

        var id = Guid.CreateVersion7();
        var secret = secrets.NewSecret();
        var key = new ApiKey(
            id,
            command.TeamId,
            command.Name.Trim(),
            ApiKeyFormat.DisplayPrefix(id),
            secrets.HashOf(secret),
            ApiScopes.Normalize(command.Scopes),
            now,
            command.Actor.Id,
            command.ExpiresAt);

        await store.Insert(key, cancellationToken);
        return CreateApiKeyResult.Succeeded(new CreatedApiKey(ApiKeySummary.From(key), ApiKeyFormat.Compose(id, secret)));
    }
}

public sealed class CreateApiKeyCommandValidator : AbstractValidator<CreateApiKeyCommand>
{
    public CreateApiKeyCommandValidator()
    {
        RuleFor(command => command.Name)
            .Cascade(CascadeMode.Stop)
            .Must(name => !string.IsNullOrWhiteSpace(name)).WithErrorCode("required").WithMessage("An API key needs a name.")
            .WithFix("Name it after what will use it, e.g. \"Claude integration\".")
            .MaximumLength(60).WithErrorCode("too-long").WithMessage("An API key name must be at most 60 characters.");

        RuleFor(command => command.Scopes)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithErrorCode("required").WithMessage("An API key needs at least one scope.")
            .WithFix("Send [\"read\"] for a read-only key, or [\"read\", \"write\"].")
            .Must(scopes => scopes.All(scope => ApiScopes.All.Contains(scope.Trim().ToLowerInvariant())))
            .WithErrorCode("unknown-scope").WithMessage("Scopes must be \"read\" or \"write\".")
            .WithFix("Send [\"read\"] for a read-only key, or [\"read\", \"write\"].");
    }
}
