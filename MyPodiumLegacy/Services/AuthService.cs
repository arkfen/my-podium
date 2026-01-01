using Azure;
using Azure.Data.Tables;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using System.Security.Cryptography;

namespace MyPodium.Services;

public class AuthService
{
    private readonly IConfiguration _configuration;
    private readonly ProtectedLocalStorage _localStorage;
    private readonly IEmailService _emailService;
    private const string AUTH_STORAGE_KEY = "podium_auth";
    private const string USERS_TABLE = "MyPodiumUsers";
    private const string AUTH_SESSIONS_TABLE = "MyPodiumAuthSessions";
    private const string OTP_CODES_TABLE = "MyPodiumOTPCodes";

    public AuthService(
        IConfiguration configuration,
        ProtectedLocalStorage localStorage,
        IEmailService emailService)
    {
        _configuration = configuration;
        _localStorage = localStorage;
        _emailService = emailService;
    }

    private TableClient CreateTableClient(string tableName)
    {
        var storageUri = _configuration.GetConnectionString("DefaultStorageUri");
        var accountName = _configuration.GetConnectionString("DefaultAccountName");
        var storageAccountKey = _configuration.GetConnectionString("DefaultStorageAccountKey");

        if (string.IsNullOrEmpty(storageUri) || string.IsNullOrEmpty(accountName) || string.IsNullOrEmpty(storageAccountKey))
            throw new InvalidOperationException("Storage connection information is missing");

        return new TableClient(
            new Uri(storageUri),
            tableName,
            new TableSharedKeyCredential(accountName, storageAccountKey));
    }

    public async Task<bool> CheckAuthenticationAsync()
    {
        try
        {
            var result = await _localStorage.GetAsync<string>(AUTH_STORAGE_KEY);
            
            if (result.Success && !string.IsNullOrEmpty(result.Value))
            {
                var sessionId = result.Value;
                
                // Verify the session exists and is valid
                var tableClient = CreateTableClient(AUTH_SESSIONS_TABLE);
                try
                {
                    var sessionEntity = await tableClient.GetEntityAsync<TableEntity>("Sessions", sessionId);
                    
                    // Check if session expired
                    if (sessionEntity != null)
                    {
                        var expiryDate = sessionEntity.Value.GetDateTimeOffset("ExpiryDate");
                        if (expiryDate > DateTimeOffset.UtcNow)
                        {
                            return true;
                        }
                        else
                        {
                            // Session expired, remove it
                            await _localStorage.DeleteAsync(AUTH_STORAGE_KEY);
                            return false;
                        }
                    }
                }
                catch (RequestFailedException)
                {
                    // Session doesn't exist
                    await _localStorage.DeleteAsync(AUTH_STORAGE_KEY);
                    return false;
                }
            }
        }
        catch
        {
            // If there's an error, assume not authenticated
        }
        
        return false;
    }

    public async Task<(bool Success, string ErrorMessage)> SendVerificationCodeAsync(string email)
    {
        // Check if email exists in the users table
        var userTableClient = CreateTableClient(USERS_TABLE);
        var userQuery = userTableClient.Query<TableEntity>(e => e.GetString("Email") == email);
        
        TableEntity? userEntity = null;
        foreach (var entity in userQuery)
        {
            userEntity = entity;
            break;
        }

        if (userEntity == null)
        {
            return (false, "Email not found. Please check your email address.");
        }

        // Generate a random 4-digit OTP
        var verificationCode = GenerateOTP();
        
        // Store the OTP in the table
        var otpTableClient = CreateTableClient(OTP_CODES_TABLE);
        
        var otpEntity = new TableEntity("OTP", Guid.NewGuid().ToString())
        {
            ["Email"] = email,
            ["Code"] = verificationCode,
            ["UserId"] = userEntity.GetString("Id"),
            ["ExpiryTime"] = DateTimeOffset.UtcNow.AddMinutes(10),
            ["IsUsed"] = false
        };
        
        await otpTableClient.AddEntityAsync(otpEntity);
        
        // Send email with verification code
        await _emailService.SendVerificationEmailAsync(email, verificationCode);
        
        return (true, string.Empty);
    }
    
