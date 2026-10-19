using Microsoft.AspNetCore.Identity;

namespace EduCabinet.Web.Models.Entities;

public class ApplicationUser : IdentityUser
{
    public string FullName { get; set; } = string.Empty;
    public string UserRole { get; set; } = "Student"; // "Administrator", "Teacher", "Student"
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<GroupMembership> GroupMemberships { get; set; } = new List<GroupMembership>();
    public ICollection<Discipline> AuthoredDisciplines { get; set; } = new List<Discipline>();
    public ICollection<LectureProgress> ProgressRecords { get; set; } = new List<LectureProgress>();
    public ICollection<TestAttempt> TestAttempts { get; set; } = new List<TestAttempt>();
    public ICollection<StudentAnswer> StudentAnswers { get; set; } = new List<StudentAnswer>();
}

public class StudentGroup
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty; // e.g., "ИС-31"
    public int CourseNumber { get; set; } = 3;
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<GroupMembership> Memberships { get; set; } = new List<GroupMembership>();
}

public class GroupMembership
{
    public int Id { get; set; }
    public string StudentId { get; set; } = string.Empty;
    public ApplicationUser Student { get; set; } = null!;

    public int GroupId { get; set; }
    public StudentGroup Group { get; set; } = null!;

    public DateTime EnrolledAt { get; set; } = DateTime.UtcNow;
}
