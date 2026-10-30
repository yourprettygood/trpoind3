using EduCabinet.Web.Models.Entities;
using System.ComponentModel.DataAnnotations;

namespace EduCabinet.Web.ViewModels;

public class TeacherDashboardViewModel
{
    public List<Discipline> Disciplines { get; set; } = new();
    public int TotalStudentsCount { get; set; }
    public int PendingReviewsCount { get; set; }
    public int RecentAttemptsCount { get; set; }
}

public class GroupJournalViewModel
{
    public Discipline Discipline { get; set; } = null!;
    public List<StudentGroup> AvailableGroups { get; set; } = new();
    public int? SelectedGroupId { get; set; }
    public StudentGroup? SelectedGroup { get; set; }
    public List<Lecture> OrderedLectures { get; set; } = new();
    public List<StudentJournalRow> StudentRows { get; set; } = new();
}

public class StudentJournalRow
{
    public ApplicationUser Student { get; set; } = null!;
    public Dictionary<int, StudentLectureCell> LectureCells { get; set; } = new(); // LectureId -> Cell
    public int CompletedLecturesCount { get; set; }
    public int OverallPercent { get; set; }
}

public class StudentLectureCell
{
    public bool IsUnlocked { get; set; } = true;
    public bool IsUnlockedManually { get; set; }
    public bool IsCompleted { get; set; }
    public bool IsTheoryRead { get; set; }
    public int? BestTestScore { get; set; }
    public bool? TestPassed { get; set; }
    public int AttemptsCount { get; set; }
    public bool HasPendingQuestions { get; set; }

    // Оценка за лекцию и дедлайн
    public int? FinalGrade { get; set; }
    public bool IsOverdue { get; set; }
    public string? TeacherGradeComment { get; set; }
    public DateTime? Deadline { get; set; }
}

public class GradeSubmissionViewModel
{
    public int AnswerId { get; set; }
    public string StudentName { get; set; } = string.Empty;
    public string DisciplineTitle { get; set; } = string.Empty;
    public string LectureTitle { get; set; } = string.Empty;
    public string QuestionText { get; set; } = string.Empty;
    public int MaxScore { get; set; }

    // Client original answer (immutable)
    public string StudentAnswerText { get; set; } = string.Empty;
    public DateTime SubmittedAt { get; set; }

    // Staff resolution inputs
    [Required]
    [Range(0, 100)]
    public int TeacherScore { get; set; }

    public string? TeacherFeedback { get; set; }
}

public class CreateLectureViewModel
{
    public int TopicId { get; set; }
    public string TopicTitle { get; set; } = string.Empty;
    public int DisciplineId { get; set; }

    [Required(ErrorMessage = "Введите название лекции")]
    [Display(Name = "Название лекции")]
    public string Title { get; set; } = string.Empty;

    [Display(Name = "Краткое описание")]
    public string Description { get; set; } = string.Empty;

    [Display(Name = "Порядковый номер")]
    public int OrderIndex { get; set; } = 1;

    [Display(Name = "Теоретический текст (HTML)")]
    public string? ContentHtml { get; set; }

    [Display(Name = "Файл материала (PDF или DOCX)")]
    public IFormFile? MaterialFile { get; set; }

    [Display(Name = "Обязательное тестирование для открытия следующей лекции")]
    public bool RequiresTestToUnlockNext { get; set; } = true;

    [Display(Name = "Проходной балл тестирования (%)")]
    [Range(1, 100)]
    public int PassingScorePercent { get; set; } = 70;

    [Display(Name = "Требовать проверку контрольных вопросов")]
    public bool RequiresQuestionsCheck { get; set; } = true;
}

public class ManualUnlockViewModel
{
    [Required]
    public string StudentId { get; set; } = string.Empty;

    [Required]
    public int LectureId { get; set; }

    [Required(ErrorMessage = "Укажите причину досрочного допуска")]
    public string Reason { get; set; } = string.Empty;
}

public class CreateTestQuestionViewModel
{
    public int LectureId { get; set; }
    public string LectureTitle { get; set; } = string.Empty;
    public int DisciplineId { get; set; }
    public string DisciplineTitle { get; set; } = string.Empty;

    [Required(ErrorMessage = "Введите формулировку вопроса теста")]
    [Display(Name = "Текст вопроса")]
    public string QuestionText { get; set; } = string.Empty;

    [Display(Name = "Тип вопроса")]
    public QuestionType QuestionType { get; set; } = QuestionType.SingleChoice;

    [Display(Name = "Баллы за правильный ответ")]
    [Range(1, 100, ErrorMessage = "Количество баллов должно быть от 1 до 100")]
    public int Points { get; set; } = 1;

    [Display(Name = "Порядковый номер")]
    [Range(1, 1000)]
    public int OrderIndex { get; set; } = 1;

    public List<AnswerOptionItemViewModel> Options { get; set; } = new();
}

public class AnswerOptionItemViewModel
{
    public string OptionText { get; set; } = string.Empty;
    public bool IsCorrect { get; set; }
    public int OrderIndex { get; set; } = 1;
}

public class UpdateTestSettingsViewModel
{
    [Required]
    public int TestId { get; set; }

    [Required]
    public int LectureId { get; set; }

    [Required(ErrorMessage = "Введите название теста")]
    [Display(Name = "Название теста")]
    public string Title { get; set; } = string.Empty;

    [Display(Name = "Проходной балл (%)")]
    [Range(1, 100, ErrorMessage = "Проходной балл должен быть от 1 до 100%")]
    public int PassingScorePercent { get; set; } = 70;

    [Display(Name = "Количество попыток")]
    [Range(1, 100, ErrorMessage = "Количество попыток должно быть от 1 до 100")]
    public int MaxAttempts { get; set; } = 3;

    [Display(Name = "Лимит времени (в минутах)")]
    [Range(1, 300, ErrorMessage = "Лимит времени должен быть от 1 до 300 минут")]
    public int? TimeLimitMinutes { get; set; }
}
