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

        // Apply automatic deadline check (if past deadline and not completed -> auto Grade 2)
        await CheckAndApplyDeadlinesAsync(studentId, lectureId);

        // All lectures are accessible for reading and study according to user specification
        return LectureAccessResult.Allowed();
    }

    public async Task CheckAndApplyDeadlinesAsync(string studentId, int lectureId)
    {
        var lecture = await _context.Lectures.FindAsync(lectureId);
        if (lecture == null) return;

        var progress = await _context.LectureProgresses
            .FirstOrDefaultAsync(p => p.StudentId == studentId && p.LectureId == lectureId);

        if (progress == null)
        {
            progress = new LectureProgress
            {
                StudentId = studentId,
                LectureId = lectureId
            };
            _context.LectureProgresses.Add(progress);
        }

        // If deadline has passed and the student hasn't completed the assignment
        if (lecture.Deadline.HasValue && DateTime.UtcNow > lecture.Deadline.Value && !progress.IsCompleted)
        {
            progress.IsOverdue = true;
            // Only set auto-grade 2 if teacher has not manually assigned a grade
            if (!progress.FinalGrade.HasValue)
            {
                progress.FinalGrade = 2;
                progress.TeacherGradeComment = "Просрочено: задание не выполнено в установленный срок (автоматическая оценка 2)";
                progress.GradedAt = DateTime.UtcNow;
            }
        }

        await _context.SaveChangesAsync();
    }

    public async Task SetTeacherGradeAsync(string studentId, int lectureId, int grade, string? comment, string teacherId)
    {
        var progress = await _context.LectureProgresses
            .FirstOrDefaultAsync(p => p.StudentId == studentId && p.LectureId == lectureId);

        if (progress == null)
        {
            progress = new LectureProgress
            {
                StudentId = studentId,
                LectureId = lectureId
            };
            _context.LectureProgresses.Add(progress);
        }

        var oldGrade = progress.FinalGrade;
        progress.FinalGrade = grade;
        progress.TeacherGradeComment = comment;
        progress.GradedByTeacherId = teacherId;
        progress.GradedAt = DateTime.UtcNow;
        if (grade >= 3)
        {
            progress.IsCompleted = true;
            progress.CompletedAt ??= DateTime.UtcNow;
        }

        // Audit log
        var teacher = await _context.Users.FindAsync(teacherId);
        var student = await _context.Users.FindAsync(studentId);
        var lecture = await _context.Lectures.FindAsync(lectureId);

        _context.AuditLogs.Add(new AuditLog
        {
            Action = "TeacherGradeChanged",
            EntityName = "LectureProgress",
            EntityId = lectureId.ToString(),
            UserId = teacherId,
            UserName = teacher?.FullName ?? "Преподаватель",
            Details = $"Преподаватель изменил оценку студенту {student?.FullName ?? studentId} по лекции «{lecture?.Title}» с [{oldGrade?.ToString() ?? "нет"}] на [{grade}]. Комментарий: {comment ?? "без комментария"}",
            Timestamp = DateTime.UtcNow
        });

        await _context.SaveChangesAsync();
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
        int? bestScore = null;
        if (lecture.Tests.Any())
        {
            var testIds = lecture.Tests.Select(t => t.Id).ToList();
            var attempts = await _context.TestAttempts
                .Where(a => a.StudentId == studentId && testIds.Contains(a.TestId) && a.Status == AttemptStatus.Completed)
                .ToListAsync();

            if (attempts.Any())
            {
                bestScore = attempts.Max(a => a.ScorePercent);
                testPassed = attempts.Any(a => a.IsPassed);
            }
            else
            {
                testPassed = false;
            }
        }
        progress.IsTestPassed = testPassed;

        if (progress.IsTheoryRead && progress.AreQuestionsPassed && progress.IsTestPassed)
        {
            progress.IsCompleted = true;
            progress.CompletedAt ??= DateTime.UtcNow;

            // Automatically calculate grade if teacher hasn't manually set it
            if (progress.FinalGrade == null || progress.FinalGrade == 2)
            {
                int score = bestScore ?? 100;
                int calculatedGrade = score switch
                {
                    >= 90 => 5,
                    >= 75 => 4,
                    >= 60 => 3,
                    _ => 2
                };
                progress.FinalGrade = calculatedGrade;
                progress.TeacherGradeComment = $"Автоматическая оценка по результатам тестирования ({score}%)";
                progress.GradedAt = DateTime.UtcNow;
            }
        }

        await _context.SaveChangesAsync();
    }
}
