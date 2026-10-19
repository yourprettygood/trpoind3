using EduCabinet.Web.Models.Entities;

namespace EduCabinet.Web.ViewModels;

public class StudentDisciplineCardViewModel
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string TeacherName { get; set; } = string.Empty;
    public int TotalLectures { get; set; }
    public int CompletedLectures { get; set; }
    public int ProgressPercent => TotalLectures > 0 ? (int)Math.Round((double)CompletedLectures / TotalLectures * 100) : 0;
}

public class StudentDisciplineDetailsViewModel
{
    public Discipline Discipline { get; set; } = null!;
    public List<TopicWithLecturesViewModel> Topics { get; set; } = new();
    public int OverallProgressPercent { get; set; }
}

public class TopicWithLecturesViewModel
{
    public Topic Topic { get; set; } = null!;
    public List<StudentLectureStatusItem> Lectures { get; set; } = new();
}

public class StudentLectureStatusItem
{
    public Lecture Lecture { get; set; } = null!;
    public bool IsUnlocked { get; set; }
    public string? LockReason { get; set; }
    public bool IsCompleted { get; set; }
    public bool IsTheoryRead { get; set; }
    public bool AreQuestionsAnswered { get; set; }
    public bool IsTestPassed { get; set; }
    public int? BestTestScore { get; set; }
    public int AttemptsUsed { get; set; }

    // Оценка за задание и дедлайн
    public int? FinalGrade { get; set; }
    public bool IsOverdue { get; set; }
    public string? TeacherGradeComment { get; set; }
}

public class StudentLectureViewViewModel
{
    public Lecture Lecture { get; set; } = null!;
    public LectureProgress? Progress { get; set; }
    public bool IsUnlocked { get; set; }
    public string? LockReason { get; set; }
    public List<ControlQuestion> Questions { get; set; } = new();
    public List<StudentAnswer> ExistingAnswers { get; set; } = new();
    public Test? Test { get; set; }
    public List<TestAttempt> TestAttempts { get; set; } = new();
}

public class StudentGradeBookViewModel
{
    public ApplicationUser Student { get; set; } = null!;
    public StudentGroup? Group { get; set; }
    public List<DisciplineGradeSummary> Disciplines { get; set; } = new();
}

public class DisciplineGradeSummary
{
    public string DisciplineTitle { get; set; } = string.Empty;
    public string TeacherName { get; set; } = string.Empty;
    public int TotalLectures { get; set; }
    public int CompletedLectures { get; set; }
    public int ProgressPercent { get; set; }
    public List<LectureGradeItem> LectureGrades { get; set; } = new();
}

public class LectureGradeItem
{
    public string LectureTitle { get; set; } = string.Empty;
    public bool IsCompleted { get; set; }
    public int? BestTestScore { get; set; }
    public bool? TestPassed { get; set; }
    public int AttemptsUsed { get; set; }
    public int QuestionsScore { get; set; }
    public int MaxQuestionsScore { get; set; }
    public int? FinalGrade { get; set; }
    public bool IsOverdue { get; set; }
}
