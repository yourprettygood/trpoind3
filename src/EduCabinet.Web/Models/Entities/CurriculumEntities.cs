namespace EduCabinet.Web.Models.Entities;

public class Discipline
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string TeacherId { get; set; } = string.Empty;
    public ApplicationUser Teacher { get; set; } = null!;
    public bool IsPublished { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Topic> Topics { get; set; } = new List<Topic>();
}

public class Topic
{
    public int Id { get; set; }
    public int DisciplineId { get; set; }
    public Discipline Discipline { get; set; } = null!;

    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int OrderIndex { get; set; } = 1;

    public ICollection<Lecture> Lectures { get; set; } = new List<Lecture>();
}

public class Lecture
{
    public int Id { get; set; }
    public int TopicId { get; set; }
    public Topic Topic { get; set; } = null!;

    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int OrderIndex { get; set; } = 1;

    // Optional rich theoretical text directly in app
    public string? ContentHtml { get; set; }

    // Uploaded material file (PDF / DOCX)
    public string? ContentFilePath { get; set; }
    public string? ContentFileName { get; set; }
    public string? ContentType { get; set; }
    public long? FileSizeBytes { get; set; }

    // Conditions to unlock subsequent lecture
    public bool RequiresTestToUnlockNext { get; set; } = true;
    public int PassingScorePercent { get; set; } = 70;
    public bool RequiresQuestionsCheck { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<ControlQuestion> ControlQuestions { get; set; } = new List<ControlQuestion>();
    public ICollection<Test> Tests { get; set; } = new List<Test>();
    public ICollection<LectureProgress> ProgressRecords { get; set; } = new List<LectureProgress>();
}
