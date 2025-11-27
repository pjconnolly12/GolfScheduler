using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using MyApp.Data;
using MyApp.Models;

namespace MyApp.Pages.Entries
{
  public class CreateModel : PageModel
  {
    private readonly ApplicationDbContext _context;

    public CreateModel(ApplicationDbContext context)
    {
      _context = context;
    }

    [BindProperty]
    public Entry Entry { get; set; } = default!;

    public Round? SelectedRound { get; set; }

    public List<Player> Players { get; set; } = new();

    // --- GET: Load form ---
    public async Task<IActionResult> OnGetAsync(int? roundId, string? status = null)
    {
      if (roundId == null)
        return RedirectToPage("/Index"); // no round selected

      SelectedRound = await _context.Rounds
          .Include(r => r.Entries)
          .ThenInclude(e => e.Player)
          .FirstOrDefaultAsync(r => r.Id == roundId);

      if (SelectedRound == null)
        return NotFound();

      Players = await _context.Players
          .OrderBy(p => p.Name)
          .ToListAsync();

      Entry = new Entry
      {
        RoundId = roundId.Value,
        Status = status ?? "Confirmed"
      };

      return Page();
    }

    // --- POST: Create entry ---
    public async Task<IActionResult> OnPostAsync()
    {
      Entry.CreatedAt = DateTime.UtcNow;

      // Expiration logic: only "Maybe" entries expire
      if (Entry.Status.Equals("Maybe", StringComparison.OrdinalIgnoreCase))
        Entry.ExpiresAt = Entry.CreatedAt.AddHours(36);
      else
        Entry.ExpiresAt = null;

      _context.Entries.Add(Entry);

      // Update round's player count if not waitlist
      var round = await _context.Rounds.FindAsync(Entry.RoundId);
      if (round != null && !Entry.Status.Equals("Waitlist", StringComparison.OrdinalIgnoreCase))
      {
        int totalToAdd = 1 + (Entry.Guests ?? 0);
        round.Golfers += totalToAdd;
        _context.Rounds.Update(round);
      }

      await _context.SaveChangesAsync();
      return RedirectToPage("/Index");
    }

    // --- POST: Remove entry ---
    public async Task<IActionResult> OnPostRemoveAsync(int entryId)
    {
      var entry = await _context.Entries
          .Include(e => e.Round)
          .FirstOrDefaultAsync(e => e.Id == entryId);

      if (entry == null) return NotFound();

      await RemoveOrUpdateEntryAsync(entry);
      return RedirectToPage("/Index");
    }

    // --- POST: Update number of guests ---
    public async Task<IActionResult> OnPostUpdateGuestsAsync(int entryId, int newGuests)
    {
      var entry = await _context.Entries
          .Include(e => e.Round)
          .FirstOrDefaultAsync(e => e.Id == entryId);

      if (entry == null) return NotFound();

      await RemoveOrUpdateEntryAsync(entry, newGuests);
      return RedirectToPage("/Index");
    }

    // --- Cleanup expired entries ---
    public async Task RemoveExpiredEntriesAsync()
    {
      var expiredEntries = await _context.Entries
          .Where(e => e.Status == "Maybe" && e.ExpiresAt <= DateTime.UtcNow)
          .ToListAsync();

      foreach (var entry in expiredEntries)
        await RemoveOrUpdateEntryAsync(entry);
    }

    // --- Helper: Remove or update entry ---
    private async Task RemoveOrUpdateEntryAsync(Entry entry, int? newGuests = null)
    {
      if (entry == null) return;

      var round = await _context.Rounds.FindAsync(entry.RoundId);
      if (round == null) return;

      // Update guests
      if (newGuests.HasValue)
      {
        if (!entry.Status.Equals("Waitlist", StringComparison.OrdinalIgnoreCase))
        {
          int oldTotal = 1 + (entry.Guests ?? 0);
          int newTotal = 1 + newGuests.Value;
          round.Golfers = round.Golfers - oldTotal + newTotal;
        }

        entry.Guests = newGuests.Value;
        _context.Entries.Update(entry);
        await _context.SaveChangesAsync();
        return;
      }

      // Remove entry
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

    // --- Promote next waitlist player ---
    private async Task PromoteNextWaitlistPlayerAsync(int roundId)
    {
      var round = await _context.Rounds.FindAsync(roundId);
      if (round == null || round.Golfers >= 4)
        return;

      var nextWaitlist = await _context.Entries
          .Where(e => e.RoundId == roundId && e.Status == "Waitlist")
          .OrderBy(e => e.CreatedAt)
          .FirstOrDefaultAsync();

      if (nextWaitlist != null)
      {
        nextWaitlist.Status = "Confirmed";
        int totalToAdd = 1 + (nextWaitlist.Guests ?? 0);
        round.Golfers += totalToAdd;
        await _context.SaveChangesAsync();
      }
    }
  }
}