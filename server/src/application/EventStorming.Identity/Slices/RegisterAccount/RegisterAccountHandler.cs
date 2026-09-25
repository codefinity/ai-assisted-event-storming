using EventStorming.Identity.Model;
using EventStorming.Identity.Shared;
using EventStorming.SharedKernel;
using FluentValidation;

namespace EventStorming.Identity.Slices.RegisterAccount;

public sealed class RegisterAccountCommandHandler(
    IValidator<RegisterAccountCommand> validator,
    IRegisterAccountStore store,
    IPasswordHasher passwordHasher,
    IClock clock) : IRegisterAccountCommandHandler
{
    public async Task<RegisterAccountResult> Handle(RegisterAccountCommand command, CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
        {
            return RegisterAccountResult.Failed(validation.ToFailures());
        }

        var account = new Account(
            Guid.CreateVersion7(),
            EmailAddresses.Normalize(command.Email),
            command.DisplayName.Trim(),
            passwordHasher.Hash(command.Password),
            clock.UtcNow);

        if (!await store.TryInsert(account, cancellationToken))
        {
            return RegisterAccountResult.Failed(Failures.Conflict(
                "email-taken",
                "An account with this email already exists.",
                "email",
                "Sign in with this email instead, or register with a different one."));
        }

        return RegisterAccountResult.Succeeded(new AccountSummary(account.Id, account.Email, account.DisplayName));
    }
}

public sealed class RegisterAccountCommandValidator : AbstractValidator<RegisterAccountCommand>
{
    public const int MinimumPasswordLength = 8;
    public const int MaximumPasswordLength = 128;

    public RegisterAccountCommandValidator()
    {
        RuleFor(command => command.Email)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithErrorCode("required").WithMessage("Email is required.").WithFix("Send the email address you want to sign in with.")
            .MaximumLength(254).WithErrorCode("too-long").WithMessage("Email must be at most 254 characters.")
            .EmailAddress().WithErrorCode("invalid-email").WithMessage("Email is not a valid email address.").WithFix("Use the form name@example.com.");

        RuleFor(command => command.DisplayName)
            .Cascade(CascadeMode.Stop)
            .Must(name => !string.IsNullOrWhiteSpace(name)).WithErrorCode("required").WithMessage("Display name is required.").WithFix("Send the name other team members will see, e.g. \"Ana\".")
            .MaximumLength(60).WithErrorCode("too-long").WithMessage("Display name must be at most 60 characters.");

        RuleFor(command => command.Password)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithErrorCode("required").WithMessage("Password is required.")
            .MinimumLength(MinimumPasswordLength).WithErrorCode("too-short").WithMessage($"Password must be at least {MinimumPasswordLength} characters.").WithFix("Choose a longer password; a short phrase works well.")
            .MaximumLength(MaximumPasswordLength).WithErrorCode("too-long").WithMessage($"Password must be at most {MaximumPasswordLength} characters.");
    }
}
