using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using MoneroShameList.Data;
using MoneroShameList.Helpers;
using MoneroShameList.Models;

namespace MoneroShameList.Pages.Admin;

[Authorize]
public class EditSubmissionModel(AppDbContext db) : PageModel
{
    [BindProperty]
    public SubmissionInput Input { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var s = await db.Submissions.FindAsync(id);
        if (s == null) return NotFound();

        Input = new SubmissionInput
        {
            Id = s.Id,
            Name = s.Name,
            Website = s.Website,
            Description = s.Description,
            WhyShamed = s.WhyShamed,
            ContactUrl = s.ContactUrl,
            MoneroAlternativeUrl = s.MoneroAlternativeUrl,
            AdminNote = s.AdminNote,
            SubmitterEmail = s.SubmitterEmail,
            Category = s.Category,
            Status = s.Status.ToString()
        };

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();

        var s = await db.Submissions.FindAsync(Input.Id);
        if (s == null) return NotFound();

        s.Name = Input.Name.Trim();
        s.Website = Input.Website.Trim();
        s.Description = Input.Description.Trim();
        s.WhyShamed = Input.WhyShamed.Trim();
        s.ContactUrl = string.IsNullOrWhiteSpace(Input.ContactUrl) ? null : Input.ContactUrl.Trim();
        s.MoneroAlternativeUrl = string.IsNullOrWhiteSpace(Input.MoneroAlternativeUrl) ? null : Input.MoneroAlternativeUrl.Trim();
        s.AdminNote = string.IsNullOrWhiteSpace(Input.AdminNote) ? null : Input.AdminNote.Trim();
        s.SubmitterEmail = string.IsNullOrWhiteSpace(Input.SubmitterEmail) ? null : Input.SubmitterEmail.Trim();
        s.Category = Input.Category;
        s.Status = Enum.Parse<SubmissionStatus>(Input.Status);

        // Keep the linked ShameEntry consistent with the submission's status.
        var entry = await db.ShameEntries.FirstOrDefaultAsync(e =>
            e.Name.ToLower() == s.Name.ToLower() ||
            e.Website.ToLower().TrimEnd('/') == s.Website.ToLower().TrimEnd('/'));

        if (s.Status == SubmissionStatus.Approved)
        {
            if (entry != null)
            {
                // Sync content AND make sure it's visible.
                entry.Name = s.Name;
                entry.Website = s.Website;
                entry.Description = s.Description;
                entry.WhyShamed = s.WhyShamed;
                entry.ContactUrl = s.ContactUrl;
                entry.MoneroAlternativeUrl = s.MoneroAlternativeUrl;
                entry.Category = s.Category;
                entry.IsActive = true;
            }
            else
            {
                // No matching entry — create one (covers Pending → Approved via this page).
                var baseSlug = SlugHelper.Generate(s.Name);
                var slug = baseSlug;
                var i = 2;
                while (await db.ShameEntries.AnyAsync(e => e.Slug == slug))
                    slug = $"{baseSlug}-{i++}";

                db.ShameEntries.Add(new ShameEntry
                {
                    Name = s.Name,
                    Website = s.Website,
                    Description = s.Description,
                    WhyShamed = s.WhyShamed,
                    ContactUrl = s.ContactUrl,
                    MoneroAlternativeUrl = s.MoneroAlternativeUrl,
                    Category = s.Category,
                    Slug = slug,
                    DateAdded = DateTime.UtcNow,
                    IsActive = true
                });
            }
        }
        else
        {
            // Rejected or Pending → hide from the public list.
            if (entry != null)
            {
                entry.IsActive = false;
            }
        }

        await db.SaveChangesAsync();
        TempData["Message"] = $"'{s.Name}' updated.";
        return RedirectToPage("/Admin/Index", new { status = Input.Status.ToLower() });
    }
}

public class SubmissionInput
{
    public int Id { get; set; }

    [Required]
    public string Status { get; set; } = "Pending";

    [Required, MaxLength(200)]
    public string Name { get; set; } = "";

    [Required, MaxLength(500)]
    public string Website { get; set; } = "";

    [Required, MaxLength(1000)]
    public string Description { get; set; } = "";

    [Required, MaxLength(1000)]
    public string WhyShamed { get; set; } = "";

    [MaxLength(500)]
    public string? ContactUrl { get; set; }

    [MaxLength(500)]
    public string? MoneroAlternativeUrl { get; set; }

    public string? AdminNote { get; set; }

    [MaxLength(200), EmailAddress]
    public string? SubmitterEmail { get; set; }

    public ShameCategory Category { get; set; } = ShameCategory.NeverAdded;
}