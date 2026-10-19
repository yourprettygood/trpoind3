using EduCabinet.Web.Data;
using EduCabinet.Web.Models.Entities;
using EduCabinet.Web.Services;
using EduCabinet.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EduCabinet.Web.Controllers;

[Authorize(Roles = "Student,Administrator,Teacher")]
public class StudentController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILectureAccessService _lectureAccessService;
    private readonly ITestService _testService;

    public StudentController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        ILectureAccessService lectureAccessService,
        ITestService testService)
    {
        _context = context;
        _userManager = userManager;
        _lectureAccessService = lectureAccessService;
        _testService = testService;
    }

    private async Task<string> GetCurrentUserIdAsync()
    {
        var user = await _userManager.GetUserAsync(User);
        return user?.Id ?? string.Empty;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var studentId = await GetCurrentUserIdAsync();

        var disciplines = await _context.Disciplines
            .Include(d => d.Teacher)
            .Include(d => d.Topics)
                .ThenInclude(t => t.Lectures)
            .Where(d => d.IsPublished)
            .OrderBy(d => d.Title)
            .ToListAsync();

        var studentProgress = await _context.LectureProgresses
            .Where(p => p.StudentId == studentId && p.IsCompleted)
            .Select(p => p.LectureId)
            .ToListAsync();

        var cards = disciplines.Select(d =>
        {
            var allLectures = d.Topics.SelectMany(t => t.Lectures).ToList();
            int total = allLectures.Count;
            int completed = allLectures.Count(l => studentProgress.Contains(l.Id));

            return new StudentDisciplineCardViewModel
            {
                Id = d.Id,
                Title = d.Title,
                Description = d.Description,
                TeacherName = d.Teacher?.FullName ?? "Преподаватель",
                TotalLectures = total,
                CompletedLectures = completed
            };
        }).ToList();

        return View(cards);
    }

    [HttpGet]
    public async Task<IActionResult> Discipline(int id)
    {
        var studentId = await GetCurrentUserIdAsync();

        var discipline = await _context.Disciplines
            .Include(d => d.Teacher)
            .Include(d => d.Topics.OrderBy(t => t.OrderIndex))
                .ThenInclude(t => t.Lectures.OrderBy(l => l.OrderIndex))
                    .ThenInclude(l => l.Tests)
            .FirstOrDefaultAsync(d => d.Id == id);

        if (discipline == null) return NotFound();

        var progressList = await _context.LectureProgresses
            .Where(p => p.StudentId == studentId)
            .ToListAsync();

        var testAttempts = await _context.TestAttempts
            .Where(a => a.StudentId == studentId && a.Status == AttemptStatus.Completed)
            .ToListAsync();

        var model = new StudentDisciplineDetailsViewModel
        {
            Discipline = discipline
        };

        int totalLectures = 0;
        int completedLectures = 0;

        foreach (var topic in discipline.Topics)
        {
            var topicVm = new TopicWithLecturesViewModel { Topic = topic };

            foreach (var lecture in topic.Lectures)
            {
                totalLectures++;
                var access = await _lectureAccessService.CanAccessLectureAsync(studentId, lecture.Id);
                var progress = progressList.FirstOrDefault(p => p.LectureId == lecture.Id);

                var lectureTestIds = lecture.Tests.Select(t => t.Id).ToHashSet();
                var attemptsForLecture = testAttempts.Where(a => lectureTestIds.Contains(a.TestId)).ToList();
                int? bestScore = attemptsForLecture.Any() ? attemptsForLecture.Max(a => a.ScorePercent) : null;
                bool testPassed = attemptsForLecture.Any(a => a.IsPassed);

                if (progress?.IsCompleted == true)
                    completedLectures++;

                    topicVm.Lectures.Add(new StudentLectureStatusItem
                    {
                        Lecture = lecture,
                        IsUnlocked = access.IsAllowed,
                        LockReason = access.Reason,
                        IsCompleted = progress?.IsCompleted ?? false,
                        IsTheoryRead = progress?.IsTheoryRead ?? false,
                        AreQuestionsAnswered = progress?.AreQuestionsAnswered ?? false,
                        IsTestPassed = testPassed,
                        BestTestScore = bestScore,
                        AttemptsUsed = attemptsForLecture.Count,
                        FinalGrade = progress?.FinalGrade,
                        IsOverdue = progress?.IsOverdue ?? (lecture.Deadline.HasValue && DateTime.UtcNow > lecture.Deadline.Value && !(progress?.IsCompleted ?? false)),
                        TeacherGradeComment = progress?.TeacherGradeComment
                    });
            }

            model.Topics.Add(topicVm);
        }

        model.OverallProgressPercent = totalLectures > 0 
            ? (int)Math.Round((double)completedLectures / totalLectures * 100) 
            : 0;

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Lecture(int id)
    {
        var studentId = await GetCurrentUserIdAsync();

        var lecture = await _context.Lectures
            .Include(l => l.Topic)
                .ThenInclude(t => t.Discipline)
            .Include(l => l.ControlQuestions.OrderBy(q => q.OrderIndex))
            .Include(l => l.Tests)
            .FirstOrDefaultAsync(l => l.Id == id);

        if (lecture == null) return NotFound();

        // Server-side sequential access enforcement
        var access = await _lectureAccessService.CanAccessLectureAsync(studentId, id);

        var progress = await _context.LectureProgresses
            .FirstOrDefaultAsync(p => p.StudentId == studentId && p.LectureId == id);

        var answers = await _context.StudentAnswers
            .Where(a => a.StudentId == studentId && a.Question.LectureId == id)
            .ToListAsync();

        var test = lecture.Tests.FirstOrDefault();
        List<TestAttempt> attempts = new();
        if (test != null)
        {
            attempts = await _context.TestAttempts
                .Where(a => a.StudentId == studentId && a.TestId == test.Id)
                .OrderByDescending(a => a.StartedAt)
                .ToListAsync();
        }

        var model = new StudentLectureViewViewModel
        {
            Lecture = lecture,
            Progress = progress,
            IsUnlocked = access.IsAllowed,
            LockReason = access.Reason,
            Questions = lecture.ControlQuestions.ToList(),
            ExistingAnswers = answers,
            Test = test,
            TestAttempts = attempts
        };

        if (access.IsAllowed)
        {
            // Auto mark theory as read upon opening
            await _lectureAccessService.MarkTheoryReadAsync(studentId, id);
        }

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> DownloadMaterial(int id)
    {
        var studentId = await GetCurrentUserIdAsync();
        var access = await _lectureAccessService.CanAccessLectureAsync(studentId, id);
        if (!access.IsAllowed && !User.IsInRole("Teacher") && !User.IsInRole("Administrator"))
        {
            return Forbid();
        }

        var lecture = await _context.Lectures.FindAsync(id);
        if (lecture == null || string.IsNullOrEmpty(lecture.ContentFilePath))
            return NotFound("Файл материала не найден.");

        var fullPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", lecture.ContentFilePath.TrimStart('/'));
        if (!System.IO.File.Exists(fullPath))
            return NotFound("Файл физически отсутствует на сервере.");

        var contentType = lecture.ContentType ?? "application/octet-stream";
        var fileName = lecture.ContentFileName ?? Path.GetFileName(fullPath);
        return PhysicalFile(fullPath, contentType, fileName);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AnswerQuestions(int lectureId, IFormCollection form)
    {
        var studentId = await GetCurrentUserIdAsync();
        var access = await _lectureAccessService.CanAccessLectureAsync(studentId, lectureId);
        if (!access.IsAllowed) return Forbid();

        var questions = await _context.ControlQuestions
            .Where(q => q.LectureId == lectureId)
            .ToListAsync();

        foreach (var q in questions)
        {
            string key = $"question_{q.Id}";
            if (form.ContainsKey(key))
            {
                string answerText = form[key].ToString().Trim();

                var existingAnswer = await _context.StudentAnswers
                    .FirstOrDefaultAsync(a => a.QuestionId == q.Id && a.StudentId == studentId);

                if (existingAnswer == null)
                {
                    existingAnswer = new StudentAnswer
                    {
                        QuestionId = q.Id,
                        StudentId = studentId,
                        StudentAnswerText = answerText,
                        SubmittedAt = DateTime.UtcNow
                    };
                    _context.StudentAnswers.Add(existingAnswer);
                }
                else
                {
                    // Update student's text without touching teacher's review
                    existingAnswer.StudentAnswerText = answerText;
                    existingAnswer.SubmittedAt = DateTime.UtcNow;
                }

                // Auto-grading for objective types
                if (q.QuestionType == QuestionType.ShortAnswer || q.QuestionType == QuestionType.SingleChoice)
                {
                    if (!string.IsNullOrEmpty(q.CorrectAnswer))
                    {
                        bool isCorrect = string.Equals(q.CorrectAnswer.Trim(), answerText, StringComparison.OrdinalIgnoreCase);
                        existingAnswer.IsGraded = true;
                        existingAnswer.TeacherScore = isCorrect ? q.MaxScore : 0;
                        existingAnswer.TeacherFeedback = isCorrect ? "Верно (автопроверка)" : "Неверно (автопроверка)";
                        existingAnswer.GradedAt = DateTime.UtcNow;
                    }
                }
            }
        }

        await _context.SaveChangesAsync();
        await _lectureAccessService.UpdateLectureCompletionStatusAsync(studentId, lectureId);

        TempData["SuccessMessage"] = "Ответы на контрольные вопросы успешно сохранены!";
        return RedirectToAction(nameof(Lecture), new { id = lectureId });
    }

    [HttpGet]
    public async Task<IActionResult> TakeTest(int testId)
    {
        var studentId = await GetCurrentUserIdAsync();

        var test = await _context.Tests
            .Include(t => t.Lecture)
                .ThenInclude(l => l.Topic)
            .Include(t => t.Questions.OrderBy(q => q.OrderIndex))
                .ThenInclude(q => q.Options.OrderBy(o => o.OrderIndex))
            .FirstOrDefaultAsync(t => t.Id == testId);

        if (test == null) return NotFound("Тест не найден.");

        // Check if student has access to this lecture
        var access = await _lectureAccessService.CanAccessLectureAsync(studentId, test.LectureId);
        if (!access.IsAllowed && !User.IsInRole("Teacher") && !User.IsInRole("Administrator"))
        {
            return Forbid();
        }

        var (attempt, error) = await _testService.StartAttemptAsync(studentId, testId);
        if (attempt == null)
        {
            TempData["ErrorMessage"] = error;
            return RedirectToAction(nameof(Lecture), new { id = test.LectureId });
        }

        ViewBag.Attempt = attempt;
        return View(test);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SubmitTest(int attemptId, int testId, IFormCollection form)
    {
        var studentId = await GetCurrentUserIdAsync();

        var model = new SubmitAttemptModel { AttemptId = attemptId };
        var test = await _context.Tests
            .Include(t => t.Questions)
            .FirstOrDefaultAsync(t => t.Id == testId);

        if (test == null) return NotFound();

        foreach (var q in test.Questions)
        {
            string key = $"question_{q.Id}";
            if (form.ContainsKey(key))
            {
                model.Answers[q.Id] = form[key].ToString();
            }
        }

        var (attempt, error) = await _testService.SubmitAttemptAsync(studentId, model);
        if (attempt == null)
        {
            TempData["ErrorMessage"] = error;
            return RedirectToAction(nameof(TakeTest), new { testId });
        }

        return RedirectToAction(nameof(TestResult), new { attemptId = attempt.Id });
    }

    [HttpGet]
    public async Task<IActionResult> TestResult(int attemptId)
    {
        var studentId = await GetCurrentUserIdAsync();

        var attempt = await _context.TestAttempts
            .Include(a => a.Test)
                .ThenInclude(t => t.Lecture)
            .Include(a => a.StudentAnswers)
                .ThenInclude(sa => sa.Question)
                    .ThenInclude(q => q.Options)
            .FirstOrDefaultAsync(a => a.Id == attemptId);

        if (attempt == null) return NotFound();

        // RBAC: student can only view their own attempts (GEMINI.md Rule 4)
        if (attempt.StudentId != studentId && !User.IsInRole("Teacher") && !User.IsInRole("Administrator"))
        {
            return Forbid();
        }

        return View(attempt);
    }

    [HttpGet]
    public async Task<IActionResult> GradeBook()
    {
        var studentId = await GetCurrentUserIdAsync();
        var student = await _context.Users
            .Include(u => u.GroupMemberships)
                .ThenInclude(m => m.Group)
            .FirstOrDefaultAsync(u => u.Id == studentId);

        if (student == null) return NotFound();

        var group = student.GroupMemberships.FirstOrDefault()?.Group;

        var disciplines = await _context.Disciplines
            .Include(d => d.Teacher)
            .Include(d => d.Topics.OrderBy(t => t.OrderIndex))
                .ThenInclude(t => t.Lectures.OrderBy(l => l.OrderIndex))
                    .ThenInclude(l => l.Tests)
            .Where(d => d.IsPublished)
            .ToListAsync();

        var progressList = await _context.LectureProgresses
            .Where(p => p.StudentId == studentId)
            .ToListAsync();

        var testAttempts = await _context.TestAttempts
            .Where(a => a.StudentId == studentId && a.Status == AttemptStatus.Completed)
            .ToListAsync();

        var model = new StudentGradeBookViewModel
        {
            Student = student,
            Group = group
        };

        foreach (var disc in disciplines)
        {
            var summary = new DisciplineGradeSummary
            {
                DisciplineTitle = disc.Title,
                TeacherName = disc.Teacher?.FullName ?? "Преподаватель"
            };

            var allLectures = disc.Topics.SelectMany(t => t.Lectures).ToList();
            summary.TotalLectures = allLectures.Count;

            int completedCount = 0;
            foreach (var lec in allLectures)
            {
                var prog = progressList.FirstOrDefault(p => p.LectureId == lec.Id);
                var testIds = lec.Tests.Select(t => t.Id).ToHashSet();
                var attempts = testAttempts.Where(a => testIds.Contains(a.TestId)).ToList();

                bool isCompleted = prog?.IsCompleted ?? false;
                if (isCompleted) completedCount++;

                summary.LectureGrades.Add(new LectureGradeItem
                {
                    LectureTitle = lec.Title,
                    IsCompleted = isCompleted,
                    BestTestScore = attempts.Any() ? attempts.Max(a => a.ScorePercent) : null,
                    TestPassed = attempts.Any(a => a.IsPassed),
                    AttemptsUsed = attempts.Count,
                    FinalGrade = prog?.FinalGrade,
                    IsOverdue = prog?.IsOverdue ?? (lec.Deadline.HasValue && DateTime.UtcNow > lec.Deadline.Value && !isCompleted)
                });
            }

            summary.CompletedLectures = completedCount;
            summary.ProgressPercent = summary.TotalLectures > 0 
                ? (int)Math.Round((double)completedCount / summary.TotalLectures * 100) 
                : 0;

            model.Disciplines.Add(summary);
        }

        return View(model);
    }

    /// <summary>
    /// Официальная печатная ведомость / сертификат успеваемости студента
    /// Строго соблюдает GEMINI.md Rule 2 (@@media print, .no-print, window.print(), подписи, М.П.)
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GradeReport()
    {
        var studentId = await GetCurrentUserIdAsync();
        var student = await _context.Users
            .Include(u => u.GroupMemberships)
                .ThenInclude(m => m.Group)
            .FirstOrDefaultAsync(u => u.Id == studentId);

        if (student == null) return NotFound();

        var group = student.GroupMemberships.FirstOrDefault()?.Group;

        var disciplines = await _context.Disciplines
            .Include(d => d.Teacher)
            .Include(d => d.Topics)
                .ThenInclude(t => t.Lectures)
                    .ThenInclude(l => l.Tests)
            .Where(d => d.IsPublished)
            .ToListAsync();

        var progressList = await _context.LectureProgresses
            .Where(p => p.StudentId == studentId)
            .ToListAsync();

        var testAttempts = await _context.TestAttempts
            .Where(a => a.StudentId == studentId && a.Status == AttemptStatus.Completed)
            .ToListAsync();

        var model = new StudentGradeBookViewModel
        {
            Student = student,
            Group = group
        };

        foreach (var disc in disciplines)
        {
            var summary = new DisciplineGradeSummary
            {
                DisciplineTitle = disc.Title,
                TeacherName = disc.Teacher?.FullName ?? "Преподаватель"
            };

            var allLectures = disc.Topics.SelectMany(t => t.Lectures).ToList();
            summary.TotalLectures = allLectures.Count;

            int completedCount = 0;
            foreach (var lec in allLectures)
            {
                var prog = progressList.FirstOrDefault(p => p.LectureId == lec.Id);
                var testIds = lec.Tests.Select(t => t.Id).ToHashSet();
                var attempts = testAttempts.Where(a => testIds.Contains(a.TestId)).ToList();

                bool isCompleted = prog?.IsCompleted ?? false;
                if (isCompleted) completedCount++;

                summary.LectureGrades.Add(new LectureGradeItem
                {
                    LectureTitle = lec.Title,
                    IsCompleted = isCompleted,
                    BestTestScore = attempts.Any() ? attempts.Max(a => a.ScorePercent) : null,
                    TestPassed = attempts.Any(a => a.IsPassed),
                    AttemptsUsed = attempts.Count,
                    FinalGrade = prog?.FinalGrade,
                    IsOverdue = prog?.IsOverdue ?? (lec.Deadline.HasValue && DateTime.UtcNow > lec.Deadline.Value && !isCompleted)
                });
            }

            summary.CompletedLectures = completedCount;
            summary.ProgressPercent = summary.TotalLectures > 0 
                ? (int)Math.Round((double)completedCount / summary.TotalLectures * 100) 
                : 0;

            model.Disciplines.Add(summary);
        }

        return View(model);
    }
}
