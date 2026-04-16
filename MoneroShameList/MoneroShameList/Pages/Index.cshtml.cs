using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using MoneroShameList.Data;
using MoneroShameList.Models;

namespace MoneroShameList.Pages;

public class IndexModel(AppDbContext db) : PageModel
{
    public List<ShameEntry> Entries { get; set; } = [];
    public string Sort { get; set; } = "date";

    public async Task OnGetAsync(string? sort)
    {
        Sort = sort == "name" ? "name" : "date";

        var query = db.ShameEntries.Where(e => e.IsActive);

        Entries = Sort == "name"
            ? await query.OrderBy(e => e.Name).ToListAsync()
            : await query.OrderByDescending(e => e.DateAdded).ToListAsync();
    }
}
