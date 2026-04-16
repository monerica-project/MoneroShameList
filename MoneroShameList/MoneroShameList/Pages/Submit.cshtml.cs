using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MoneroShameList.Data;
using MoneroShameList.Models;

namespace MoneroShameList.Pages;

public class SubmitModel(AppDbContext db) : PageModel
{
    [BindProperty]
    public SubmitInput Input { get; set; } = new();

    public bool Submitted { get; set; }

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();

        db.Submissions.Add(new Submission
        {
            Name = Input.Name,
            Website = Input.Website,
            Description = Input.Description,
            WhyShamed = Input.WhyShamed,
            ContactUrl = string.IsNullOrWhiteSpace(Input.ContactUrl) ? null : Input.ContactUrl,
            SubmitterEmail = string.IsNullOrWhiteSpace(Input.SubmitterEmail) ? null : Input.SubmitterEmail,
            SubmittedAt = DateTime.UtcNow,
            Status = SubmissionStatus.Pending
        });

        await db.SaveChangesAsync();
        Submitted = true;
        return Page();
    }
}

public class SubmitInput
{
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

    [MaxLength(200), EmailAddress]
    public string? SubmitterEmail { get; set; }
}
