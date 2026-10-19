namespace EduCabinet.Web.Models.Entities;

public class LectureProgress
{
    public int Id { get; set; }
    public string StudentId { get; set; } = string.Empty;
    public ApplicationUser Student { get; set; } = null!;

    public int LectureId { get; set; }
    public Lecture Lecture { get; set; } = null!;

    public bool IsTheoryRead { get; set; }
    public bool AreQuestionsAnswered { get; set; }
    public bool AreQuestionsPassed { get; set; }
    public bool IsTestPassed { get; set; }
    public bool IsCompleted { get; set; }
    public DateTime? CompletedAt { get; set; }

    // Manual teacher override with full audit tracking
    public bool IsUnlockedManually { get; set; }
    public string? UnlockedByTeacherId { get; set; }
    public ApplicationUser? UnlockedByTeacher { get; set; }
    public string? UnlockReason { get; set; }
    public DateTime? UnlockedAt { get; set; }
}

public class AuditLog
{
    public int Id { get; set; }
    public string Action { get; set; } = string.Empty; // e.g., "ManualLectureUnlock", "RoleChanged"
    public string EntityName { get; set; } = string.Empty;
    public string? EntityId { get; set; }
    public string? UserId { get; set; }
    public string? UserName { get; set; }
    public string? Details { get; set; }
    public string? IpAddress { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
