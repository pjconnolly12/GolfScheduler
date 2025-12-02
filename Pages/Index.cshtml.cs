using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using MyApp.Data;
using MyApp.Models;
using Microsoft.AspNetCore.Identity;

public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public IndexModel(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    public List<Round> UpcomingRounds { get; set; } = new();

    public string? CurrentPlayerId { get; set; }

    public async Task OnGetAsync()
    {
        // -----------------------------
        // 🔥 Load current logged-in player
        // -----------------------------
        var user = await _userManager.GetUserAsync(User);

        if (user != null)
        {
            var player = await _context.Players
                .FirstOrDefaultAsync(p => p.Id == user.Id);

            CurrentPlayerId = player?.Id.ToString();
        }

        // --- 1. Remove expired entries ---
        var expiredEntries = await _context.Entries
            .Where(e => e.Status == "Maybe" && e.ExpiresAt <= DateTime.UtcNow)
            .ToListAsync();

        foreach (var entry in expiredEntries)
        {
            await RemoveOrUpdateEntryAsync(entry);
        }

        var upcomingRounds = await _context.Rounds
            .Include(r => r.Entries)
            .Where(r => r.Date >= DateTime.UtcNow)
            .ToListAsync();

        foreach (var round in upcomingRounds)
        {
            int currentPlayers = round.Entries
                .Where(e => !e.Status.Equals("Waitlist", StringComparison.OrdinalIgnoreCase) &&
                            (e.Status != "Maybe" || (e.ExpiresAt ?? DateTime.MaxValue) > DateTime.UtcNow))
                .Sum(e => 1 + (e.Guests ?? 0));

            // Promote waitlist entries
            while (currentPlayers < 4)
            {
                var nextWaitlist = round.Entries
                    .Where(e => e.Status == "Waitlist")
                    .OrderBy(e => e.CreatedAt)
                    .FirstOrDefault();

                if (nextWaitlist == null) break;

                nextWaitlist.Status = "Confirmed";
                int totalToAdd = 1 + (nextWaitlist.Guests ?? 0);
                currentPlayers += totalToAdd;
                round.Golfers += totalToAdd;

                _context.Entries.Update(nextWaitlist);
                _context.Rounds.Update(round);
                await _context.SaveChangesAsync();
            }
        }

        // Load updated rounds
        UpcomingRounds = await _context.Rounds
            .Include(r => r.Entries)
                .ThenInclude(e => e.Player)
            .Where(r => r.Date >= DateTime.UtcNow)
            .OrderBy(r => r.Date)
            .ToListAsync();
    }

    // --- Helper: remove entry and update player count ---
    private async Task RemoveOrUpdateEntryAsync(Entry entry)
    {
        if (entry == null) return;

        var round = await _context.Rounds.FindAsync(entry.RoundId);
        if (round == null) return;

        // Only adjust Golfers count for non-waitlist
        if (!entry.Status.Equals("Waitlist", StringComparison.OrdinalIgnoreCase))
        {
            int totalToRemove = 1 + (entry.Guests ?? 0);
            round.Golfers = Math.Max(0, round.Golfers - totalToRemove);
        }

        _context.Entries.Remove(entry);
        await _context.SaveChangesAsync();

        // Promote next waitlist player
        await PromoteNextWaitlistPlayerAsync(round.Id);
    }

    // --- Helper: promote next waitlist player ---
    private async Task PromoteNextWaitlistPlayerAsync(int roundId)
    {
        var round = await _context.Rounds
            .FirstOrDefaultAsync(r => r.Id == roundId);

        if (round == null) return;

        while (round.Golfers < 4)
        {
            var nextWaitlist = await _context.Entries
                .Where(e => e.RoundId == roundId && e.Status == "Waitlist")
                .OrderBy(e => e.CreatedAt)
                .FirstOrDefaultAsync();

            if (nextWaitlist == null) break;

            nextWaitlist.Status = "Confirmed";
            int totalToAdd = 1 + (nextWaitlist.Guests ?? 0);
            round.Golfers += totalToAdd;

            _context.Entries.Update(nextWaitlist);
            _context.Rounds.Update(round);

            await _context.SaveChangesAsync();
        }
    }
}

