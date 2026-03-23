using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using MyApp.Data;
using MyApp.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Options;
using MyApp.Services;

namespace MyApp.Pages.Entries
{
  [Authorize]
  public class CreateModel : PageModel
  {
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IRoundNotificationEmailService _roundNotificationEmailService;
    private readonly RoundNotificationEmailOptions _roundNotificationEmailOptions;

    public CreateModel(
      ApplicationDbContext context,
      UserManager<ApplicationUser> userManager,
      IRoundNotificationEmailService roundNotificationEmailService,
      IOptions<RoundNotificationEmailOptions> roundNotificationEmailOptions)
    {
      _context = context;
      _userManager = userManager;
      _roundNotificationEmailService = roundNotificationEmailService;
      _roundNotificationEmailOptions = roundNotificationEmailOptions.Value;
    }

    [BindProperty]
    public Entry Entry { get; set; } = default!;

    public Round? SelectedRound { get; set; }

    public List<Player> Players { get; set; } = new();

    [TempData]
    public string? GuestUpdateErrorMessage { get; set; }

    // --- GET: Load form ---
    public async Task<IActionResult> OnGetAsync(int? roundId, string? status = null)
    {
      if (roundId == null)
        return RedirectToPage("/Index"); // no round selected

      SelectedRound = await LoadSelectedRoundAsync(roundId.Value);

      if (SelectedRound == null)
        return NotFound();

      var requestedStatus = status ?? "Confirmed";
      if (requestedStatus.Equals("Maybe", StringComparison.OrdinalIgnoreCase) && !CanUseMaybeStatus(SelectedRound.Date))
      {
        requestedStatus = "Confirmed";
        ModelState.AddModelError(string.Empty, "Maybe is unavailable within 48 hours of the round. Please join as Confirmed instead.");
      }

      Entry = new Entry
      {
        RoundId = roundId.Value,
        Status = requestedStatus
      };

      return Page();
    }

    // --- POST: Create entry ---
    public async Task<IActionResult> OnPostAsync()
    {
      var user = await _userManager.GetUserAsync(User);
      if (user == null)
      {
        return Challenge(); // Not logged in
      }
      var player = await GetOrCreatePlayerAsync(user);
      if (player == null)
      {
        return Forbid();
      }
      Entry.CreatedAt = DateTime.UtcNow;

      // Normalize guest count so it always persists and is counted when the entry is added
      Entry.Guests = Math.Max(0, Entry.Guests ?? 0);

      // Expiration logic: only "Maybe" entries expire
      if (Entry.Status.Equals("Maybe", StringComparison.OrdinalIgnoreCase))
        Entry.ExpiresAt = Entry.CreatedAt.AddHours(36);
      else
        Entry.ExpiresAt = null;

      // Get or create the logged-in user's Player record
      player = await _context.Players
        .FirstOrDefaultAsync(p => p.UserId == user.Id
                                 || (!string.IsNullOrEmpty(user.PlayerId) && p.Id == user.PlayerId)
                                 || (!string.IsNullOrEmpty(user.Email) && p.Email == user.Email));

      if (player == null)
      {
        player = new Player
        {
          UserId = user.Id,
          Name = user.UserName ?? "Unknown",
          Email = user.Email ?? string.Empty
        };

        _context.Players.Add(player);
        await _context.SaveChangesAsync();

        // Back-link ApplicationUser to the new Player if not already linked
        if (string.IsNullOrEmpty(user.PlayerId))
        {
          user.PlayerId = player.Id;
          await _userManager.UpdateAsync(user);
        }
      }

      // Force ownership
      Entry.PlayerId = player.Id;

      SelectedRound = await LoadSelectedRoundAsync(Entry.RoundId);
      if (SelectedRound == null)
      {
        return NotFound();
      }

      if (Entry.Status.Equals("Maybe", StringComparison.OrdinalIgnoreCase) && !CanUseMaybeStatus(SelectedRound.Date))
      {
        ModelState.AddModelError(nameof(Entry.Status), "Maybe is unavailable within 48 hours of the round. Please choose Confirmed.");
        return Page();
      }

      if (!Entry.Status.Equals("Waitlist", StringComparison.OrdinalIgnoreCase))
      {
        int totalToAdd = 1 + (Entry.Guests ?? 0);
        int currentPlayers = GetActiveGolferCount(SelectedRound);
        if (currentPlayers + totalToAdd > SelectedRound.PlayerLimit)
        {
          ModelState.AddModelError(string.Empty, $"This entry would put the round over {SelectedRound.PlayerLimit} players. Reduce the guest count or join the waitlist instead.");
          return Page();
        }

        SelectedRound.Golfers = currentPlayers + totalToAdd;
        _context.Rounds.Update(SelectedRound);
      }

      _context.Entries.Add(Entry);
      await _context.SaveChangesAsync();

      await SendEntryConfirmationNotificationAsync(SelectedRound, Entry, player);

      return RedirectToPage("/Index");
    }

