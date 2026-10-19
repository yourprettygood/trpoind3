namespace EduCabinet.Web.Models.Entities;

public enum QuestionType
{
    SingleChoice,
    MultipleChoice,
    ShortAnswer,
    DetailedAnswer,
    Matching
}

public enum AttemptStatus
{
    InProgress,
    Completed,
    TimedOut
}
