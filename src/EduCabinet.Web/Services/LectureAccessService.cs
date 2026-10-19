using EduCabinet.Web.Data;
using EduCabinet.Web.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace EduCabinet.Web.Services;

public class LectureAccessService : ILectureAccessService
{
    private readonly ApplicationDbContext _context;

    public LectureAccessService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<LectureAccessResult> CanAccessLectureAsync(string studentId, int lectureId)
    {
        var currentLecture = await _context.Lectures
            .Include(l => l.Topic)
            .FirstOrDefaultAsync(l => l.Id == lectureId);

        if (currentLecture == null)
        {
            return LectureAccessResult.Denied("Лекция не найдена.");
        }

        // Check if student has manual unlock override
        var currentProgress = await _context.LectureProgresses
            .FirstOrDefaultAsync(p => p.StudentId == studentId && p.LectureId == lectureId);

        if (currentProgress != null && currentProgress.IsUnlockedManually)
        {
            return LectureAccessResult.Allowed();
        }

        // Get all lectures in this discipline in linear pedagogical order
        var disciplineLectures = await _context.Lectures
            .Include(l => l.Topic)
            .Where(l => l.Topic.DisciplineId == currentLecture.Topic.DisciplineId)
            .OrderBy(l => l.Topic.OrderIndex)
            .ThenBy(l => l.TopicId)
            .ThenBy(l => l.OrderIndex)
            .ThenBy(l => l.Id)
            .ToListAsync();

        var currentIndex = disciplineLectures.FindIndex(l => l.Id == lectureId);
        if (currentIndex <= 0)
        {
            // First lecture of the discipline is always accessible
            return LectureAccessResult.Allowed();
        }

        var prevLecture = disciplineLectures[currentIndex - 1];

        // Retrieve progress for the previous lecture
        var prevProgress = await _context.LectureProgresses
            .FirstOrDefaultAsync(p => p.StudentId == studentId && p.LectureId == prevLecture.Id);

        // 1. Must have marked theory as read
        if (prevProgress == null || !prevProgress.IsTheoryRead)
        {
            return LectureAccessResult.Denied(
                $"Доступ закрыт: сначала необходимо изучить теоретический материал предшествующей лекции «{prevLecture.Title}».",
                prevLecture.Id,
                prevLecture.Title);
        }

        // 2. Check control questions if any exist
        var prevQuestions = await _context.ControlQuestions
            .Where(q => q.LectureId == prevLecture.Id)
            .ToListAsync();

        if (prevQuestions.Any() && prevLecture.RequiresQuestionsCheck)
        {
            var studentAnswers = await _context.StudentAnswers
                .Where(a => a.StudentId == studentId && a.Question.LectureId == prevLecture.Id)
                .ToListAsync();

            var answeredQuestionIds = studentAnswers.Select(a => a.QuestionId).ToHashSet();
            if (prevQuestions.Any(q => !answeredQuestionIds.Contains(q.Id)))
            {
                return LectureAccessResult.Denied(
                    $"Доступ закрыт: необходимо ответить на все контрольные вопросы к лекции «{prevLecture.Title}».",
                    prevLecture.Id,
                    prevLecture.Title);
            }

            // Check if any detailed answers require teacher review and are not yet reviewed
            var pendingReview = studentAnswers
                .Where(a => prevQuestions.First(q => q.Id == a.QuestionId).QuestionType == QuestionType.DetailedAnswer && !a.IsGraded)
                .ToList();

            if (pendingReview.Any())
            {
                return LectureAccessResult.Denied(
                    $"Доступ закрыт: ваши ответы на контрольные вопросы к лекции «{prevLecture.Title}» ожидают проверки преподавателем.",
                    prevLecture.Id,
                    prevLecture.Title);
            }
        }

        // 3. Check tests if required
        if (prevLecture.RequiresTestToUnlockNext)
        {
            var prevTestIds = await _context.Tests
                .Where(t => t.LectureId == prevLecture.Id)
                .Select(t => t.Id)
                .ToListAsync();

            if (prevTestIds.Any())
            {
                var hasPassingAttempt = await _context.TestAttempts
                    .AnyAsync(a => a.StudentId == studentId 
                                && prevTestIds.Contains(a.TestId) 
                                && a.IsPassed);

                if (!hasPassingAttempt)
                {
                    return LectureAccessResult.Denied(
                        $"Доступ закрыт: необходимо пройти тестирование по лекции «{prevLecture.Title}» с результатом не менее {prevLecture.PassingScorePercent}%.",
                        prevLecture.Id,
                        prevLecture.Title);
                }
            }
        }

        return LectureAccessResult.Allowed();
    }

