using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using MoneroShameList.Data;
using MoneroShameList.Models;

namespace MoneroShameList.Pages.Admin;

[Authorize]
public class ListingsModel(AppDbContext db) : PageModel
{
    public List<ShameEntry> Entries { get; set; } = [];

    public async Task OnGetAsync()
    {
        Entries = await db.ShameEntries
            .OrderByDescending(e => e.DateAdded)
            .ToListAsync();
    }

    public async Task<IActionResult> OnPostToggleAsync(int id)
    {
        var entry = await db.ShameEntries.FindAsync(id);
        if (entry == null) return NotFound();

        entry.IsActive = !entry.IsActive;
        await SyncLinkedSubmissionAsync(entry.Name, entry.Website, entry.IsActive);

        await db.SaveChangesAsync();
        TempData["Message"] = $"'{entry.Name}' is now {(entry.IsActive ? "visible" : "hidden")}.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        var entry = await db.ShameEntries.FindAsync(id);
        if (entry == null) return NotFound();

        await SyncLinkedSubmissionAsync(entry.Name, entry.Website, isActive: false);

        db.ShameEntries.Remove(entry);
        await db.SaveChangesAsync();
        TempData["Message"] = $"'{entry.Name}' deleted.";
        return RedirectToPage();
    }

    private async Task SyncLinkedSubmissionAsync(string name, string website, bool isActive)
    {
        var nameNorm = name.ToLower();
        var websiteNorm = website.ToLower().TrimEnd('/');

        var submissions = await db.Submissions
            .Where(s => s.Name.ToLower() == nameNorm
                     || s.Website.ToLower().TrimEnd('/') == websiteNorm)
            .ToListAsync();

        var newStatus = isActive ? SubmissionStatus.Approved : SubmissionStatus.Rejected;
        foreach (var sub in submissions)
        {
            sub.Status = newStatus;
        }
    }
}