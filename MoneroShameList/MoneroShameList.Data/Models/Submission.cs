using System.ComponentModel.DataAnnotations;

namespace MoneroShameList.Models;

public enum SubmissionStatus { Pending, Approved, Rejected }

public class Submission
{
    public int Id { get; set; }
    [MaxLength(200)] public string Name { get; set; } = "";
    [MaxLength(500)] public string Website { get; set; } = "";
    [MaxLength(1000)] public string Description { get; set; } = "";
    [MaxLength(1000)] public string WhyShamed { get; set; } = "";
    [MaxLength(500)] public string? ContactUrl { get; set; }
    [MaxLength(500)] public string? MoneroAlternativeUrl { get; set; }
    [MaxLength(200)] public string? SubmitterEmail { get; set; }
    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;
    public SubmissionStatus Status { get; set; } = SubmissionStatus.Pending;
    public string? AdminNote { get; set; }
    public ShameCategory Category { get; set; } = ShameCategory.NeverAdded;
}
