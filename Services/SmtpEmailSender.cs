using System.Net;
using System.Net.Mail;
using FlightCrewStudio.Data;
using Microsoft.AspNetCore.Identity;

namespace FlightCrewStudio.Services;

/// <summary>
/// Sends Identity emails (confirmation, password reset) over SMTP when "Smtp:*" is configured.
/// Without SMTP, the app registers the framework no-op sender instead.
/// </summary>
public sealed class SmtpEmailSender(IConfiguration config, ILogger<SmtpEmailSender> logger)
    : IEmailSender<ApplicationUser>
{
    public Task SendConfirmationLinkAsync(ApplicationUser user, string email, string confirmationLink) =>
        SendAsync(email, "Confirm your account",
            $"Please confirm your account by clicking <a href='{confirmationLink}'>here</a>.");

    public Task SendPasswordResetLinkAsync(ApplicationUser user, string email, string resetLink) =>
        SendAsync(email, "Reset your password",
            $"Reset your password by clicking <a href='{resetLink}'>here</a>.");

    public Task SendPasswordResetCodeAsync(ApplicationUser user, string email, string resetCode) =>
        SendAsync(email, "Reset your password", $"Your password reset code is: {resetCode}");

    private async Task SendAsync(string to, string subject, string htmlBody)
    {
        var host = config["Smtp:Host"]!;
        var port = int.TryParse(config["Smtp:Port"], out var p) ? p : 587;
        var user = config["Smtp:Username"];
        var pass = config["Smtp:Password"];
        var from = config["Smtp:From"] ?? user ?? "no-reply@flightcrewstudio.local";
        var useSsl = !bool.TryParse(config["Smtp:UseSsl"], out var ssl) || ssl;

        using var client = new SmtpClient(host, port) { EnableSsl = useSsl };
        if (!string.IsNullOrEmpty(user))
            client.Credentials = new NetworkCredential(user, pass);

        using var message = new MailMessage(from, to, subject, htmlBody) { IsBodyHtml = true };
        try
        {
            await client.SendMailAsync(message);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send email to {To}", to);
            throw;
        }
    }
}