    public async Task MarkTheoryReadAsync(string studentId, int lectureId)
    {
        var progress = await _context.LectureProgresses
            .FirstOrDefaultAsync(p => p.StudentId == studentId && p.LectureId == lectureId);

        if (progress == null)
        {
            progress = new LectureProgress
            {
                StudentId = studentId,
                LectureId = lectureId,
                IsTheoryRead = true
            };
            _context.LectureProgresses.Add(progress);
        }
        else
        {
            progress.IsTheoryRead = true;
        }

        await _context.SaveChangesAsync();
        await UpdateLectureCompletionStatusAsync(studentId, lectureId);
    }

    public async Task<bool> UnlockLectureManuallyAsync(string studentId, int lectureId, string teacherId, string reason)
    {
        var progress = await _context.LectureProgresses
            .FirstOrDefaultAsync(p => p.StudentId == studentId && p.LectureId == lectureId);

        if (progress == null)
        {
            progress = new LectureProgress
            {
                StudentId = studentId,
                LectureId = lectureId,
                IsUnlockedManually = true,
                UnlockedByTeacherId = teacherId,
                UnlockReason = reason,
                UnlockedAt = DateTime.UtcNow
            };
            _context.LectureProgresses.Add(progress);
        }
        else
        {
            progress.IsUnlockedManually = true;
            progress.UnlockedByTeacherId = teacherId;
            progress.UnlockReason = reason;
            progress.UnlockedAt = DateTime.UtcNow;
        }

        // Add audit log
        var teacher = await _context.Users.FindAsync(teacherId);
        _context.AuditLogs.Add(new AuditLog
        {
            Action = "ManualLectureUnlock",
            EntityName = "LectureProgress",
            EntityId = lectureId.ToString(),
            UserId = teacherId,
            UserName = teacher?.FullName ?? teacher?.UserName ?? "Teacher",
            Details = $"Преподаватель разблокировал лекцию ID={lectureId} для студента ID={studentId}. Причина: {reason}",
            Timestamp = DateTime.UtcNow
        });

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task UpdateLectureCompletionStatusAsync(string studentId, int lectureId)
    {
        var lecture = await _context.Lectures
            .Include(l => l.ControlQuestions)
            .Include(l => l.Tests)
            .FirstOrDefaultAsync(l => l.Id == lectureId);

        if (lecture == null) return;

        var progress = await _context.LectureProgresses
            .FirstOrDefaultAsync(p => p.StudentId == studentId && p.LectureId == lectureId);

        if (progress == null) return;

        // Check questions
        bool questionsPassed = true;
        if (lecture.ControlQuestions.Any())
        {
            var answers = await _context.StudentAnswers
                .Where(a => a.StudentId == studentId && a.Question.LectureId == lectureId)
                .ToListAsync();

            if (answers.Count < lecture.ControlQuestions.Count)
            {
                questionsPassed = false;
            }
            else if (lecture.RequiresQuestionsCheck)
            {
                questionsPassed = answers.All(a => a.IsGraded ? (a.TeacherScore > 0) : true);
            }
        }
        progress.AreQuestionsAnswered = questionsPassed;
        progress.AreQuestionsPassed = questionsPassed;

        // Check test
        bool testPassed = true;
        if (lecture.Tests.Any())
        {
            var testIds = lecture.Tests.Select(t => t.Id).ToList();
            testPassed = await _context.TestAttempts
                .AnyAsync(a => a.StudentId == studentId && testIds.Contains(a.TestId) && a.IsPassed);
        }
        progress.IsTestPassed = testPassed;

        if (progress.IsTheoryRead && progress.AreQuestionsPassed && progress.IsTestPassed)
        {
            if (!progress.IsCompleted)
            {
                progress.IsCompleted = true;
                progress.CompletedAt = DateTime.UtcNow;
            }
        }

        await _context.SaveChangesAsync();
    }
}
