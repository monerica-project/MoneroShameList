using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using MoneroShameList.Data;
using MoneroShameList.Helpers;
using MoneroShameList.Models;
using System.ComponentModel.DataAnnotations;

namespace MoneroShameList.Pages.Admin;

[Authorize]
public class EditListingModel(AppDbContext db) : PageModel
{
    [BindProperty]
    public EntryInput Input { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(int? id)
    {
        if (id == null) return Page();

        var entry = await db.ShameEntries.FindAsync(id);
        if (entry == null) return NotFound();

        Input = new EntryInput
        {
            Id = entry.Id,
            Name = entry.Name,
            Website = entry.Website,
            Description = entry.Description,
            WhyShamed = entry.WhyShamed,
            ContactUrl = entry.ContactUrl,
            MoneroAlternativeUrl = entry.MoneroAlternativeUrl,
            AdminComment = entry.AdminComment,
            Category = entry.Category,
            IsActive = entry.IsActive
        };

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();

        var nameNorm = Input.Name.Trim().ToLower();
        var websiteNorm = Input.Website.ToLower().TrimEnd('/');

        bool duplicate = await db.ShameEntries.AnyAsync(e =>
            e.Id != Input.Id &&
            (e.Name.ToLower() == nameNorm || e.Website.ToLower().TrimEnd('/') == websiteNorm));

        if (duplicate)
        {
            ModelState.AddModelError(string.Empty, "Another listing already exists with this name or website.");
            return Page();
        }

        if (Input.Id == 0)
        {
            db.ShameEntries.Add(new ShameEntry
            {
                Slug = await UniqueSlugAsync(SlugHelper.Generate(Input.Name), 0),
                Name = Input.Name.Trim(),
                Website = Input.Website.Trim(),
                Description = Input.Description.Trim(),
                WhyShamed = Input.WhyShamed.Trim(),
                ContactUrl = string.IsNullOrWhiteSpace(Input.ContactUrl) ? null : Input.ContactUrl.Trim(),
                MoneroAlternativeUrl = string.IsNullOrWhiteSpace(Input.MoneroAlternativeUrl) ? null : Input.MoneroAlternativeUrl.Trim(),
                AdminComment = string.IsNullOrWhiteSpace(Input.AdminComment) ? null : Input.AdminComment.Trim(),
                Category = Input.Category,
                IsActive = Input.IsActive,
                DateAdded = DateTime.UtcNow
            });
        }
        else
        {
            var entry = await db.ShameEntries.FindAsync(Input.Id);
            if (entry == null) return NotFound();
            
            entry.Slug = await UniqueSlugAsync(SlugHelper.Generate(Input.Name), entry.Id);
            entry.Name = Input.Name.Trim();
            entry.Website = Input.Website.Trim();
            entry.Description = Input.Description.Trim();
            entry.WhyShamed = Input.WhyShamed.Trim();
            entry.ContactUrl = string.IsNullOrWhiteSpace(Input.ContactUrl) ? null : Input.ContactUrl.Trim();
            entry.MoneroAlternativeUrl = string.IsNullOrWhiteSpace(Input.MoneroAlternativeUrl) ? null : Input.MoneroAlternativeUrl.Trim();
            entry.AdminComment = string.IsNullOrWhiteSpace(Input.AdminComment) ? null : Input.AdminComment.Trim();
            entry.Category = Input.Category;
            entry.IsActive = Input.IsActive;
        }

        await db.SaveChangesAsync();
        TempData["Message"] = Input.Id == 0 ? $"'{Input.Name}' added." : $"'{Input.Name}' updated.";
        return RedirectToPage("/Admin/Listings");
    }

    private async Task<string> UniqueSlugAsync(string baseSlug, int excludeId)
    {
        var slug = baseSlug;
        var i = 2;
        while (await db.ShameEntries.AnyAsync(e => e.Slug == slug && e.Id != excludeId))
        {
            slug = $"{baseSlug}-{i++}";
        }
        return slug;
    }
}

public class EntryInput
{
    public int Id { get; set; }

    [Required, MaxLength(200)]
    public string Name { get; set; } = "";

    [Required, MaxLength(500), Url]
    public string Website { get; set; } = "";

    [Required, MaxLength(1000)]
    public string Description { get; set; } = "";

    [Required, MaxLength(1000)]
    public string WhyShamed { get; set; } = "";

    [MaxLength(500), Url]
    public string? ContactUrl { get; set; }

    [MaxLength(500), Url]
    public string? MoneroAlternativeUrl { get; set; }

    public string? AdminComment { get; set; }

    public ShameCategory Category { get; set; } = ShameCategory.NeverAdded;

    public bool IsActive { get; set; } = true;
}