    private string GenerateOTP()
    {
        // Generate a random 4-digit code
        using var rng = RandomNumberGenerator.Create();
        var randomNumber = new byte[4];
        rng.GetBytes(randomNumber);
        var code = Math.Abs(BitConverter.ToInt32(randomNumber, 0) % 10000);
        return code.ToString("D4"); // Ensures 4 digits with leading zeros if needed
    }

    public async Task<(bool Success, string UserId, string UserName, string ErrorMessage)> VerifyOTPAsync(string email, string otpCode)
    {
        var otpTableClient = CreateTableClient(OTP_CODES_TABLE);
        var cutoffTime = DateTimeOffset.UtcNow.AddMinutes(-10);
        
        try
        {
            Console.WriteLine($"VerifyOTPAsync: Verifying OTP for Email='{email}', Code='{otpCode}'");
            
            var query = otpTableClient.QueryAsync<TableEntity>(
                filter: $"PartitionKey eq 'OTP' and Email eq '{email}' and IsUsed eq false and ExpiryTime gt datetime'{cutoffTime:yyyy-MM-ddTHH:mm:ss.fffffffZ}'");
            
            TableEntity? otpEntity = null;
            await foreach (var entityValue in query) 
            {
                var codeInDb = entityValue.GetString("Code");
                Console.WriteLine($"VerifyOTPAsync: Found OTP in DB: Code='{codeInDb}'. Comparing with provided: '{otpCode}'");
                if (string.Equals(codeInDb, otpCode, StringComparison.OrdinalIgnoreCase))
                {
                    otpEntity = entityValue;
                    break; // Found the matching OTP
                }
            }
            
            if (otpEntity == null)
            {
                Console.WriteLine("VerifyOTPAsync: No matching and valid OTP found in DB.");
                return (false, string.Empty, string.Empty, "Invalid or expired verification code.");
            }
            
            Console.WriteLine($"VerifyOTPAsync: OTP entity found. RowKey='{otpEntity.RowKey}'. Marking as used.");
            otpEntity["IsUsed"] = true;
            await otpTableClient.UpdateEntityAsync(otpEntity, ETag.All, TableUpdateMode.Merge);
            
            var userId = otpEntity.GetString("UserId");
            if (string.IsNullOrWhiteSpace(userId)) // Check for null, empty, or whitespace
            {
                Console.WriteLine($"VerifyOTPAsync: UserId is null, empty, or whitespace in the OTP entity. OTP RowKey: '{otpEntity.RowKey}'.");
                return (false, string.Empty, string.Empty, "Critical error: UserId missing or invalid in OTP record.");
            }
            // Check for characters forbidden in Azure Table Storage RowKeys
            if (userId.Any(c => c == '/' || c == '\\' || c == '#' || c == '?' || char.IsControl(c)))
            {
                Console.WriteLine($"VerifyOTPAsync: Invalid characters detected in UserId '{userId}' from OTP RowKey '{otpEntity.RowKey}'. Forbidden characters: / \\ # ? or control characters.");
                return (false, string.Empty, string.Empty, "Critical error: UserId contains invalid characters.");
            }
            Console.WriteLine($"VerifyOTPAsync: UserId='{userId}' retrieved from OTP. Preparing to fetch user details from table '{USERS_TABLE}'.");

            var userTableClient = CreateTableClient(USERS_TABLE);
            
            try
            {
                // First try to find the user by UsrId field if it exists in the database
                TableEntity? userEntity = null;
                
                // Method 1: Query by UsrId field
                Console.WriteLine($"VerifyOTPAsync: Attempting to query user with UsrId='{userId}'");
                var userQuery = userTableClient.Query<TableEntity>(entity => entity.GetString("UsrId") == userId);
                
                foreach (var entity in userQuery)
                {
                    userEntity = entity;
                    Console.WriteLine($"VerifyOTPAsync: User found by UsrId. PartitionKey='{userEntity.PartitionKey}', RowKey='{userEntity.RowKey}'");
                    break;
                }
                
                // Method 2: If not found with UsrId, try with split partition/row key approach
                if (userEntity == null && userId.Length >= 12)
                {
                    string partitionKey = userId.Substring(0, 6);
                    string rowKey = userId.Substring(6);
                    Console.WriteLine($"VerifyOTPAsync: User not found by UsrId, trying with PartitionKey='{partitionKey}' and RowKey='{rowKey}'");
                    
                    try
                    {
                        var userResponse = await userTableClient.GetEntityAsync<TableEntity>(partitionKey, rowKey);
                        userEntity = userResponse.Value;
                        Console.WriteLine($"VerifyOTPAsync: User found using split key approach");
                    }
                    catch (RequestFailedException rfe)
                    {
                        Console.WriteLine($"VerifyOTPAsync: Failed to get user with split key approach. Status: {rfe.Status}, Error: {rfe.ErrorCode}");
                    }
                }
                
                // If both approaches failed, try the original method as a fallback
                if (userEntity == null)
                {
                    Console.WriteLine($"VerifyOTPAsync: Falling back to original method - GetEntityAsync with PartitionKey='User' and RowKey='{userId}'");
                    try
                    {
                        var userEntityResponse = await userTableClient.GetEntityAsync<TableEntity>("User", userId);
                        userEntity = userEntityResponse.Value;
                    }
                    catch (RequestFailedException rfe)
                    {
                        if (rfe.Status == 404)
                        {
                            Console.WriteLine($"VerifyOTPAsync: User not found with fallback method. Status: 404");
                            return (false, string.Empty, string.Empty, $"User details not found for ID: {userId}.");
                        }
                        throw; // Let the outer catch handle other types of exceptions
                    }
                }
                
                if (userEntity == null)
                {
                    Console.WriteLine($"VerifyOTPAsync: User not found with any lookup method for UserId='{userId}'");
                    return (false, string.Empty, string.Empty, $"User details not found for ID: {userId}.");
                }
                
                Console.WriteLine($"VerifyOTPAsync: User entity retrieved. PartitionKey='{userEntity.PartitionKey}', RowKey='{userEntity.RowKey}'.");

                var userName = userEntity.GetString("Name");
                if (string.IsNullOrEmpty(userName))
                {
                    Console.WriteLine($"VerifyOTPAsync: UserName is null or empty for UserId='{userId}'. This might be acceptable depending on data integrity rules.");
                    userName = "User (Name not set)"; // Provide a default or handle as an error if name is mandatory
                }
                Console.WriteLine($"VerifyOTPAsync: UserName='{userName}' for UserId='{userId}'.");
                
                var sessionId = Guid.NewGuid().ToString();
                Console.WriteLine($"VerifyOTPAsync: Creating session. SessionId='{sessionId}'.");
                var sessionTableClient = CreateTableClient(AUTH_SESSIONS_TABLE);
                
                var sessionEntity = new TableEntity("Sessions", sessionId)
                {
                    ["UserId"] = userId,
                    ["Email"] = email, 
                    ["UserName"] = userName, 
                    ["CreatedDate"] = DateTimeOffset.UtcNow,
                    ["ExpiryDate"] = DateTimeOffset.UtcNow.AddDays(14), 
                    ["IsActive"] = true
                };
                
                await sessionTableClient.AddEntityAsync(sessionEntity);
                Console.WriteLine($"VerifyOTPAsync: Session entity for SessionId='{sessionId}' added to table '{AUTH_SESSIONS_TABLE}'.");
                
                await _localStorage.SetAsync(AUTH_STORAGE_KEY, sessionId);
                Console.WriteLine($"VerifyOTPAsync: SessionId='{sessionId}' stored in local storage. Verification successful.");
                
                return (true, userId, userName, string.Empty);
            }
            catch (RequestFailedException rfe) // Catch specific Azure SDK errors first
            {
                // Log the full exception details using ToString()
                Console.WriteLine($"VerifyOTPAsync: RequestFailedException while fetching user (UserId='{userId}'). Status: {rfe.Status}. ErrorCode: {rfe.ErrorCode}. Message: {rfe.Message}. Full Exception Details: {rfe.ToString()}");
                if (rfe.Status == 404)
                {
                    return (false, string.Empty, string.Empty, $"User not found (ID: {userId}). Please ensure the user ID is correct and exists.");
                }
                // For other RequestFailedExceptions, provide a generic error but log comprehensive details
                return (false, string.Empty, string.Empty, "A problem occurred while accessing your user data. Please try again shortly.");
            }
            catch (Exception ex) // General catch for other unexpected errors during this block
            {
                // Log the full exception details using ToString()
                Console.WriteLine($"VerifyOTPAsync: Unexpected error during user retrieval or session creation phase for UserId='{userId}'. Exception Type: {ex.GetType().FullName}. Full Exception Details: {ex.ToString()}");
                return (false, string.Empty, string.Empty, "An unexpected error occurred while finalizing your sign-in. Please try again.");
            }
        }
        catch (Exception ex) // Catch-all for the entire method
        {
            Console.WriteLine($"VerifyOTPAsync: General error during OTP verification process for Email='{email}'. Full Exception Details: {ex.ToString()}");
            return (false, string.Empty, string.Empty, "An unexpected error occurred while verifying your code. Please try again.");
        }
    }

