using EduCabinet.Web.Data;
using EduCabinet.Web.Models.Entities;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace EduCabinet.Web.Services;

public class SubmitAttemptModel
{
    public int AttemptId { get; set; }
    // QuestionId -> chosen option id or text
    public Dictionary<int, string> Answers { get; set; } = new();
}

public interface ITestService
{
    Task<(TestAttempt? Attempt, string? Error)> StartAttemptAsync(string studentId, int testId);
    Task<(TestAttempt? Attempt, string? Error)> SubmitAttemptAsync(string studentId, SubmitAttemptModel model);
}

public class TestService : ITestService
{
    private readonly ApplicationDbContext _context;
    private readonly ILectureAccessService _lectureAccessService;

    public TestService(ApplicationDbContext context, ILectureAccessService lectureAccessService)
    {
        _context = context;
        _lectureAccessService = lectureAccessService;
    }

    public async Task<(TestAttempt? Attempt, string? Error)> StartAttemptAsync(string studentId, int testId)
    {
        var test = await _context.Tests
            .Include(t => t.Lecture)
            .Include(t => t.Questions)
                .ThenInclude(q => q.Options)
            .FirstOrDefaultAsync(t => t.Id == testId);

        if (test == null) return (null, "Тест не найден.");

        // Check attempts count
        var pastAttemptsCount = await _context.TestAttempts
            .CountAsync(a => a.StudentId == studentId && a.TestId == testId && a.Status == AttemptStatus.Completed);

        if (pastAttemptsCount >= test.MaxAttempts)
        {
            return (null, $"Вы исчерпали максимальное количество попыток ({test.MaxAttempts}).");
        }

        // Check if there is an in-progress attempt
        var activeAttempt = await _context.TestAttempts
            .FirstOrDefaultAsync(a => a.StudentId == studentId && a.TestId == testId && a.Status == AttemptStatus.InProgress);

        if (activeAttempt != null)
        {
            return (activeAttempt, null);
        }

        var newAttempt = new TestAttempt
        {
            StudentId = studentId,
            TestId = testId,
            AttemptNumber = pastAttemptsCount + 1,
            StartedAt = DateTime.UtcNow,
            Status = AttemptStatus.InProgress,
            MaxPoints = test.Questions.Sum(q => q.Points)
        };

        _context.TestAttempts.Add(newAttempt);
        await _context.SaveChangesAsync();

        return (newAttempt, null);
    }

    public async Task<(TestAttempt? Attempt, string? Error)> SubmitAttemptAsync(string studentId, SubmitAttemptModel model)
    {
        var attempt = await _context.TestAttempts
            .Include(a => a.Test)
                .ThenInclude(t => t.Questions)
                    .ThenInclude(q => q.Options)
            .FirstOrDefaultAsync(a => a.Id == model.AttemptId && a.StudentId == studentId);

        if (attempt == null) return (null, "Попытка не найдена.");
        if (attempt.Status != AttemptStatus.InProgress) return (attempt, "Данная попытка уже была завершена.");

        var test = attempt.Test;
        int totalPoints = 0;
        int maxPoints = test.Questions.Sum(q => q.Points);
        if (maxPoints == 0) maxPoints = 1;

        foreach (var q in test.Questions)
        {
            model.Answers.TryGetValue(q.Id, out var rawAnswer);
            rawAnswer = rawAnswer?.Trim() ?? string.Empty;

            bool isCorrect = false;
            int pointsEarned = 0;

            if (q.QuestionType == QuestionType.SingleChoice)
            {
                if (int.TryParse(rawAnswer, out int optionId))
                {
                    var opt = q.Options.FirstOrDefault(o => o.Id == optionId);
                    if (opt != null && opt.IsCorrect)
                    {
                        isCorrect = true;
                        pointsEarned = q.Points;
                    }
                }
            }
            else if (q.QuestionType == QuestionType.MultipleChoice)
            {
                // rawAnswer could be comma-separated list of IDs: "1,3"
                var selectedIds = rawAnswer.Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => int.TryParse(s.Trim(), out int id) ? id : 0)
                    .Where(id => id > 0)
                    .ToHashSet();

                var correctIds = q.Options.Where(o => o.IsCorrect).Select(o => o.Id).ToHashSet();
                if (selectedIds.SetEquals(correctIds))
                {
                    isCorrect = true;
                    pointsEarned = q.Points;
                }
            }
            else // ShortAnswer
            {
                var correctOpt = q.Options.FirstOrDefault(o => o.IsCorrect);
                if (correctOpt != null && string.Equals(correctOpt.OptionText.Trim(), rawAnswer, StringComparison.OrdinalIgnoreCase))
                {
                    isCorrect = true;
                    pointsEarned = q.Points;
                }
            }

            totalPoints += pointsEarned;

            var studentAns = new StudentTestAnswer
            {
                AttemptId = attempt.Id,
                QuestionId = q.Id,
                TextAnswer = rawAnswer,
                IsCorrect = isCorrect,
                PointsAwarded = pointsEarned
            };
            _context.StudentTestAnswers.Add(studentAns);
        }

        int scorePercent = (int)Math.Round((double)totalPoints / maxPoints * 100);
        bool isPassed = scorePercent >= test.PassingScorePercent;

        attempt.TotalPoints = totalPoints;
        attempt.MaxPoints = maxPoints;
        attempt.ScorePercent = scorePercent;
        attempt.IsPassed = isPassed;
        attempt.Status = AttemptStatus.Completed;
        attempt.CompletedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        // Update lecture completion progress
        await _lectureAccessService.UpdateLectureCompletionStatusAsync(studentId, test.LectureId);

        return (attempt, null);
    }
}
