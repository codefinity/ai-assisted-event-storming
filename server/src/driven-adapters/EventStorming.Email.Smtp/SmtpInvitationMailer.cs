using System.Globalization;
using EventStorming.Teams.Model;
using EventStorming.Teams.Shared;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using MimeKit;

namespace EventStorming.Email.Smtp;

/// <param name="WebAppBaseUrl">Where the web app is served, for the link in the email (e.g. http://localhost:3000).</param>
public sealed record SmtpOptions(string Host, int Port, string From, string WebAppBaseUrl, string? Username = null, string? Password = null);

/// <summary>
/// Sends invitation emails over SMTP (Mailpit in development). A failure is logged, not thrown: the
/// invitation already exists, and its Owner can still copy the link from the team settings.
/// </summary>
internal sealed class SmtpInvitationMailer(SmtpOptions options, ILogger<SmtpInvitationMailer> logger) : IInvitationMailer
{
    private static readonly TimeSpan SendTimeout = TimeSpan.FromSeconds(10);

    public async Task Send(InvitationMail mail, CancellationToken cancellationToken)
    {
        var link = $"{options.WebAppBaseUrl.TrimEnd('/')}/invitations/{Uri.EscapeDataString(mail.Token)}";
        var role = mail.Role switch
        {
            TeamRole.Owner => "an Owner",
            TeamRole.Editor => "an Editor",
            _ => "a Viewer",
        };

        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse(options.From));
        message.To.Add(MailboxAddress.Parse(mail.To));
        message.Subject = $"{mail.InvitedBy} invited you to {mail.TeamName} on EventStorming";
        message.Body = new TextPart("plain")
        {
            Text = $"""
                Hi,

                {mail.InvitedBy} invited you to join the team "{mail.TeamName}" as {role}.

                Accept the invitation: {link}

                The link works until {mail.ExpiresAt.ToString("d MMMM yyyy", CultureInfo.InvariantCulture)}.
                """,
        };

        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(SendTimeout);

            using var client = new SmtpClient();
            await client.ConnectAsync(options.Host, options.Port, SecureSocketOptions.Auto, timeout.Token);
            if (!string.IsNullOrEmpty(options.Username))
            {
                await client.AuthenticateAsync(options.Username, options.Password ?? string.Empty, timeout.Token);
            }

            await client.SendAsync(message, timeout.Token);
            await client.DisconnectAsync(true, timeout.Token);
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            logger.LogError(exception, "Could not send the invitation email for team {TeamName} to {Recipient}.", mail.TeamName, mail.To);
        }
    }
}
