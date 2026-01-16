using System.Net;
using System.Net.Mail;

namespace Podium.Api.Services;

public interface IEmailService
{
    Task SendVerificationEmailAsync(string toEmail, string verificationCode);
    Task SendTemporaryPasswordEmailAsync(string toEmail, string username, string temporaryPassword);
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
        try
        {
            using var client = new SmtpClient(_smtpServer, _smtpPort)
            {
                EnableSsl = true,
                UseDefaultCredentials = false,
                Credentials = new NetworkCredential(_smtpUsername, _smtpPassword)
            };

            var message = new MailMessage
            {
                From = new MailAddress(_senderEmail, _senderName),
                Subject = "Your Verification Code for YouCent Podium",
                IsBodyHtml = true,
                Body = $@"
                    <!DOCTYPE html>
                    <html>
                    <head>
                        <style>
                            body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
                            .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
                            .header {{ background: #2563eb; color: white; padding: 20px; text-align: center; border-radius: 8px 8px 0 0; }}
                            .content {{ background: #f9fafb; padding: 30px; border-radius: 0 0 8px 8px; }}
                            .code {{ font-size: 32px; font-weight: bold; color: #2563eb; text-align: center; letter-spacing: 8px; padding: 20px; background: white; border-radius: 8px; margin: 20px 0; }}
                            .footer {{ text-align: center; margin-top: 20px; color: #6b7280; font-size: 14px; }}
                        </style>
                    </head>
                    <body>
                        <div class='container'>
                            <div class='header'>
                                <h1>YouCent Podium</h1>
                            </div>
                            <div class='content'>
                                <h2>Your Verification Code</h2>
                                <p>Use this code to sign in to your Podium account:</p>
                                <div class='code'>{verificationCode}</div>
                                <p><strong>This code expires in 10 minutes.</strong></p>
                                <p>If you didn't request this code, you can safely ignore this email.</p>
                            </div>
                            <div class='footer'>
                                <p>This is an automated message, please do not reply.</p>
                                <p>&copy; {DateTime.Now.Year} Podium. All rights reserved.</p>
                            </div>
                        </div>
                    </body>
                    </html>
                "
            };
            
            message.To.Add(toEmail);
            
            await client.SendMailAsync(message);
        }
        catch (Exception ex)
        {
            // Log the error but don't throw - we don't want to break the flow
            Console.WriteLine($"Failed to send email to {toEmail}: {ex.Message}");
            throw; // Re-throw so caller knows it failed
        }
    }

    public async Task SendTemporaryPasswordEmailAsync(string toEmail, string username, string temporaryPassword)
    {
        try
        {
            using var client = new SmtpClient(_smtpServer, _smtpPort)
            {
                EnableSsl = true,
                UseDefaultCredentials = false,
                Credentials = new NetworkCredential(_smtpUsername, _smtpPassword)
            };

            var message = new MailMessage
            {
                From = new MailAddress(_senderEmail, _senderName),
                Subject = "Your Temporary Password - Podium",
                IsBodyHtml = true,
                Body = $@"
                    <!DOCTYPE html>
                    <html>
                    <head>
                        <style>
                            body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
                            .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
                            .header {{ background: #2563eb; color: white; padding: 20px; text-align: center; border-radius: 8px 8px 0 0; }}
                            .content {{ background: #f9fafb; padding: 30px; border-radius: 0 0 8px 8px; }}
                            .password {{ font-size: 24px; font-weight: bold; color: #2563eb; text-align: center; letter-spacing: 2px; padding: 20px; background: white; border-radius: 8px; margin: 20px 0; font-family: 'Courier New', monospace; }}
                            .warning {{ background: #fef3c7; border-left: 4px solid #f59e0b; padding: 15px; margin: 20px 0; border-radius: 4px; }}
                            .footer {{ text-align: center; margin-top: 20px; color: #6b7280; font-size: 14px; }}
                        </style>
                    </head>
                    <body>
                        <div class='container'>
                            <div class='header'>
                                <h1>Podium</h1>
                            </div>
                            <div class='content'>
                                <h2>Your Temporary Password</h2>
                                <p>Hello {username},</p>
                                <p>A temporary password has been set for your account by an administrator.</p>
                                <div class='password'>{temporaryPassword}</div>
                                <div class='warning'>
                                    <p><strong>⚠️ Important Security Notice:</strong></p>
                                    <p>• This is a temporary password. Please sign in and change it immediately.</p>
                                    <p>• Do not share this password with anyone.</p>
                                    <p>• If you did not request this password reset, please contact your administrator immediately.</p>
                                </div>
                                <p>To sign in, go to the Podium application and use your email address with this temporary password.</p>
                            </div>
                            <div class='footer'>
                                <p>This is an automated message, please do not reply.</p>
                                <p>&copy; {DateTime.Now.Year} Podium. All rights reserved.</p>
                            </div>
                        </div>
                    </body>
                    </html>
                "
            };
            
            message.To.Add(toEmail);
            
            await client.SendMailAsync(message);
        }
        catch (Exception ex)
        {
            // Log the error but don't throw - we don't want to break the flow
            Console.WriteLine($"Failed to send temporary password email to {toEmail}: {ex.Message}");
            throw; // Re-throw so caller knows it failed
        }
    }
}
