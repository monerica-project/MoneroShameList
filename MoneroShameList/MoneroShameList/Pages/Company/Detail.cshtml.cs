using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using MoneroShameList.Data;
using MoneroShameList.Models;

namespace MoneroShameList.Pages.Company;

public class DetailModel(AppDbContext db) : PageModel
{
    public ShameEntry Entry { get; set; } = null!;

    public async Task<IActionResult> OnGetAsync(string slug)
    {
        var entry = await db.ShameEntries
            .FirstOrDefaultAsync(e => e.Slug == slug && e.IsActive);

        if (entry == null) return NotFound();

        Entry = entry;
        return Page();
    }
}