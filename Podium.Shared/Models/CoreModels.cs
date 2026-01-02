namespace Podium.Shared.Models;

public class Sport
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTimeOffset CreatedDate { get; set; }
}

public class Tier
{
    public string Id { get; set; } = string.Empty;
    public string SportId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string ShortName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; }
    public DateTimeOffset CreatedDate { get; set; }
}

public class Season
{
    public string Year { get; set; } = string.Empty;
    public string TierId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public DateTimeOffset StartDate { get; set; }
    public DateTimeOffset EndDate { get; set; }
    public bool IsActive { get; set; }
    public DateTimeOffset CreatedDate { get; set; }
}

public class Event
{
    public string Id { get; set; } = string.Empty;
    public string TierId { get; set; } = string.Empty;
    public string SeasonYear { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public DateTimeOffset EventDate { get; set; }
    public DateTimeOffset PredictionCutoffDate { get; set; }
    public int Round { get; set; }
    public bool IsCompleted { get; set; }
    public bool IsActive { get; set; }
    public DateTimeOffset CreatedDate { get; set; }
    
    public bool IsPredictionOpen => DateTimeOffset.UtcNow < PredictionCutoffDate && IsActive && !IsCompleted;
}

public class Competitor
{
    public string Id { get; set; } = string.Empty;
    public string SportId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string ShortName { get; set; } = string.Empty;
    public string Number { get; set; } = string.Empty;
    public string Team { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTimeOffset CreatedDate { get; set; }
}

public class SeasonParticipant
{
    public string CompetitorId { get; set; } = string.Empty;
    public string TierId { get; set; } = string.Empty;
    public string SeasonYear { get; set; } = string.Empty;
    public string CompetitorName { get; set; } = string.Empty;
    public string Team { get; set; } = string.Empty;
    public string Number { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTimeOffset JoinedDate { get; set; }
}

public class EventResult
{
    public string EventId { get; set; } = string.Empty;
    public int Position { get; set; } // 1, 2, 3
    public string CompetitorId { get; set; } = string.Empty;
    public string CompetitorName { get; set; } = string.Empty;
    public DateTimeOffset RecordedDate { get; set; }
}

public class UserPrediction
{
    public string UserId { get; set; } = string.Empty;
    public string EventId { get; set; } = string.Empty;
    public int Position { get; set; } // 1, 2, 3
    public string CompetitorId { get; set; } = string.Empty;
    public string CompetitorName { get; set; } = string.Empty;
    public DateTimeOffset PredictedDate { get; set; }
    public int PointsAwarded { get; set; }
}

public class PointsConfiguration
{
    public string TierId { get; set; } = string.Empty;
    public string SeasonYear { get; set; } = string.Empty;
    public int ExactPositionPoints { get; set; }
    public int OneOffPoints { get; set; }
    public int TwoOffPoints { get; set; }
    public int InPodiumPoints { get; set; }
    public DateTimeOffset CreatedDate { get; set; }
}

public class User
{
    public string Id { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public bool IsEmailVerified { get; set; }
    public DateTimeOffset CreatedDate { get; set; }
    public DateTimeOffset LastLoginDate { get; set; }
}

public class AuthSession
{
    public string SessionId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public DateTimeOffset CreatedDate { get; set; }
    public DateTimeOffset ExpiryDate { get; set; }
    public bool IsActive { get; set; }
    public DateTimeOffset LastActivityDate { get; set; }
}

public class OTPCode
{
    public string Id { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public DateTimeOffset ExpiryTime { get; set; }
    public bool IsUsed { get; set; }
    public DateTimeOffset CreatedDate { get; set; }
}
