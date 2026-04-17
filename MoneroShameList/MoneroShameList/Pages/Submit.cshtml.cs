using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using MoneroShameList.Data;
using MoneroShameList.Models;

namespace MoneroShameList.Pages;

public class SubmitModel(AppDbContext db) : PageModel
{
    [BindProperty]
    public SubmitInput Input { get; set; } = new();

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync()
    {
        Input.ContactUrl = string.IsNullOrWhiteSpace(Input.ContactUrl) ? null : Input.ContactUrl.Trim();
        Input.MoneroAlternativeUrl = string.IsNullOrWhiteSpace(Input.MoneroAlternativeUrl) ? null : Input.MoneroAlternativeUrl.Trim();
        Input.SubmitterEmail = string.IsNullOrWhiteSpace(Input.SubmitterEmail) ? null : Input.SubmitterEmail.Trim();
        Input.Website = Input.Website?.Trim() ?? "";

        ModelState.Clear();
        if (!TryValidateModel(Input, nameof(Input)))
            return Page();

        var nameNorm = Input.Name.Trim().ToLower();
        var websiteNorm = Input.Website.ToLower().TrimEnd('/');

        bool duplicate = await db.ShameEntries.AnyAsync(e =>
            e.Name.ToLower() == nameNorm ||
            e.Website.ToLower().TrimEnd('/') == websiteNorm)
            || await db.Submissions.AnyAsync(s =>
                s.Status == SubmissionStatus.Pending &&
                (s.Name.ToLower() == nameNorm ||
                 s.Website.ToLower().TrimEnd('/') == websiteNorm));

        if (duplicate)
        {
            ModelState.AddModelError(string.Empty, "This company or website has already been submitted or is already on the list.");
            return Page();
        }

        db.Submissions.Add(new Submission
        {
            Name = Input.Name.Trim(),
            Website = Input.Website,
            Description = Input.Description.Trim(),
            WhyShamed = Input.WhyShamed.Trim(),
            ContactUrl = Input.ContactUrl,
            MoneroAlternativeUrl = Input.MoneroAlternativeUrl,
            SubmitterEmail = Input.SubmitterEmail,
            Category = Input.Category,
            SubmittedAt = DateTime.UtcNow,
            Status = SubmissionStatus.Pending
        });

        await db.SaveChangesAsync();
        TempData["Submitted"] = true;
        return RedirectToPage();
    }
}

public class SubmitInput
{
    [Required(ErrorMessage = "Company name is required.")]
    [MaxLength(200, ErrorMessage = "Company name must be 200 characters or fewer.")]
    public string Name { get; set; } = "";

    [Required(ErrorMessage = "Website is required.")]
    [MaxLength(500, ErrorMessage = "Website URL must be 500 characters or fewer.")]
    [Url(ErrorMessage = "Please enter a valid URL including https://.")]
    public string Website { get; set; } = "";

    public ShameCategory Category { get; set; } = ShameCategory.NeverAdded;

    [Required(ErrorMessage = "Description is required.")]
    [MaxLength(1000, ErrorMessage = "Description must be 1000 characters or fewer.")]
    public string Description { get; set; } = "";

    [Required(ErrorMessage = "Please explain why they should be shamed.")]
    [MaxLength(1000, ErrorMessage = "Must be 1000 characters or fewer.")]
    public string WhyShamed { get; set; } = "";

    [MaxLength(500, ErrorMessage = "URL must be 500 characters or fewer.")]
    [Url(ErrorMessage = "Please enter a valid URL including https://.")]
    public string? ContactUrl { get; set; }

    [MaxLength(500, ErrorMessage = "URL must be 500 characters or fewer.")]
    [Url(ErrorMessage = "Please enter a valid URL including https://.")]
    public string? MoneroAlternativeUrl { get; set; }

    [MaxLength(200, ErrorMessage = "Email must be 200 characters or fewer.")]
    [EmailAddress(ErrorMessage = "Please enter a valid email address.")]
    public string? SubmitterEmail { get; set; }
}
