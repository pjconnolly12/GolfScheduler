using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using MyApp.Data;
using MyApp.Models;
public class IndexModel : PageModel
{
    private readonly AppDbContext _context;

    public IndexModel(AppDbContext context)
    {
        _context = context;
    }

    public List<Round> UpcomingRounds { get; set; } = new();

    public async Task OnGetAsync()
    {
        UpcomingRounds = await _context.Rounds
            .Where(r => r.Date >= DateTime.Today)
            .OrderBy(r => r.Date)
            .Include(r => r.Entries)
                .ThenInclude(e => e.Player)
            .ToListAsync();
    }
}