    // --- POST: Remove entry ---
    public async Task<IActionResult> OnPostRemoveAsync(int entryId)
    {
      var entry = await _context.Entries
          .Include(e => e.Round)
          .FirstOrDefaultAsync(e => e.Id == entryId);

      if (entry == null) return NotFound();

      var player = await GetCurrentPlayerAsync();
      if (player == null || entry.PlayerId != player.Id)
        return Forbid();

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

      var player = await GetCurrentPlayerAsync();
      if (player == null || entry.PlayerId != player.Id)
        return Forbid();

      if (newGuests < 0)
      {
        GuestUpdateErrorMessage = "Guest count cannot be negative.";
        return RedirectToPage("/Index");
      }

      var round = await LoadSelectedRoundAsync(entry.RoundId);
      if (round == null)
      {
        return NotFound();
      }

      if (!entry.Status.Equals("Waitlist", StringComparison.OrdinalIgnoreCase))
      {
        int currentPlayers = GetActiveGolferCount(round);
        int oldTotal = 1 + (entry.Guests ?? 0);
        int newTotal = 1 + newGuests;

        if (currentPlayers - oldTotal + newTotal > round.PlayerLimit)
        {
          GuestUpdateErrorMessage = $"This update would put the round over {round.PlayerLimit} players. Reduce the guest count for this entry.";
          return RedirectToPage("/Index");
        }
      }

      await RemoveOrUpdateEntryAsync(entry, newGuests);
      return RedirectToPage("/Index");
    }

