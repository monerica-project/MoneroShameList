using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using MoneroShameList.Data;
using MoneroShameList.Models;

namespace MoneroShameList.Pages;

public class IndexModel(AppDbContext db) : PageModel
{
    public List<ShameEntry> Entries { get; set; } = [];
    public List<ShameEntry> MixedSupport { get; set; } = [];
    public List<ShameEntry> Converted { get; set; } = [];
    public string Sort { get; set; } = "date";
    public string Tab { get; set; } = "shame";

    public async Task OnGetAsync(string? sort, string? tab)
    {
        Sort = sort == "name" ? "name" : "date";
        Tab = tab is "mixed" or "converted" ? tab : "shame";

        var shameCategories = new[] {
            ShameCategory.NeverAdded,
            ShameCategory.DelistedXmr,
            ShameCategory.AllowsOtherCrypto,
            ShameCategory.Scammer
        };

        var shameQuery = db.ShameEntries.Where(e => e.IsActive && shameCategories.Contains(e.Category));
        Entries = Sort == "name"
            ? await shameQuery.OrderBy(e => e.Name).ToListAsync()
            : await shameQuery.OrderByDescending(e => e.DateAdded).ToListAsync();

        var mixedQuery = db.ShameEntries.Where(e => e.IsActive && e.Category == ShameCategory.MixedSupport);
        MixedSupport = Sort == "name"
            ? await mixedQuery.OrderBy(e => e.Name).ToListAsync()
            : await mixedQuery.OrderByDescending(e => e.DateAdded).ToListAsync();

        Converted = await db.ShameEntries
            .Where(e => e.IsActive && e.Category == ShameCategory.Converted)
            .OrderBy(e => e.Name)
            .ToListAsync();
    }
}