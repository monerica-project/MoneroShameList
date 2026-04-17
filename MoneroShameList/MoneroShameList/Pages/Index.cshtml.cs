using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using MoneroShameList.Data;
using MoneroShameList.Models;

namespace MoneroShameList.Pages;

public class IndexModel(AppDbContext db) : PageModel
{
    public List<ShameEntry> Entries { get; set; } = [];
    public List<ShameEntry> Converted { get; set; } = [];
    public string Sort { get; set; } = "date";

    public async Task OnGetAsync(string? sort)
    {
        Sort = sort == "name" ? "name" : "date";

        var shameCategories = new[] {
            ShameCategory.NeverAdded,
            ShameCategory.DelistedXmr,
            ShameCategory.AllowsOtherCrypto,
            ShameCategory.Scammer
        };

        var query = db.ShameEntries.Where(e => e.IsActive && shameCategories.Contains(e.Category));

        Entries = Sort == "name"
            ? await query.OrderBy(e => e.Name).ToListAsync()
            : await query.OrderByDescending(e => e.DateAdded).ToListAsync();

        Converted = await db.ShameEntries
            .Where(e => e.IsActive && e.Category == ShameCategory.Converted)
            .OrderBy(e => e.Name)
            .ToListAsync();
    }
}