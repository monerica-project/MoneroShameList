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
        await db.SaveChangesAsync();
        TempData["Message"] = $"'{entry.Name}' is now {(entry.IsActive ? "visible" : "hidden")}.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        var entry = await db.ShameEntries.FindAsync(id);
        if (entry == null) return NotFound();
        db.ShameEntries.Remove(entry);
        await db.SaveChangesAsync();
        TempData["Message"] = $"'{entry.Name}' deleted.";
        return RedirectToPage();
    }
}
