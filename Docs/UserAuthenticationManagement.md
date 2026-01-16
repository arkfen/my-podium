# User Authentication Management

This document describes the advanced user authentication management capabilities available to administrators in the Podium application.

## Overview

Administrators can now manage user authentication methods and initiate password setup/reset for users through the admin panel. This provides greater flexibility in how users can access the system and helps with user onboarding and account recovery.

## Features

### 1. Configure Sign-In Methods

Administrators can configure which authentication methods are allowed for each user:

- **Both** (default): Users can sign in using either email OTP or password
- **Email**: Users can only sign in using email OTP (passwordless)
- **Password**: Users can only sign in using their password

**Important**: The authentication method is enforced at the API level. If a user's method is set to "Email", they will not be able to sign in with a password, even if they have one set. Similarly, if set to "Password", they cannot use email OTP.

#### How to Change Authentication Method

1. Navigate to **Admin > Manage Users** (`/admin/users`)
2. Click **Edit** on the user you want to modify
3. In the edit form, select the desired authentication method from the **Preferred Authentication Method** dropdown
4. Click **Save Changes**

The authentication method will be updated immediately and enforced on the user's next sign-in attempt.

### 2. Setup/Reset User Password

Administrators can generate temporary passwords for users who need:
- Initial password setup (for new users)
- Password reset (for users who forgot their password)
- Password recovery (for locked accounts)

**Important Security Feature**: The temporary password is **emailed directly to the user** - the administrator never sees the password. This ensures security and accountability.

#### How to Setup Password

**From User Management List:**
1. Navigate to **Admin > Manage Users** (`/admin/users`)
2. Click **Setup Password** on the user row
3. A confirmation dialog will appear showing:
   - The user's information
   - A warning that the password will be emailed to the user
   - A warning that you (the admin) will NOT see the password
4. Click **Confirm & Email Password** to proceed
5. A success dialog will confirm the email was sent

**From User Details Page:**
1. Navigate to **Admin > Manage Users** and click on a username
2. On the user details page, click **Setup/Reset Password**
3. Follow the same confirmation process as above

#### Security Considerations

**Temporary Password Characteristics:**
- 12 characters long
- Contains uppercase letters, lowercase letters, numbers, and special characters
- Generated using cryptographically secure random number generation
- Meets strong password requirements

**Best Practices:**
- The temporary password is **never shown to administrators** - it's sent directly to the user via email
- Users receive a professional email with clear instructions
- The email includes security warnings and instructions to change the password after first login
- Password is set in the database only after admin confirmation
- If email delivery fails, the admin is notified immediately

#### Automatic Behavior

When you setup a password for a user:
- A confirmation dialog ensures you don't accidentally change a password
- The password is only generated and saved after confirmation
- If the user's authentication method is "Email" (OTP only), it will automatically be updated to "Both" to allow password login
- The user receives an email with:
  - The temporary password
  - Instructions to sign in
  - Security warnings
  - Instructions to change the password after first login
- If email delivery fails, you're notified and can contact the user through alternative means

## API Endpoints

### Update Authentication Method
```
PUT /api/admin/users/{userId}/auth-method
```

**Request Body:**
```json
{
  "preferredAuthMethod": "Both" // or "Email" or "Password"
}
```

**Response:**
```json
{
  "message": "Authentication method updated successfully"
}
```

### Initiate Password Setup
```
POST /api/admin/users/{userId}/setup-password
```

**Response:**
```json
{
  "userId": "user123",
  "username": "john_doe",
  "email": "john@example.com",
  "currentAuthMethod": "Email",
  "message": "Ready to setup password. Confirm to generate and email password to user."
}
```

### Confirm Password Setup
```
POST /api/admin/users/{userId}/setup-password/confirm
```

**Response:**
```json
{
  "message": "Password has been generated and emailed to john@example.com.",
  "email": "john@example.com",
  "success": true
}
```

## User Experience

### For Administrators
- Clear two-step process prevents accidental password changes
- Confirmation dialog shows exactly what will happen
- Success confirmation when email is sent
- Notification if email delivery fails
- No password exposure - maintains security
- Consistent experience across user management and user details pages

### For Users
- Receive professional email with temporary password
- Clear instructions on how to sign in
- Security warnings and best practices included
- Flexibility in choosing how to authenticate after password is set
- No disruption to existing authentication methods when passwords are added

## Authentication Method Enforcement

The system now properly enforces the PreferredAuthMethod setting:

- **Email-only users**: Cannot sign in with password, even if one exists. The `/api/auth/signin` endpoint will reject password authentication attempts.
- **Password-only users**: Cannot request OTP codes. The `/api/auth/send-otp` endpoint will reject OTP requests.
- **Both**: Users can choose either method for each sign-in.

This ensures that the authentication method setting is not just a preference but an enforced security policy.

## Troubleshooting

**Issue**: User cannot sign in with password after setup
- **Solution**: Verify the user's authentication method is set to "Both" or "Password" in the admin panel
- Check that the user is using the correct email address
- Verify the temporary password was received via email

**Issue**: User reports not receiving password email
- **Solution**: Check the user's email address is correct in the system
- Verify SMTP settings are configured correctly
- Check spam/junk folders
- If email delivery failed, the admin was notified - generate a new password

**Issue**: Cannot setup password for a user
- **Solution**: Ensure you have admin privileges
- Check that the user exists and is active
- Verify email service is configured
- Check API connectivity

**Issue**: Lost the temporary password
- **Solution**: The password was emailed to the user - have them check their email
- If email was not received, generate a new password using the "Setup Password" button again
- The previous temporary password will be overwritten

**Issue**: User trying to sign in with wrong method
- **Solution**: Check the user's PreferredAuthMethod setting
- If set to "Email", they must use OTP (not password)
- If set to "Password", they must use password (not OTP)
- Change to "Both" if they need both options

## Security Notes

1. **Admin Privileges Required**: Only users with admin privileges can access these features
2. **Password Privacy**: Administrators never see user passwords
3. **Confirmation Required**: Two-step process prevents accidental password changes
4. **Email Verification**: Email delivery status is reported
5. **Audit Trail**: All password setup actions should be logged (consider adding audit logging)
6. **Password Strength**: All generated passwords meet strong security requirements
7. **Authentication Enforcement**: API enforces auth method restrictions
8. **Secure Transmission**: Always use HTTPS in production to protect password transmission

## Email Configuration

The password email feature requires SMTP configuration. See `EmailConfiguration.md` for details on setting up the email service.

If email is not configured:
- Password setup will still work
- Passwords will be logged to console (development only)
- Admin will be notified of email delivery failure
- Admin should contact user through alternative means

## Future Enhancements

Potential improvements for future versions:
- Temporary password expiration (force change on first login)
- Password history to prevent reuse
- Audit log of authentication method changes
- Audit log of password setup actions
- Bulk password reset functionality
- Password strength policy configuration
