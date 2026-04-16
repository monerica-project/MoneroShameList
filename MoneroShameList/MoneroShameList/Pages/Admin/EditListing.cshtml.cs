using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MoneroShameList.Data;
using MoneroShameList.Models;

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
            IsActive = entry.IsActive
        };

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();

        if (Input.Id == 0)
        {
            db.ShameEntries.Add(new ShameEntry
            {
                Name = Input.Name,
                Website = Input.Website,
                Description = Input.Description,
                WhyShamed = Input.WhyShamed,
                ContactUrl = string.IsNullOrWhiteSpace(Input.ContactUrl) ? null : Input.ContactUrl,
                IsActive = Input.IsActive,
                DateAdded = DateTime.UtcNow
            });
        }
        else
        {
            var entry = await db.ShameEntries.FindAsync(Input.Id);
            if (entry == null) return NotFound();

            entry.Name = Input.Name;
            entry.Website = Input.Website;
            entry.Description = Input.Description;
            entry.WhyShamed = Input.WhyShamed;
            entry.ContactUrl = string.IsNullOrWhiteSpace(Input.ContactUrl) ? null : Input.ContactUrl;
            entry.IsActive = Input.IsActive;
        }

        await db.SaveChangesAsync();
        TempData["Message"] = Input.Id == 0 ? $"'{Input.Name}' added." : $"'{Input.Name}' updated.";
        return RedirectToPage("/Admin/Listings");
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

    public bool IsActive { get; set; } = true;
}
