# Email Configuration Guide - Podium API

## Overview

The API sends verification codes via email when users sign in with OTP. This guide shows how to configure email sending.

## Quick Setup (Gmail)

### 1. Enable 2-Factor Authentication on Gmail
1. Go to Google Account settings
2. Security ? 2-Step Verification
3. Enable it

### 2. Generate App Password
1. Google Account ? Security
2. 2-Step Verification ? App passwords
3. Select "Mail" and "Other (Custom name)"
4. Name it "Podium API"
5. Copy the 16-character password

### 3. Configure API

**Option A: User Secrets (Recommended for Development)**
```bash
cd Podium/Podium.Api

dotnet user-secrets set "EmailSettings:SmtpServer" "smtp.gmail.com"
dotnet user-secrets set "EmailSettings:SmtpPort" "587"
dotnet user-secrets set "EmailSettings:Username" "your-email@gmail.com"
dotnet user-secrets set "EmailSettings:Password" "your-app-password-here"
dotnet user-secrets set "EmailSettings:SenderEmail" "your-email@gmail.com"
dotnet user-secrets set "EmailSettings:SenderName" "Podium"
```

**Option B: appsettings.Development.json**
```json
{
  "EmailSettings": {
    "SmtpServer": "smtp.gmail.com",
    "SmtpPort": 587,
    "Username": "your-email@gmail.com",
    "Password": "your-16-char-app-password",
    "SenderEmail": "your-email@gmail.com",
    "SenderName": "Podium"
  }
}
```

### 4. Test It
1. Start the API: `dotnet run`
2. Look for: `? Email service configured`
3. Try signing in with email OTP
4. Check your inbox!

---

## Other Email Providers

### Outlook/Hotmail
```json
{
  "EmailSettings": {
    "SmtpServer": "smtp-mail.outlook.com",
    "SmtpPort": 587,
    "Username": "your-email@outlook.com",
    "Password": "your-password",
    "SenderEmail": "your-email@outlook.com",
    "SenderName": "Podium"
  }
}
```

### SendGrid
```json
{
  "EmailSettings": {
    "SmtpServer": "smtp.sendgrid.net",
    "SmtpPort": 587,
    "Username": "apikey",
    "Password": "your-sendgrid-api-key",
    "SenderEmail": "verified-sender@yourdomain.com",
    "SenderName": "Podium"
  }
}
```

### Mailgun
```json
{
  "EmailSettings": {
    "SmtpServer": "smtp.mailgun.org",
    "SmtpPort": 587,
    "Username": "postmaster@your-domain.mailgun.org",
    "Password": "your-mailgun-smtp-password",
    "SenderEmail": "noreply@yourdomain.com",
    "SenderName": "Podium"
  }
}
```

### Custom SMTP Server
```json
{
  "EmailSettings": {
    "SmtpServer": "mail.yourdomain.com",
    "SmtpPort": 587,
    "Username": "noreply@yourdomain.com",
    "Password": "your-password",
    "SenderEmail": "noreply@yourdomain.com",
    "SenderName": "Podium"
  }
}
```

---

## Production Setup

### Azure Key Vault (Recommended)
```csharp
// In Program.cs
builder.Configuration.AddAzureKeyVault(
    new Uri($"https://{keyVaultName}.vault.azure.net/"),
    new DefaultAzureCredential());
```

Store secrets in Key Vault:
- `EmailSettings--SmtpServer`
- `EmailSettings--SmtpPort`
- `EmailSettings--Username`
- `EmailSettings--Password`
- `EmailSettings--SenderEmail`
- `EmailSettings--SenderName`

### Environment Variables
```bash
export EmailSettings__SmtpServer="smtp.gmail.com"
export EmailSettings__SmtpPort="587"
export EmailSettings__Username="your-email@gmail.com"
export EmailSettings__Password="your-app-password"
export EmailSettings__SenderEmail="your-email@gmail.com"
export EmailSettings__SenderName="Podium"
```

---

## Fallback Behavior

If email is **not configured**:
- ? API still works
- ? OTP codes are logged to console
- ?? Users won't receive emails
- ?? Check API console output for codes during testing

API startup will show:
```
?? Email service not configured - OTP codes will be logged to console only
```

---

## Email Template

The email sent to users looks like this:

**Subject:** Your Verification Code for Podium

**Body:**
```
?? Podium

Your Verification Code

Use this code to sign in to your Podium account:

[ 123456 ]

This code expires in 10 minutes.

If you didn't request this code, you can safely ignore this email.
```

---

## Troubleshooting

### Email not sending?
1. Check API console for errors
2. Verify SMTP credentials
3. Test with a simple email client first
4. Check firewall/network allows SMTP traffic
5. For Gmail: Ensure "Less secure app access" is OFF (use app password instead)

### "Authentication failed" error?
- Gmail: Use app password, not your regular password
- Outlook: Enable SMTP in account settings
- SendGrid/Mailgun: Verify API key is correct

### Email goes to spam?
- Set up SPF/DKIM records for your domain
- Use a verified sender email
- Consider using a dedicated email service (SendGrid, Mailgun)

### Rate limiting?
- Gmail: 500 emails/day for free accounts
- Outlook: 300 emails/day for free accounts
- SendGrid: 100 emails/day on free tier
- Consider upgrading or using a professional service

---

## Best Practices

### Development
- ? Use user secrets for credentials
- ? Test with your own email first
- ? Keep app passwords secure
- ? Don't commit credentials to Git

### Production
- ? Use Azure Key Vault or similar
- ? Use a dedicated email service (SendGrid, Mailgun)
- ? Set up SPF, DKIM, DMARC records
- ? Monitor email delivery rates
- ? Have error logging and alerting
- ? Use a "noreply@" sender address

---

## Security Notes

?? **Never commit email passwords to source control!**

? **Always use:**
- User secrets in development
- Key vaults in production
- App passwords (not main passwords)
- Environment variables for containers

?? **The email service:**
- Uses TLS/SSL encryption
- Validates recipient emails
- Handles failures gracefully
- Logs errors without exposing credentials

---

## Testing Without Email

During development, you can test OTP flow without email:

1. Don't configure email settings
2. Request OTP in the app
3. Check API console output:
   ```
   OTP Code for john@example.com: 123456
   ```
4. Use the code from console to sign in

This is perfect for:
- Initial development
- Unit testing
- CI/CD pipelines
- Demo environments

---

## Need Help?

- Gmail setup issues: https://support.google.com/mail/answer/7126229
- Outlook setup: https://support.microsoft.com/en-us/office/pop-imap-and-smtp-settings
- SendGrid docs: https://docs.sendgrid.com/
- Mailgun docs: https://documentation.mailgun.com/
