using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using MyApp.Data;
using MyApp.Models;

public class CreateModel : PageModel
{
  private readonly AppDbContext _context;

  public CreateModel(AppDbContext context)
  {
    _context = context;
  }

  [BindProperty]
  public Entry Entry { get; set; } = new();

  public SelectList Players { get; set; } = default!;
  public SelectList Rounds { get; set; } = default!;

  public async Task OnGetAsync()
  {
    Players = new SelectList(await _context.Players.ToListAsync(), "Id", "Name");
    Rounds = new SelectList(await _context.Rounds.ToListAsync(), "Id", "Course");
  }

  public async Task<IActionResult> OnPostAsync()
  {
    if (!ModelState.IsValid)
    {
      Players = new SelectList(await _context.Players.ToListAsync(), "Id", "Name");
      Rounds = new SelectList(await _context.Rounds.ToListAsync(), "Id", "Course");
      return Page();
    }

    // Validate Guests <= 2
    if (Entry.Guests.HasValue && Entry.Guests > 2)
    {
      ModelState.AddModelError("Entry.Guests", "You can bring a maximum of 2 guests.");
      return Page();
    }

    _context.Entries.Add(Entry);
    await _context.SaveChangesAsync();
    return RedirectToPage("/Entries/Index");
  }
}
