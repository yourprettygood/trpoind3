namespace EduCabinet.Web.Services;

public class LectureAccessResult
{
    public bool IsAllowed { get; set; }
    public string? Reason { get; set; }
    public int? BlockingLectureId { get; set; }
    public string? BlockingLectureTitle { get; set; }

    public static LectureAccessResult Allowed() => new() { IsAllowed = true };
    public static LectureAccessResult Denied(string reason, int? blockingId = null, string? blockingTitle = null) =>
        new() { IsAllowed = false, Reason = reason, BlockingLectureId = blockingId, BlockingLectureTitle = blockingTitle };
}

public interface ILectureAccessService
{
    Task<LectureAccessResult> CanAccessLectureAsync(string studentId, int lectureId);
    Task MarkTheoryReadAsync(string studentId, int lectureId);
    Task<bool> UnlockLectureManuallyAsync(string studentId, int lectureId, string teacherId, string reason);
    Task UpdateLectureCompletionStatusAsync(string studentId, int lectureId);
}
