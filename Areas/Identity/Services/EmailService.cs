using MailKit.Security;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.Options;
using MimeKit;
using MailKit.Net.Smtp;
using os;

namespace os.Areas.Identity.Services;
public class EmailService : IEmailSender
{
    private string emailPass;

    public EmailService(IConfiguration configuration)
    {
        emailPass = Environment.GetEnvironmentVariable("OA_Email_Pass");
        if (emailPass == null)
        {

            emailPass = Environment.GetEnvironmentVariable("OS_Email_Pass");
        }
    }

    public async Task SendEmailAsync(string toEmail, string subject, string message)
    {
        await Execute(subject, message, toEmail);
    }
    public async Task Execute(string subject, string message, string toEmail)
    {
        var email = new MimeMessage();
        email.From.Add(MailboxAddress.Parse("cs@magnadigi.com"));
        email.To.Add(MailboxAddress.Parse(toEmail));
        email.Subject = subject;
        email.Body = new TextPart(MimeKit.Text.TextFormat.Html)
        {
            Text = message
        };
        using var smtp = new SmtpClient();
        smtp.Connect("us2.smtp.mailhostbox.com", 587, SecureSocketOptions.StartTls);
        smtp.Authenticate("cs@magnadigi.com", emailPass);
        var response = smtp.Send(email);
        smtp.Disconnect(true);
    }
}

