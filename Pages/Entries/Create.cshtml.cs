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
    private readonly AppDbContext _context;

    public CreateModel(AppDbContext context)
    {
      _context = context;
    }

    [BindProperty]
    public Entry Entry { get; set; } = default!;

    public Round? SelectedRound { get; set; }

    public List<Player> Players { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(int? roundId, string? status = null)
    {
      if (roundId == null)
      {
        return RedirectToPage("/Index"); // no round selected
      }

      SelectedRound = await _context.Rounds.FirstOrDefaultAsync(r => r.Id == roundId);

      if (SelectedRound == null)
      {
        return NotFound();
      }

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

    public async Task<IActionResult> OnPostAsync()
    {
      Entry.CreatedAt = DateTime.UtcNow;

      // Handle expiration logic
      if (Entry.Status.Equals("Maybe", StringComparison.OrdinalIgnoreCase))
      {
        Entry.ExpiresAt = Entry.CreatedAt.AddHours(36);
      }
      else
      {
        // Waitlist and Confirmed entries never expire
        Entry.ExpiresAt = null;
      }

      _context.Entries.Add(Entry);

      // Find related round
      var round = await _context.Rounds.FindAsync(Entry.RoundId);
      if (round != null)
      {
        // Only update player count if not Waitlist
        if (!Entry.Status.Equals("Waitlist", StringComparison.OrdinalIgnoreCase))
        {
          int totalToAdd = 1 + (Entry.Guests ?? 0);
          round.Golfers += totalToAdd;

          _context.Rounds.Update(round);
        }
      }

      await _context.SaveChangesAsync();
      return RedirectToPage("/Index");
    }
  }
}