using EduCabinet.Web.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EduCabinet.Web.Controllers;

[Authorize(Roles = "Teacher,Administrator")]
public class DatabaseExplorerController : Controller
{
    private readonly ApplicationDbContext _context;

    public DatabaseExplorerController(ApplicationDbContext context)
    {
        _context = context;
    }

    public class TableMetaDto
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int RowCount { get; set; }
        public List<string> Columns { get; set; } = new();
        public List<string> ForeignKeys { get; set; } = new();
    }

    private static readonly Dictionary<string, string> TableDescriptions = new()
    {
        ["Users"] = "Учётные записи пользователей (Студенты, Преподаватели, Администраторы)",
        ["StudentGroups"] = "Справочник учебных групп студентов (ИС-31, курс и специальность)",
        ["GroupMemberships"] = "Связующая таблица привязки студентов к учебным группам",
        ["Disciplines"] = "Учебные курсы и дисциплины, созданные преподавателями",
        ["Topics"] = "Темы внутри дисциплин с порядковыми номерами изучения",
        ["Lectures"] = "Лекции с прикрепленными PDF/DOCX, порогами тестов и условиями открытия",
        ["ControlQuestions"] = "Контрольные вопросы к лекциям (краткие, с выбором, развёрнутые)",
        ["StudentAnswers"] = "Ответы студентов на контрольные вопросы (изолированы от рецензии)",
        ["Tests"] = "Контрольные тесты к лекциям с проходным баллом и числом попыток",
        ["TestQuestions"] = "Вопросы тестирования с указанием баллов",
        ["AnswerOptions"] = "Варианты ответов на вопросы теста с флагом правильности",
        ["TestAttempts"] = "Попытки тестирования студентов: баллы, процент, статус прохождения",
        ["StudentTestAnswers"] = "Детальные ответы студента по каждому вопросу в попытке теста",
        ["LectureProgresses"] = "Прогресс студента по лекции: теория, вопросы, тест, ручной допуск",
        ["AuditLogs"] = "Журнал аудита безопасности: фиксация ручных разблокировок и изменений"
    };

    [HttpGet]
    public async Task<IActionResult> Index(string? selectedTable = null)
    {
        var tables = new List<TableMetaDto>
        {
            new() { Name = "Disciplines", Description = TableDescriptions["Disciplines"], RowCount = await _context.Disciplines.CountAsync() },
            new() { Name = "Topics", Description = TableDescriptions["Topics"], RowCount = await _context.Topics.CountAsync() },
            new() { Name = "Lectures", Description = TableDescriptions["Lectures"], RowCount = await _context.Lectures.CountAsync() },
            new() { Name = "ControlQuestions", Description = TableDescriptions["ControlQuestions"], RowCount = await _context.ControlQuestions.CountAsync() },
            new() { Name = "StudentAnswers", Description = TableDescriptions["StudentAnswers"], RowCount = await _context.StudentAnswers.CountAsync() },
            new() { Name = "Tests", Description = TableDescriptions["Tests"], RowCount = await _context.Tests.CountAsync() },
            new() { Name = "TestQuestions", Description = TableDescriptions["TestQuestions"], RowCount = await _context.TestQuestions.CountAsync() },
            new() { Name = "AnswerOptions", Description = TableDescriptions["AnswerOptions"], RowCount = await _context.AnswerOptions.CountAsync() },
            new() { Name = "TestAttempts", Description = TableDescriptions["TestAttempts"], RowCount = await _context.TestAttempts.CountAsync() },
            new() { Name = "StudentTestAnswers", Description = TableDescriptions["StudentTestAnswers"], RowCount = await _context.StudentTestAnswers.CountAsync() },
            new() { Name = "LectureProgresses", Description = TableDescriptions["LectureProgresses"], RowCount = await _context.LectureProgresses.CountAsync() },
            new() { Name = "StudentGroups", Description = TableDescriptions["StudentGroups"], RowCount = await _context.StudentGroups.CountAsync() },
            new() { Name = "GroupMemberships", Description = TableDescriptions["GroupMemberships"], RowCount = await _context.GroupMemberships.CountAsync() },
            new() { Name = "Users", Description = TableDescriptions["Users"], RowCount = await _context.Users.CountAsync() },
            new() { Name = "AuditLogs", Description = TableDescriptions["AuditLogs"], RowCount = await _context.AuditLogs.CountAsync() }
        };

        ViewBag.SelectedTable = string.IsNullOrEmpty(selectedTable) ? "Lectures" : selectedTable;
        return View(tables);
    }

    [HttpGet]
    public async Task<IActionResult> GetTableData(string tableName)
    {
        tableName = tableName?.Trim() ?? string.Empty;

        return tableName.ToLowerInvariant() switch
        {
            "disciplines" => Json(new { columns = new[] { "Id", "Title", "Description", "TeacherId", "IsPublished", "CreatedAt" }, rows = await _context.Disciplines.Select(d => new { d.Id, d.Title, d.Description, d.TeacherId, d.IsPublished, CreatedAt = d.CreatedAt.ToString("yyyy-MM-dd HH:mm") }).ToListAsync() }),
            "topics" => Json(new { columns = new[] { "Id", "DisciplineId", "Title", "OrderIndex", "Description" }, rows = await _context.Topics.Select(t => new { t.Id, t.DisciplineId, t.Title, t.OrderIndex, t.Description }).ToListAsync() }),
            "lectures" => Json(new { columns = new[] { "Id", "TopicId", "Title", "OrderIndex", "PassingScorePercent", "RequiresTestToUnlockNext", "ContentFileName", "CreatedAt" }, rows = await _context.Lectures.Select(l => new { l.Id, l.TopicId, l.Title, l.OrderIndex, l.PassingScorePercent, l.RequiresTestToUnlockNext, l.ContentFileName, CreatedAt = l.CreatedAt.ToString("yyyy-MM-dd HH:mm") }).ToListAsync() }),
            "controlquestions" => Json(new { columns = new[] { "Id", "LectureId", "QuestionText", "QuestionType", "MaxScore", "CorrectAnswer" }, rows = await _context.ControlQuestions.Select(q => new { q.Id, q.LectureId, q.QuestionText, QuestionType = q.QuestionType.ToString(), q.MaxScore, q.CorrectAnswer }).ToListAsync() }),
            "studentanswers" => Json(new { columns = new[] { "Id", "QuestionId", "StudentId", "StudentAnswerText", "SubmittedAt", "IsGraded", "TeacherScore", "TeacherFeedback" }, rows = await _context.StudentAnswers.Select(a => new { a.Id, a.QuestionId, a.StudentId, a.StudentAnswerText, SubmittedAt = a.SubmittedAt.ToString("yyyy-MM-dd HH:mm"), a.IsGraded, a.TeacherScore, a.TeacherFeedback }).ToListAsync() }),
            "tests" => Json(new { columns = new[] { "Id", "LectureId", "Title", "PassingScorePercent", "MaxAttempts", "TimeLimitMinutes" }, rows = await _context.Tests.Select(t => new { t.Id, t.LectureId, t.Title, t.PassingScorePercent, t.MaxAttempts, t.TimeLimitMinutes }).ToListAsync() }),
            "testquestions" => Json(new { columns = new[] { "Id", "TestId", "QuestionText", "QuestionType", "Points" }, rows = await _context.TestQuestions.Select(q => new { q.Id, q.TestId, q.QuestionText, QuestionType = q.QuestionType.ToString(), q.Points }).ToListAsync() }),
            "answeroptions" => Json(new { columns = new[] { "Id", "QuestionId", "OptionText", "IsCorrect", "OrderIndex" }, rows = await _context.AnswerOptions.Select(o => new { o.Id, o.QuestionId, o.OptionText, o.IsCorrect, o.OrderIndex }).ToListAsync() }),
            "testattempts" => Json(new { columns = new[] { "Id", "TestId", "StudentId", "AttemptNumber", "TotalPoints", "MaxPoints", "ScorePercent", "IsPassed", "Status", "StartedAt" }, rows = await _context.TestAttempts.Select(a => new { a.Id, a.TestId, a.StudentId, a.AttemptNumber, a.TotalPoints, a.MaxPoints, a.ScorePercent, a.IsPassed, Status = a.Status.ToString(), StartedAt = a.StartedAt.ToString("yyyy-MM-dd HH:mm") }).ToListAsync() }),
            "studenttestanswers" => Json(new { columns = new[] { "Id", "AttemptId", "QuestionId", "TextAnswer", "IsCorrect", "PointsAwarded" }, rows = await _context.StudentTestAnswers.Select(sta => new { sta.Id, sta.AttemptId, sta.QuestionId, sta.TextAnswer, sta.IsCorrect, sta.PointsAwarded }).ToListAsync() }),
            "lectureprogresses" => Json(new { columns = new[] { "Id", "StudentId", "LectureId", "IsTheoryRead", "AreQuestionsAnswered", "IsTestPassed", "IsCompleted", "IsUnlockedManually", "UnlockReason" }, rows = await _context.LectureProgresses.Select(p => new { p.Id, p.StudentId, p.LectureId, p.IsTheoryRead, p.AreQuestionsAnswered, p.IsTestPassed, p.IsCompleted, p.IsUnlockedManually, p.UnlockReason }).ToListAsync() }),
            "studentgroups" => Json(new { columns = new[] { "Id", "Name", "CourseNumber", "Description", "CreatedAt" }, rows = await _context.StudentGroups.Select(g => new { g.Id, g.Name, g.CourseNumber, g.Description, CreatedAt = g.CreatedAt.ToString("yyyy-MM-dd HH:mm") }).ToListAsync() }),
            "groupmemberships" => Json(new { columns = new[] { "Id", "StudentId", "GroupId", "EnrolledAt" }, rows = await _context.GroupMemberships.Select(gm => new { gm.Id, gm.StudentId, gm.GroupId, EnrolledAt = gm.EnrolledAt.ToString("yyyy-MM-dd HH:mm") }).ToListAsync() }),
            "users" => Json(new { columns = new[] { "Id", "UserName", "Email", "FullName", "UserRole", "CreatedAt" }, rows = await _context.Users.Select(u => new { u.Id, u.UserName, u.Email, u.FullName, u.UserRole, CreatedAt = u.CreatedAt.ToString("yyyy-MM-dd HH:mm") }).ToListAsync() }),
            "auditlogs" => Json(new { columns = new[] { "Id", "Action", "EntityName", "EntityId", "UserName", "Details", "Timestamp" }, rows = await _context.AuditLogs.OrderByDescending(al => al.Timestamp).Select(al => new { al.Id, al.Action, al.EntityName, al.EntityId, al.UserName, al.Details, Timestamp = al.Timestamp.ToString("yyyy-MM-dd HH:mm:ss") }).ToListAsync() }),
            _ => BadRequest("Неизвестная таблица.")
        };
    }
}
