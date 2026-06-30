using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using MoneroShameList.Data;
using MoneroShameList.Models;

namespace MoneroShameList.Pages;

public class IndexModel(AppDbContext db) : PageModel
{
    public const int PageSize = 50;

    public List<ShameEntry> Entries { get; set; } = [];
    public string Sort { get; set; } = "date";        // date | date-asc | name | name-desc
    public string Tab { get; set; } = "shame";         // shame | mixed | converted
    public string? CategoryFilter { get; set; }        // reason filter (shame tab only)
    public string ViewMode { get; set; } = "cards";    // cards | table
    public int Page { get; set; } = 1;
    public int TotalCount { get; set; }
    public int TotalPages { get; set; } = 1;

    // totals for the tab badges (independent of the active filter/paging)
    public int ShameCount { get; set; }
    public int MixedCount { get; set; }
    public int ConvertedCount { get; set; }

    private static readonly ShameCategory[] ShameCats =
    {
        ShameCategory.NeverAdded, ShameCategory.DelistedXmr,
        ShameCategory.AllowsOtherCrypto, ShameCategory.Scammer
    };

    // NOTE: the query key is "page", but the handler parameter must NOT be named "page"
    // — Razor Pages reserves "page" as a route token, so it never binds. Bind it explicitly.
    public async Task OnGetAsync(string? sort, string? tab, string? category, string? view,
        [FromQuery(Name = "page")] int pageNumber = 1)
    {
        Sort = sort is "name" or "name-desc" or "date-asc" ? sort : "date";
        Tab = tab is "mixed" or "converted" ? tab : "shame";
        ViewMode = view == "table" ? "table" : "cards";
        Page = pageNumber < 1 ? 1 : pageNumber;

        ShameCount = await db.ShameEntries.CountAsync(e => e.IsActive && ShameCats.Contains(e.Category));
        MixedCount = await db.ShameEntries.CountAsync(e => e.IsActive && e.Category == ShameCategory.MixedSupport);
        ConvertedCount = await db.ShameEntries.CountAsync(e => e.IsActive && e.Category == ShameCategory.Converted);

        IQueryable<ShameEntry> query = Tab switch
        {
            "mixed" => db.ShameEntries.Where(e => e.IsActive && e.Category == ShameCategory.MixedSupport),
            "converted" => db.ShameEntries.Where(e => e.IsActive && e.Category == ShameCategory.Converted),
            _ => db.ShameEntries.Where(e => e.IsActive && ShameCats.Contains(e.Category)),
        };

        // Reason filter only applies to the shame tab (the other tabs are a single category).
        if (Tab == "shame" && TryParseReason(category, out var reason))
        {
            CategoryFilter = reason.ToString();
            query = query.Where(e => e.Category == reason);
        }

        TotalCount = await query.CountAsync();
        TotalPages = Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize));
        if (Page > TotalPages) Page = TotalPages;

        query = Sort switch
        {
            "name" => query.OrderBy(e => e.Name.ToLower()),
            "name-desc" => query.OrderByDescending(e => e.Name.ToLower()),
            "date-asc" => query.OrderBy(e => e.DateAdded),
            _ => query.OrderByDescending(e => e.DateAdded),
        };

        Entries = await query.Skip((Page - 1) * PageSize).Take(PageSize).ToListAsync();
    }

    private static bool TryParseReason(string? s, out ShameCategory reason)
    {
        reason = default;
        if (string.IsNullOrWhiteSpace(s)) return false;
        return Enum.TryParse(s, ignoreCase: true, out reason) && ShameCats.Contains(reason);
    }
}
