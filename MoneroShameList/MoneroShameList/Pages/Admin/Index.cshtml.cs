using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using MoneroShameList.Data;
using MoneroShameList.Helpers;
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

        var baseSlug = SlugHelper.Generate(submission.Name);
        var slug = baseSlug;
        var i = 2;
        while (await db.ShameEntries.AnyAsync(e => e.Slug == slug))
            slug = $"{baseSlug}-{i++}";

        db.ShameEntries.Add(new ShameEntry
        {
            Name = submission.Name,
            Website = submission.Website,
            Description = submission.Description,
            WhyShamed = submission.WhyShamed,
            ContactUrl = submission.ContactUrl,
            MoneroAlternativeUrl = submission.MoneroAlternativeUrl,
            Category = submission.Category,
            Slug = slug,
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

    public async Task<IActionResult> OnPostReApproveAsync(int id)
    {
        var submission = await db.Submissions.FindAsync(id);
        if (submission == null) return NotFound();

        submission.Status = SubmissionStatus.Approved;

        bool alreadyExists = await db.ShameEntries.AnyAsync(e =>
            e.Name.ToLower() == submission.Name.ToLower() ||
            e.Website.ToLower().TrimEnd('/') == submission.Website.ToLower().TrimEnd('/'));

        if (!alreadyExists)
        {
            var baseSlug = SlugHelper.Generate(submission.Name);
            var slug = baseSlug;
            var i = 2;
            while (await db.ShameEntries.AnyAsync(e => e.Slug == slug))
                slug = $"{baseSlug}-{i++}";

            db.ShameEntries.Add(new ShameEntry
            {
                Name = submission.Name,
                Website = submission.Website,
                Description = submission.Description,
                WhyShamed = submission.WhyShamed,
                ContactUrl = submission.ContactUrl,
                MoneroAlternativeUrl = submission.MoneroAlternativeUrl,
                Category = submission.Category,
                Slug = slug,
                DateAdded = DateTime.UtcNow,
                IsActive = true
            });
        }

        await db.SaveChangesAsync();
        TempData["Message"] = $"'{submission.Name}' approved.";
        return RedirectToPage(new { status = "rejected" });
    }
}