    // --- POST: Update entry status ---
    public async Task<IActionResult> OnPostUpdateStatusAsync(int entryId, string newStatus)
    {
      var entry = await _context.Entries
          .FirstOrDefaultAsync(e => e.Id == entryId);

      if (entry == null) return NotFound();

      var player = await GetCurrentPlayerAsync();
      if (player == null || entry.PlayerId != player.Id)
        return Forbid();

      if (!entry.Status.Equals("Maybe", StringComparison.OrdinalIgnoreCase) ||
          !newStatus.Equals("Confirmed", StringComparison.OrdinalIgnoreCase))
      {
        return RedirectToPage("/Index");
      }

      entry.Status = "Confirmed";
      entry.ExpiresAt = null;

      _context.Entries.Update(entry);
      await _context.SaveChangesAsync();

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

    private static int GetActiveGolferCount(Round round)
    {
      return round.Entries
          .Where(IsActiveEntry)
          .Sum(e => 1 + (e.Guests ?? 0));
    }

    private static bool IsActiveEntry(Entry entry)
    {
      return !entry.Status.Equals("Waitlist", StringComparison.OrdinalIgnoreCase)
          && (!entry.Status.Equals("Maybe", StringComparison.OrdinalIgnoreCase)
              || (entry.ExpiresAt ?? DateTime.MaxValue) > DateTime.UtcNow);
    }

    public bool MaybeStatusAllowed => SelectedRound != null && CanUseMaybeStatus(SelectedRound.Date);

    private static bool CanUseMaybeStatus(DateTime roundDate)
    {
      return roundDate > DateTime.Now.AddHours(48);
    }

    private async Task<Round?> LoadSelectedRoundAsync(int roundId)
    {
      return await _context.Rounds
          .Include(r => r.Entries)
          .ThenInclude(e => e.Player)
          .FirstOrDefaultAsync(r => r.Id == roundId);
    }

    private async Task<Player?> GetCurrentPlayerAsync()
    {
      var user = await _userManager.GetUserAsync(User);
      if (user == null)
        return null;

      return await _context.Players
        .FirstOrDefaultAsync(p => p.UserId == user.Id
                                  || (!string.IsNullOrEmpty(user.PlayerId) && p.Id == user.PlayerId)
                                  || (!string.IsNullOrEmpty(user.Email) && p.Email == user.Email));
    }

    private async Task<Player?> GetOrCreatePlayerAsync(ApplicationUser user)
    {
      var player = await _context.Players
        .FirstOrDefaultAsync(p => p.UserId == user.Id
                                  || (!string.IsNullOrEmpty(user.PlayerId) && p.Id == user.PlayerId)
                                  || (!string.IsNullOrEmpty(user.Email) && p.Email == user.Email));

      if (player != null)
      {
        return player;
      }

      player = new Player
      {
        UserId = user.Id,
        Name = user.UserName ?? "Unknown",
        Email = user.Email ?? string.Empty
      };

      _context.Players.Add(player);
      await _context.SaveChangesAsync();

      if (string.IsNullOrEmpty(user.PlayerId))
      {
        user.PlayerId = player.Id;
        await _userManager.UpdateAsync(user);
      }

      return player;
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
      if (round == null || round.Golfers >= round.PlayerLimit)
        return;

      var nextWaitlist = await _context.Entries
          .Include(e => e.Player)
          .Where(e => e.RoundId == roundId && e.Status == "Waitlist")
          .OrderBy(e => e.CreatedAt)
          .FirstOrDefaultAsync();

      if (nextWaitlist == null)
      {
        return;
      }

      int totalToAdd = 1 + (nextWaitlist.Guests ?? 0);
      if (round.Golfers + totalToAdd > round.PlayerLimit)
      {
        return;
      }

      nextWaitlist.Status = "Confirmed";
      round.Golfers += totalToAdd;
      await _context.SaveChangesAsync();

      await SendWaitlistPromotionNotificationAsync(roundId, nextWaitlist.Id);
    }

    private async Task SendWaitlistPromotionNotificationAsync(int roundId, int promotedEntryId)
    {
      var round = await _context.Rounds
          .Include(r => r.Entries)
          .ThenInclude(e => e.Player)
          .FirstOrDefaultAsync(r => r.Id == roundId);

      if (round == null)
      {
        return;
      }

      var promotedEntry = round.Entries.FirstOrDefault(e => e.Id == promotedEntryId);
      var recipientEmail = promotedEntry?.Player?.Email;
      if (promotedEntry == null || string.IsNullOrWhiteSpace(recipientEmail))
      {
        return;
      }

      var promotedPlayerName = GetPlayerDisplayName(promotedEntry.Player);
      var otherMembers = round.Entries
          .Where(e => e.Id != promotedEntryId && e.Status.Equals("Confirmed", StringComparison.OrdinalIgnoreCase))
          .Select(e => GetPlayerDisplayName(e.Player))
          .Where(name => !string.IsNullOrWhiteSpace(name))
          .Distinct(StringComparer.OrdinalIgnoreCase)
          .ToList();

      await _roundNotificationEmailService.SendWaitlistPromotionNotificationAsync(
          round,
          recipientEmail,
          promotedPlayerName,
          otherMembers,
          _roundNotificationEmailOptions.SiteUrl);
    }

    private async Task SendEntryConfirmationNotificationAsync(Round round, Entry entry, Player player)
    {
      if (string.IsNullOrWhiteSpace(player.Email))
      {
        return;
      }

      var playerDisplayName = GetPlayerDisplayName(player);
      await _roundNotificationEmailService.SendEntryConfirmationAsync(
          round,
          entry,
          player.Email,
          playerDisplayName,
          _roundNotificationEmailOptions.SiteUrl);
    }

    private static string GetPlayerDisplayName(Player? player)
    {
      if (player == null)
      {
        return "Unknown player";
      }

      if (!string.IsNullOrWhiteSpace(player.Name))
      {
        return player.Name;
      }

      return !string.IsNullOrWhiteSpace(player.Email) ? player.Email : "Unknown player";
    }
  }
}
