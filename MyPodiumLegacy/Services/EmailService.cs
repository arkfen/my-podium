using System.Net;
using System.Net.Mail;

namespace MyPodium.Services;

public interface IEmailService
{
    Task SendVerificationEmailAsync(string toEmail, string verificationCode);
}

public class EmailService : IEmailService
{
    private readonly string _smtpServer;
    private readonly int _smtpPort;
    private readonly string _smtpUsername;
    private readonly string _smtpPassword;
    private readonly string _senderEmail;
    private readonly string _senderName;

    public EmailService(string smtpServer, int smtpPort, string smtpUsername, string smtpPassword, 
        string senderEmail, string senderName)
    {
        _smtpServer = smtpServer;
        _smtpPort = smtpPort;
        _smtpUsername = smtpUsername;
        _smtpPassword = smtpPassword;
        _senderEmail = senderEmail;
        _senderName = senderName;
    }

    public async Task SendVerificationEmailAsync(string toEmail, string verificationCode)
    {
        var client = new SmtpClient(_smtpServer, _smtpPort)
        {
            EnableSsl = true,
            UseDefaultCredentials = false,
            Credentials = new NetworkCredential(_smtpUsername, _smtpPassword)
        };

        var message = new MailMessage
        {
            From = new MailAddress(_senderEmail, _senderName),
            Subject = "Your Verification Code for Podium Dream",
            IsBodyHtml = true,
            Body = $@"
                <h2>Here's your verification code</h2>
                <h1>{verificationCode}</h1>
                <p>This code expires in 10 minutes, use it to log in to your account. 
                Don't share it or forward this email to anyone else – it's for your eyes only! 
                If you didn't request a code, don't worry, you can ignore this email.</p>
            "
        };
        
        message.To.Add(toEmail);
        
        await client.SendMailAsync(message);
    }
}