    public async Task<(string UserId, string UserName)> GetUserInfoAsync()
    {
        try
        {
            var result = await _localStorage.GetAsync<string>(AUTH_STORAGE_KEY);
            
            if (result.Success && !string.IsNullOrEmpty(result.Value))
            {
                var sessionId = result.Value;
                Console.WriteLine($"GetUserInfoAsync: Found session ID in local storage: '{sessionId}'. Verifying session.");
                
                var tableClient = CreateTableClient(AUTH_SESSIONS_TABLE);
                try
                {
                    var sessionEntityResponse = await tableClient.GetEntityAsync<TableEntity>("Sessions", sessionId);
                    if (sessionEntityResponse != null && sessionEntityResponse.Value != null)
                    {
                        var sessionEntity = sessionEntityResponse.Value;
                        // Check if session is active and not expired
                        var isActive = sessionEntity.GetBoolean("IsActive");
                        var expiryDate = sessionEntity.GetDateTimeOffset("ExpiryDate");

                        if (isActive == true && expiryDate > DateTimeOffset.UtcNow)
                        {
                            var userId = sessionEntity.GetString("UserId");
                            var userName = sessionEntity.GetString("UserName"); // Prefer UserName from session if available and reliable

                            if (string.IsNullOrWhiteSpace(userId)) {
                                 Console.WriteLine($"GetUserInfoAsync: UserId is null or whitespace in active session '{sessionId}'. Invalidating session.");
                                 await _localStorage.DeleteAsync(AUTH_STORAGE_KEY); 
                                 return (string.Empty, string.Empty);
                            }

                            if (!string.IsNullOrWhiteSpace(userName))
                            {
                                 Console.WriteLine($"GetUserInfoAsync: UserId='{userId}', UserName='{userName}' retrieved directly from active session '{sessionId}'.");
                                 return (userId, userName);
                            }
                            
                            // Fallback: If UserName not in session (e.g., older session format) or needs refresh, fetch from Users table.
                            Console.WriteLine($"GetUserInfoAsync: UserName not in session or needs refresh for UserId='{userId}' (SessionId='{sessionId}'). Fetching from '{USERS_TABLE}'.");
                            var userTableClient = CreateTableClient(USERS_TABLE);
                            
                            TableEntity? userEntity = null;
                            userName = null;
                            
                            // Method 1: Query by UsrId field
                            Console.WriteLine($"GetUserInfoAsync: Attempting to query user with UsrId='{userId}'");
                            var userQuery = userTableClient.Query<TableEntity>(entity => entity.GetString("UsrId") == userId);
                            
                            foreach (var entity in userQuery)
                            {
                                userEntity = entity;
                                userName = userEntity.GetString("Name");
                                Console.WriteLine($"GetUserInfoAsync: User found by UsrId. PartitionKey='{userEntity.PartitionKey}', RowKey='{userEntity.RowKey}'");
                                break;
                            }
                            
                            // Method 2: If not found with UsrId, try with split partition/row key approach
                            if (userEntity == null && userId.Length >= 12)
                            {
                                string partitionKey = userId.Substring(0, 6);
                                string rowKey = userId.Substring(6);
                                Console.WriteLine($"GetUserInfoAsync: User not found by UsrId, trying with PartitionKey='{partitionKey}' and RowKey='{rowKey}'");
                                
                                try
                                {
                                    var userResponse = await userTableClient.GetEntityAsync<TableEntity>(partitionKey, rowKey);
                                    userEntity = userResponse.Value;
                                    userName = userEntity.GetString("Name");
                                    Console.WriteLine($"GetUserInfoAsync: User found using split key approach");
                                }
                                catch (RequestFailedException rfe)
                                {
                                    Console.WriteLine($"GetUserInfoAsync: Failed to get user with split key approach. Status: {rfe.Status}, Error: {rfe.ErrorCode}");
                                }
                            }
                            
                            // Method 3: Fallback to original method
                            if (userEntity == null)
                            {
                                Console.WriteLine($"GetUserInfoAsync: Falling back to original method - GetEntityAsync with PartitionKey='User' and RowKey='{userId}'");
                                try 
                                {
                                    var userEntityResponse = await userTableClient.GetEntityAsync<TableEntity>("User", userId);
                                    if (userEntityResponse != null && userEntityResponse.Value != null)
                                    {
                                        userEntity = userEntityResponse.Value;
                                        userName = userEntity.GetString("Name");
                                    }
                                }
                                catch (RequestFailedException rfe)
                                {
                                    Console.WriteLine($"GetUserInfoAsync: Failed to get user with original method. Status: {rfe.Status}, Error: {rfe.ErrorCode}");
                                }
                            }
                            
                            if (userEntity != null)
                            {
                                Console.WriteLine($"GetUserInfoAsync: Fetched UserName='{userName}' from '{USERS_TABLE}' for UserId='{userId}'.");
                                return (userId, userName ?? string.Empty); // Return empty if name is null in DB
                            }
                            else
                            {
                                Console.WriteLine($"GetUserInfoAsync: User not found in '{USERS_TABLE}' with any lookup method for UserId='{userId}' (from session '{sessionId}'). Invalidating session.");
                                await _localStorage.DeleteAsync(AUTH_STORAGE_KEY); 
                                return (string.Empty, string.Empty);
                            }
                        }
                        else
                        {
                             Console.WriteLine($"GetUserInfoAsync: Session '{sessionId}' is inactive or expired. IsActive: {isActive}, ExpiryDate: {expiryDate}. Removing from local storage.");
                             await _localStorage.DeleteAsync(AUTH_STORAGE_KEY);
                        }
                    }
                    else
                    {
                        Console.WriteLine($"GetUserInfoAsync: Session '{sessionId}' not found in table '{AUTH_SESSIONS_TABLE}' or its Value was null. Removing from local storage.");
                        await _localStorage.DeleteAsync(AUTH_STORAGE_KEY); 
                    }
                }
                catch (RequestFailedException rfe)
                {
                    Console.WriteLine($"GetUserInfoAsync: RequestFailedException for session '{sessionId}'. Status: {rfe.Status}. ErrorCode: {rfe.ErrorCode}. Message: {rfe.Message}. Full Exception: {rfe.ToString()}");
                    if (rfe.Status == 404) 
                    {
                        Console.WriteLine($"GetUserInfoAsync: Session '{sessionId}' not found in table (404). Removing from local storage.");
                        await _localStorage.DeleteAsync(AUTH_STORAGE_KEY); 
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"GetUserInfoAsync: Error retrieving session '{sessionId}' or user info. Full Exception: {ex.ToString()}");
                }
            }
            else
            {
                Console.WriteLine("GetUserInfoAsync: No session ID found in local storage or GetAsync was not successful.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"GetUserInfoAsync: General error in GetUserInfoAsync. Full Exception: {ex.ToString()}");
        }
        
        return (string.Empty, string.Empty);
    }

    public async Task SignOutAsync()
    {
        try
        {
            var result = await _localStorage.GetAsync<string>(AUTH_STORAGE_KEY);
            
            if (result.Success && !string.IsNullOrEmpty(result.Value))
            {
                var sessionId = result.Value;
                
                // Mark the session as inactive
                var tableClient = CreateTableClient(AUTH_SESSIONS_TABLE);
                try
                {
                    var sessionEntity = await tableClient.GetEntityAsync<TableEntity>("Sessions", sessionId);
                    if (sessionEntity != null)
                    {
                        sessionEntity.Value["IsActive"] = false;
                        await tableClient.UpdateEntityAsync(sessionEntity.Value, ETag.All);
                    }
                }
                catch
                {
                    // Session doesn't exist, that's fine
                }
            }
            
            // Remove from local storage
            await _localStorage.DeleteAsync(AUTH_STORAGE_KEY);
        }
        catch
        {
            // Ignore any errors during sign out
        }
    }
}