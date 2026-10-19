using EduCabinet.Web.Data;
using EduCabinet.Web.Models.Entities;
using EduCabinet.Web.Services;
using EduCabinet.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EduCabinet.Web.Controllers;

[Authorize(Roles = "Teacher,Administrator")]
public class TeacherController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILectureAccessService _lectureAccessService;
    private readonly IWebHostEnvironment _environment;

    public TeacherController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        ILectureAccessService lectureAccessService,
        IWebHostEnvironment environment)
    {
        _context = context;
        _userManager = userManager;
        _lectureAccessService = lectureAccessService;
        _environment = environment;
    }

    private async Task<string> GetCurrentUserIdAsync()
    {
        var user = await _userManager.GetUserAsync(User);
        return user?.Id ?? string.Empty;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var teacherId = await GetCurrentUserIdAsync();
        var isAdmin = User.IsInRole("Administrator");

        var disciplinesQuery = _context.Disciplines.AsQueryable();
        if (!isAdmin)
            disciplinesQuery = disciplinesQuery.Where(d => d.TeacherId == teacherId);

        var disciplines = await disciplinesQuery
            .Include(d => d.Topics)
                .ThenInclude(t => t.Lectures)
            .OrderBy(d => d.Title)
            .ToListAsync();

        var pendingAnswersCount = await _context.StudentAnswers
            .CountAsync(a => !a.IsGraded && a.Question.QuestionType == QuestionType.DetailedAnswer);

        var totalStudents = await _userManager.GetUsersInRoleAsync("Student");

        var recentAttempts = await _context.TestAttempts
            .CountAsync(a => a.StartedAt >= DateTime.UtcNow.AddDays(-7));

        var model = new TeacherDashboardViewModel
        {
            Disciplines = disciplines,
            TotalStudentsCount = totalStudents.Count,
            PendingReviewsCount = pendingAnswersCount,
            RecentAttemptsCount = recentAttempts
        };

        return View(model);
    }

    // === Disciplines ===

    [HttpGet]
    public async Task<IActionResult> Disciplines()
    {
        var teacherId = await GetCurrentUserIdAsync();
        var isAdmin = User.IsInRole("Administrator");

        var query = _context.Disciplines
            .Include(d => d.Teacher)
            .Include(d => d.Topics)
                .ThenInclude(t => t.Lectures)
            .AsQueryable();

        if (!isAdmin)
            query = query.Where(d => d.TeacherId == teacherId);

        var list = await query.OrderBy(d => d.Title).ToListAsync();
        return View(list);
    }

    [HttpGet]
    public IActionResult CreateDiscipline() => View(new Discipline());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateDiscipline(Discipline model)
    {
        if (string.IsNullOrWhiteSpace(model.Title))
        {
            ModelState.AddModelError("Title", "Укажите название дисциплины");
            return View(model);
        }

        var teacherId = await GetCurrentUserIdAsync();
        model.TeacherId = teacherId;
        model.CreatedAt = DateTime.UtcNow;

        _context.Disciplines.Add(model);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "Дисциплина успешно создана!";
        return RedirectToAction(nameof(Disciplines));
    }

    // === Topics ===

    [HttpGet]
    public async Task<IActionResult> Topics(int disciplineId)
    {
        var discipline = await _context.Disciplines
            .Include(d => d.Topics.OrderBy(t => t.OrderIndex))
                .ThenInclude(t => t.Lectures.OrderBy(l => l.OrderIndex))
            .FirstOrDefaultAsync(d => d.Id == disciplineId);

        if (discipline == null) return NotFound();
        return View(discipline);
    }

    [HttpGet]
    public IActionResult CreateTopic(int disciplineId)
    {
        return View(new Topic { DisciplineId = disciplineId, OrderIndex = 1 });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateTopic(Topic model)
    {
        if (string.IsNullOrWhiteSpace(model.Title))
        {
            ModelState.AddModelError("Title", "Укажите название темы");
            return View(model);
        }

        _context.Topics.Add(model);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "Тема успешно добавлена!";
        return RedirectToAction(nameof(Topics), new { disciplineId = model.DisciplineId });
    }

    // === Lectures ===

    [HttpGet]
    public async Task<IActionResult> CreateLecture(int topicId)
    {
        var topic = await _context.Topics
            .Include(t => t.Discipline)
            .FirstOrDefaultAsync(t => t.Id == topicId);

        if (topic == null) return NotFound();

        var model = new CreateLectureViewModel
        {
            TopicId = topic.Id,
            TopicTitle = topic.Title,
            DisciplineId = topic.DisciplineId,
            OrderIndex = 1,
            PassingScorePercent = 70,
            RequiresTestToUnlockNext = true,
            RequiresQuestionsCheck = true
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateLecture(CreateLectureViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        string? filePath = null;
        string? originalFileName = null;
        string? contentType = null;
        long? fileSize = null;

        if (model.MaterialFile != null && model.MaterialFile.Length > 0)
        {
            var ext = Path.GetExtension(model.MaterialFile.FileName).ToLowerInvariant();
            var allowedExtensions = new[] { ".pdf", ".docx", ".doc", ".txt", ".zip" };
            if (!allowedExtensions.Contains(ext))
            {
                ModelState.AddModelError("MaterialFile", "Разрешены только файлы форматов: PDF, DOCX, DOC, TXT, ZIP.");
                return View(model);
            }

            var uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads");
            if (!Directory.Exists(uploadsFolder))
                Directory.CreateDirectory(uploadsFolder);

            var uniqueFileName = $"{Guid.NewGuid()}{ext}";
            var physicalPath = Path.Combine(uploadsFolder, uniqueFileName);

            using (var stream = new FileStream(physicalPath, FileMode.Create))
            {
                await model.MaterialFile.CopyToAsync(stream);
            }

            filePath = $"/uploads/{uniqueFileName}";
            originalFileName = Path.GetFileName(model.MaterialFile.FileName);
            contentType = model.MaterialFile.ContentType;
            fileSize = model.MaterialFile.Length;
        }

        var lecture = new Lecture
        {
            TopicId = model.TopicId,
            Title = model.Title,
            Description = model.Description ?? string.Empty,
            OrderIndex = model.OrderIndex,
            ContentHtml = model.ContentHtml,
            ContentFilePath = filePath,
            ContentFileName = originalFileName,
            ContentType = contentType,
            FileSizeBytes = fileSize,
            RequiresTestToUnlockNext = model.RequiresTestToUnlockNext,
            PassingScorePercent = model.PassingScorePercent,
            RequiresQuestionsCheck = model.RequiresQuestionsCheck,
            CreatedAt = DateTime.UtcNow
        };

        _context.Lectures.Add(lecture);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "Лекция успешно создана!";
        return RedirectToAction(nameof(Topics), new { disciplineId = model.DisciplineId });
    }

    // === Control Questions Management ===

    [HttpGet]
    public async Task<IActionResult> Questions(int lectureId)
    {
        var lecture = await _context.Lectures
            .Include(l => l.Topic)
                .ThenInclude(t => t.Discipline)
            .Include(l => l.ControlQuestions.OrderBy(q => q.OrderIndex))
            .FirstOrDefaultAsync(l => l.Id == lectureId);

        if (lecture == null) return NotFound();
        return View(lecture);
    }

    [HttpGet]
    public IActionResult CreateQuestion(int lectureId)
    {
        return View(new ControlQuestion { LectureId = lectureId, MaxScore = 5, OrderIndex = 1 });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateQuestion(ControlQuestion model)
    {
        if (string.IsNullOrWhiteSpace(model.QuestionText))
        {
            ModelState.AddModelError("QuestionText", "Введите текст вопроса");
            return View(model);
        }

        _context.ControlQuestions.Add(model);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "Контрольный вопрос успешно добавлен!";
        return RedirectToAction(nameof(Questions), new { lectureId = model.LectureId });
    }

    // === Grading Student Answers (GEMINI.md Rule 1) ===

    [HttpGet]
    public async Task<IActionResult> ReviewAnswers()
    {
        var answers = await _context.StudentAnswers
            .Include(a => a.Student)
            .Include(a => a.Question)
                .ThenInclude(q => q.Lecture)
                    .ThenInclude(l => l.Topic)
                        .ThenInclude(t => t.Discipline)
            .Where(a => !a.IsGraded && a.Question.QuestionType == QuestionType.DetailedAnswer)
            .OrderBy(a => a.SubmittedAt)
            .ToListAsync();

        return View(answers);
    }

    [HttpGet]
    public async Task<IActionResult> GradeAnswer(int id)
    {
        var answer = await _context.StudentAnswers
            .Include(a => a.Student)
            .Include(a => a.Question)
                .ThenInclude(q => q.Lecture)
                    .ThenInclude(l => l.Topic)
                        .ThenInclude(t => t.Discipline)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (answer == null) return NotFound();

        var model = new GradeSubmissionViewModel
        {
            AnswerId = answer.Id,
            StudentName = answer.Student?.FullName ?? "Студент",
            DisciplineTitle = answer.Question.Lecture.Topic.Discipline.Title,
            LectureTitle = answer.Question.Lecture.Title,
            QuestionText = answer.Question.QuestionText,
            MaxScore = answer.Question.MaxScore,
            StudentAnswerText = answer.StudentAnswerText, // Preserved immutably
            SubmittedAt = answer.SubmittedAt,
            TeacherScore = answer.TeacherScore ?? answer.Question.MaxScore,
            TeacherFeedback = answer.TeacherFeedback
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GradeAnswer(GradeSubmissionViewModel model)
    {
        var answer = await _context.StudentAnswers
            .Include(a => a.Question)
            .FirstOrDefaultAsync(a => a.Id == model.AnswerId);

        if (answer == null) return NotFound();

        var teacherId = await GetCurrentUserIdAsync();

        // Strictly follows GEMINI.md Rule 1:
        // Teacher review fields are stored isolated from student data, student text is never modified
        answer.IsGraded = true;
        answer.TeacherScore = model.TeacherScore;
        answer.TeacherFeedback = model.TeacherFeedback;
        answer.GradedById = teacherId;
        answer.GradedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        // Update progress for this lecture
        await _lectureAccessService.UpdateLectureCompletionStatusAsync(answer.StudentId, answer.Question.LectureId);

        TempData["SuccessMessage"] = "Оценка и отзыв успешно сохранены!";
        return RedirectToAction(nameof(ReviewAnswers));
    }

    // === Group Journal & Matrix ===

    [HttpGet]
    public async Task<IActionResult> Journal(int disciplineId, int? groupId)
    {
        var discipline = await _context.Disciplines
            .Include(d => d.Topics.OrderBy(t => t.OrderIndex))
                .ThenInclude(t => t.Lectures.OrderBy(l => l.OrderIndex))
                    .ThenInclude(l => l.Tests)
            .FirstOrDefaultAsync(d => d.Id == disciplineId);

        if (discipline == null) return NotFound();

        var availableGroups = await _context.StudentGroups.OrderBy(g => g.Name).ToListAsync();
        var selectedGroupId = groupId ?? availableGroups.FirstOrDefault()?.Id;
        var selectedGroup = availableGroups.FirstOrDefault(g => g.Id == selectedGroupId);

        var lectures = discipline.Topics
            .SelectMany(t => t.Lectures)
            .OrderBy(l => l.Topic.OrderIndex)
            .ThenBy(l => l.OrderIndex)
            .ToList();

        var model = new GroupJournalViewModel
        {
            Discipline = discipline,
            AvailableGroups = availableGroups,
            SelectedGroupId = selectedGroupId,
            SelectedGroup = selectedGroup,
            OrderedLectures = lectures
        };

        if (selectedGroupId.HasValue)
        {
            var studentIds = await _context.GroupMemberships
                .Where(m => m.GroupId == selectedGroupId.Value)
                .Select(m => m.StudentId)
                .ToListAsync();

            var students = await _context.Users
                .Where(u => studentIds.Contains(u.Id))
                .OrderBy(u => u.FullName)
                .ToListAsync();

            var progresses = await _context.LectureProgresses
                .Where(p => studentIds.Contains(p.StudentId))
                .ToListAsync();

            var testAttempts = await _context.TestAttempts
                .Where(a => studentIds.Contains(a.StudentId) && a.Status == AttemptStatus.Completed)
                .ToListAsync();

            foreach (var student in students)
            {
                var row = new StudentJournalRow { Student = student };
                int completedCount = 0;

                foreach (var lec in lectures)
                {
                    var prog = progresses.FirstOrDefault(p => p.StudentId == student.Id && p.LectureId == lec.Id);
                    var access = await _lectureAccessService.CanAccessLectureAsync(student.Id, lec.Id);
                    var testIds = lec.Tests.Select(t => t.Id).ToHashSet();
                    var attempts = testAttempts.Where(a => a.StudentId == student.Id && testIds.Contains(a.TestId)).ToList();

                    bool isCompleted = prog?.IsCompleted ?? false;
                    if (isCompleted) completedCount++;

                    row.LectureCells[lec.Id] = new StudentLectureCell
                    {
                        IsUnlocked = access.IsAllowed,
                        IsUnlockedManually = prog?.IsUnlockedManually ?? false,
                        IsCompleted = isCompleted,
                        IsTheoryRead = prog?.IsTheoryRead ?? false,
                        BestTestScore = attempts.Any() ? attempts.Max(a => a.ScorePercent) : null,
                        TestPassed = attempts.Any(a => a.IsPassed),
                        AttemptsCount = attempts.Count,
                        FinalGrade = prog?.FinalGrade,
                        IsOverdue = prog?.IsOverdue ?? (lec.Deadline.HasValue && DateTime.UtcNow > lec.Deadline.Value && !isCompleted),
                        TeacherGradeComment = prog?.TeacherGradeComment,
                        Deadline = lec.Deadline
                    };
                }

                row.CompletedLecturesCount = completedCount;
                row.OverallPercent = lectures.Count > 0 
                    ? (int)Math.Round((double)completedCount / lectures.Count * 100) 
                    : 0;

                model.StudentRows.Add(row);
            }
        }

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateStudentGrade(string studentId, int lectureId, int grade, string? comment, int disciplineId, int? groupId)
    {
        var teacherId = await GetCurrentUserIdAsync();
        await _lectureAccessService.SetTeacherGradeAsync(studentId, lectureId, grade, comment, teacherId);

        TempData["SuccessMessage"] = $"Оценка [{grade}] успешно выставлена преподавателем!";
        return RedirectToAction(nameof(Journal), new { disciplineId, groupId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ManualUnlock(ManualUnlockViewModel model, int disciplineId, int? groupId)
    {
        var teacherId = await GetCurrentUserIdAsync();
        await _lectureAccessService.UnlockLectureManuallyAsync(model.StudentId, model.LectureId, teacherId, model.Reason);

        TempData["SuccessMessage"] = "Лекция успешно разблокирована преподавателем в ручном режиме!";
        return RedirectToAction(nameof(Journal), new { disciplineId, groupId });
    }

    /// <summary>
    /// Официальная печатная ведомость успеваемости группы по дисциплине
    /// Строго соблюдает GEMINI.md Rule 2 (@@media print, .no-print, window.print(), подписи, М.П.)
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GroupReport(int disciplineId, int groupId)
    {
        var discipline = await _context.Disciplines
            .Include(d => d.Teacher)
            .Include(d => d.Topics.OrderBy(t => t.OrderIndex))
                .ThenInclude(t => t.Lectures.OrderBy(l => l.OrderIndex))
                    .ThenInclude(l => l.Tests)
            .FirstOrDefaultAsync(d => d.Id == disciplineId);

        var group = await _context.StudentGroups.FindAsync(groupId);
        if (discipline == null || group == null) return NotFound();

        var lectures = discipline.Topics
            .SelectMany(t => t.Lectures)
            .OrderBy(l => l.Topic.OrderIndex)
            .ThenBy(l => l.OrderIndex)
            .ToList();

        var studentIds = await _context.GroupMemberships
            .Where(m => m.GroupId == groupId)
            .Select(m => m.StudentId)
            .ToListAsync();

        var students = await _context.Users
            .Where(u => studentIds.Contains(u.Id))
            .OrderBy(u => u.FullName)
            .ToListAsync();

        var progresses = await _context.LectureProgresses
            .Where(p => studentIds.Contains(p.StudentId))
            .ToListAsync();

        var testAttempts = await _context.TestAttempts
            .Where(a => studentIds.Contains(a.StudentId) && a.Status == AttemptStatus.Completed)
            .ToListAsync();

        var model = new GroupJournalViewModel
        {
            Discipline = discipline,
            SelectedGroupId = groupId,
            SelectedGroup = group,
            OrderedLectures = lectures
        };

        foreach (var student in students)
        {
            var row = new StudentJournalRow { Student = student };
            int completedCount = 0;

            foreach (var lec in lectures)
            {
                var prog = progresses.FirstOrDefault(p => p.StudentId == student.Id && p.LectureId == lec.Id);
                var testIds = lec.Tests.Select(t => t.Id).ToHashSet();
                var attempts = testAttempts.Where(a => a.StudentId == student.Id && testIds.Contains(a.TestId)).ToList();

                bool isCompleted = prog?.IsCompleted ?? false;
                if (isCompleted) completedCount++;

                row.LectureCells[lec.Id] = new StudentLectureCell
                {
                    IsCompleted = isCompleted,
                    IsTheoryRead = prog?.IsTheoryRead ?? false,
                    BestTestScore = attempts.Any() ? attempts.Max(a => a.ScorePercent) : null,
                    TestPassed = attempts.Any(a => a.IsPassed),
                    AttemptsCount = attempts.Count
                };
            }

            row.CompletedLecturesCount = completedCount;
            row.OverallPercent = lectures.Count > 0 
                ? (int)Math.Round((double)completedCount / lectures.Count * 100) 
                : 0;

            model.StudentRows.Add(row);
        }

        return View(model);
    }
}
