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

#### How to Change Authentication Method

1. Navigate to **Admin > Manage Users** (`/admin/users`)
2. Click **Edit** on the user you want to modify
3. In the edit form, select the desired authentication method from the **Preferred Authentication Method** dropdown
4. Click **Save Changes**

The authentication method will be updated immediately.

### 2. Setup/Reset User Password

Administrators can generate temporary passwords for users who need:
- Initial password setup (for new users)
- Password reset (for users who forgot their password)
- Password recovery (for locked accounts)

#### How to Setup Password

**From User Management List:**
1. Navigate to **Admin > Manage Users** (`/admin/users`)
2. Click **Setup Password** on the user row
3. A secure temporary password will be generated and displayed in a modal dialog
4. **Important**: This password is shown only once - copy it immediately
5. Share the password securely with the user

**From User Details Page:**
1. Navigate to **Admin > Manage Users** and click on a username
2. On the user details page, click **Setup/Reset Password**
3. Follow the same steps as above

#### Security Considerations

**Temporary Password Characteristics:**
- 12 characters long
- Contains uppercase letters, lowercase letters, numbers, and special characters
- Generated using cryptographically secure random number generation
- Meets strong password requirements

**Best Practices:**
- The temporary password is displayed only once - ensure you save it before closing the dialog
- Share passwords securely with users (encrypted email, secure messaging, or in person)
- Instruct users to change their password after first login
- Never store temporary passwords in plain text logs or unencrypted files

#### Automatic Behavior

When you setup a password for a user:
- If the user's authentication method is "Email" (OTP only), it will automatically be updated to "Both" to allow password login
- The user can immediately use the temporary password to sign in
- The user's existing email OTP functionality remains available (if auth method is "Both")

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

### Setup User Password
```
POST /api/admin/users/{userId}/setup-password
```

**Response:**
```json
{
  "message": "Password setup initiated. Temporary password generated.",
  "temporaryPassword": "Abc123!@#Xyz",
  "warning": "Please share this password securely with the user. It will not be shown again."
}
```

## User Experience

### For Administrators
- Clear UI with dropdown selectors for authentication methods
- One-click password generation with secure display
- Warning messages to ensure proper password handling
- Consistent experience across user management and user details pages

### For Users
- Flexibility in choosing how to authenticate
- Immediate access after password setup
- No disruption to existing authentication methods when passwords are added

## Troubleshooting

**Issue**: User cannot sign in with password after setup
- **Solution**: Verify the user's authentication method is set to "Both" or "Password"
- Check that the password was copied correctly

**Issue**: Cannot setup password for a user
- **Solution**: Ensure you have admin privileges
- Check that the user exists and is active
- Verify API connectivity

**Issue**: Lost the temporary password
- **Solution**: Generate a new password using the "Setup Password" button again
- The previous temporary password will be overwritten

## Security Notes

1. **Admin Privileges Required**: Only users with admin privileges can access these features
2. **Audit Trail**: All password setup actions should be logged (consider adding audit logging)
3. **Password Strength**: All generated passwords meet strong security requirements
4. **One-Time Display**: Temporary passwords are never stored or logged in plain text
5. **Secure Transmission**: Always use HTTPS in production to protect password transmission

## Future Enhancements

Potential improvements for future versions:
- Email notification to users when password is setup/reset
- Temporary password expiration (force change on first login)
- Password history to prevent reuse
- Audit log of authentication method changes
- Bulk password reset functionality
