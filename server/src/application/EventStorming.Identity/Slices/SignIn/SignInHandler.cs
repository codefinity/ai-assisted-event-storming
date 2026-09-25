using EventStorming.Identity.Model;
using EventStorming.Identity.Shared;
using EventStorming.SharedKernel;
using FluentValidation;

namespace EventStorming.Identity.Slices.SignIn;

public sealed class SignInCommandHandler(
    IValidator<SignInCommand> validator,
    ISignInStore store,
    IPasswordHasher passwordHasher,
    IAccessTokenIssuer accessTokenIssuer,
    IRefreshTokenGenerator refreshTokenGenerator,
    IClock clock) : ISignInCommandHandler
{
    public async Task<SignInResult> Handle(SignInCommand command, CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
        {
            return SignInResult.Failed(validation.ToFailures());
        }

        var account = await store.FindByEmail(EmailAddresses.Normalize(command.Email), cancellationToken);
        if (account is null)
        {
            // Burn the same hashing cost as a real check, so response time does not reveal which
            // emails have accounts.
            passwordHasher.Hash(command.Password);
            return SignInResult.Failed(IncorrectCredentials);
        }

        if (!passwordHasher.Verify(command.Password, account.PasswordHash))
        {
            return SignInResult.Failed(IncorrectCredentials);
        }

        var now = clock.UtcNow;
        var summary = new AccountSummary(account.Id, account.Email, account.DisplayName);
        var refresh = refreshTokenGenerator.Generate();
        var refreshToken = new RefreshToken(
            Guid.CreateVersion7(),
            account.Id,
            FamilyId: Guid.CreateVersion7(),
            refresh.Hash,
            now,
            now + SessionPolicy.RefreshTokenLifetime);

        await store.Save(refreshToken, cancellationToken);

        var access = accessTokenIssuer.Issue(summary);
        return SignInResult.Succeeded(new SessionTokens(access.Value, access.ExpiresAt, refresh.Value, refreshToken.ExpiresAt, summary));
    }

    // Deliberately the same for an unknown email and a wrong password.
    private static readonly Failure IncorrectCredentials = new(
        FailureKind.Unauthenticated,
        "incorrect-credentials",
        "The email or password is incorrect.",
        Fix: "Check both and try again, or create an account if you do not have one.");
}

public sealed class SignInCommandValidator : AbstractValidator<SignInCommand>
{
    public SignInCommandValidator()
    {
        RuleFor(command => command.Email)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithErrorCode("required").WithMessage("Email is required.")
            .EmailAddress().WithErrorCode("invalid-email").WithMessage("Email is not a valid email address.").WithFix("Use the form name@example.com.");

        RuleFor(command => command.Password)
            .NotEmpty().WithErrorCode("required").WithMessage("Password is required.");
    }
}
