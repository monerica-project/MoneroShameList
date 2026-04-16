using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using MoneroShameList.Data;
using MoneroShameList.Models;

namespace MoneroShameList.Pages.Admin;

[Authorize]
public class AdminIndexModel(AppDbContext db) : PageModel
{
    public List<Submission> Submissions { get; set; } = [];
    public string Status { get; set; } = "pending";
    public int PendingCount { get; set; }

    public async Task OnGetAsync(string? status)
    {
        Status = status is "approved" or "rejected" ? status : "pending";

        var parsed = Enum.Parse<SubmissionStatus>(Status, ignoreCase: true);
        Submissions = await db.Submissions
            .Where(s => s.Status == parsed)
            .OrderByDescending(s => s.SubmittedAt)
            .ToListAsync();

        PendingCount = await db.Submissions.CountAsync(s => s.Status == SubmissionStatus.Pending);
    }

    public async Task<IActionResult> OnPostApproveAsync(int id)
    {
        var submission = await db.Submissions.FindAsync(id);
        if (submission == null) return NotFound();

        submission.Status = SubmissionStatus.Approved;

        db.ShameEntries.Add(new ShameEntry
        {
            Name = submission.Name,
            Website = submission.Website,
            Description = submission.Description,
            WhyShamed = submission.WhyShamed,
            ContactUrl = submission.ContactUrl,
            DateAdded = DateTime.UtcNow,
            IsActive = true
        });

        await db.SaveChangesAsync();
        TempData["Message"] = $"'{submission.Name}' approved and added to the list.";
        return RedirectToPage(new { status = "pending" });
    }

    public async Task<IActionResult> OnPostRejectAsync(int id)
    {
        var submission = await db.Submissions.FindAsync(id);
        if (submission == null) return NotFound();

        submission.Status = SubmissionStatus.Rejected;
        await db.SaveChangesAsync();
        TempData["Message"] = $"'{submission.Name}' rejected.";
        return RedirectToPage(new { status = "pending" });
    }
}
