namespace EduCabinet.Web.Models.Entities;

public class ControlQuestion
{
    public int Id { get; set; }
    public int LectureId { get; set; }
    public Lecture Lecture { get; set; } = null!;

    public string QuestionText { get; set; } = string.Empty;
    public QuestionType QuestionType { get; set; } = QuestionType.ShortAnswer;
    
    // JSON-serialized array of options if SingleChoice / MultipleChoice
    public string? OptionsJson { get; set; }
    
    // For auto-evaluated questions (short answer / choice index)
    public string? CorrectAnswer { get; set; }
    
    public int MaxScore { get; set; } = 5;
    public int OrderIndex { get; set; } = 1;

    public ICollection<StudentAnswer> StudentAnswers { get; set; } = new List<StudentAnswer>();
}

/// <summary>
/// Strictly complies with GEMINI.md Rule 1:
/// Client / Student data (StudentAnswerText, SubmittedAt) is isolated from
/// Staff / Teacher resolution data (TeacherScore, TeacherFeedback, IsGraded, GradedById, GradedAt).
/// Saving teacher review NEVER overwrites or alters student's original input.
/// </summary>
public class StudentAnswer
{
    public int Id { get; set; }
    public int QuestionId { get; set; }
    public ControlQuestion Question { get; set; } = null!;

    public string StudentId { get; set; } = string.Empty;
    public ApplicationUser Student { get; set; } = null!;

    // === Client / Student Input Data ===
    public string StudentAnswerText { get; set; } = string.Empty;
    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;

    // === Staff / Teacher Resolution Data ===
    public bool IsGraded { get; set; } = false;
    public int? TeacherScore { get; set; }
    public string? TeacherFeedback { get; set; }
    public string? GradedById { get; set; }
    public ApplicationUser? GradedBy { get; set; }
    public DateTime? GradedAt { get; set; }
}

public class Test
{
    public int Id { get; set; }
    public int LectureId { get; set; }
    public Lecture Lecture { get; set; } = null!;

    public string Title { get; set; } = string.Empty;
    public int PassingScorePercent { get; set; } = 70;
    public int MaxAttempts { get; set; } = 3;
    public int? TimeLimitMinutes { get; set; }
    public bool ShuffleQuestions { get; set; } = true;
    public bool ShuffleOptions { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<TestQuestion> Questions { get; set; } = new List<TestQuestion>();
    public ICollection<TestAttempt> Attempts { get; set; } = new List<TestAttempt>();
}

public class TestQuestion
{
    public int Id { get; set; }
    public int TestId { get; set; }
    public Test Test { get; set; } = null!;

    public string QuestionText { get; set; } = string.Empty;
    public QuestionType QuestionType { get; set; } = QuestionType.SingleChoice;
    public int Points { get; set; } = 1;
    public int OrderIndex { get; set; } = 1;

    public ICollection<AnswerOption> Options { get; set; } = new List<AnswerOption>();
}

public class AnswerOption
{
    public int Id { get; set; }
    public int QuestionId { get; set; }
    public TestQuestion Question { get; set; } = null!;

    public string OptionText { get; set; } = string.Empty;
    public bool IsCorrect { get; set; }
    public int OrderIndex { get; set; } = 1;
}

public class TestAttempt
{
    public int Id { get; set; }
    public int TestId { get; set; }
    public Test Test { get; set; } = null!;

    public string StudentId { get; set; } = string.Empty;
    public ApplicationUser Student { get; set; } = null!;

    public int AttemptNumber { get; set; } = 1;
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }

    public int TotalPoints { get; set; }
    public int MaxPoints { get; set; }
    public int ScorePercent { get; set; }
    public bool IsPassed { get; set; }
    public AttemptStatus Status { get; set; } = AttemptStatus.InProgress;

    public ICollection<StudentTestAnswer> StudentAnswers { get; set; } = new List<StudentTestAnswer>();
}

public class StudentTestAnswer
{
    public int Id { get; set; }
    public int AttemptId { get; set; }
    public TestAttempt Attempt { get; set; } = null!;

    public int QuestionId { get; set; }
    public TestQuestion Question { get; set; } = null!;

    public string? SelectedOptionIdsJson { get; set; } // JSON array of int IDs
    public string? TextAnswer { get; set; }
    public bool IsCorrect { get; set; }
    public int PointsAwarded { get; set; }
}
