using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using MyApp.Data;
using MyApp.Models;
using MyApp.Services;

namespace MyApp.Pages.Rounds;

[Authorize]
public class CreateModel : PageModel
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoundOperationsOptions _roundOperationsOptions;

    public CreateModel(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        IOptions<RoundOperationsOptions> roundOperationsOptions)
    {
        _context = context;
        _userManager = userManager;
        _roundOperationsOptions = roundOperationsOptions.Value;
    }

    [BindProperty]
    public RoundInput Input { get; set; } = new();

    public async Task<IActionResult> OnGetAsync()
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser is null)
        {
            return Challenge();
        }

        if (!await CanManageRoundsAsync(currentUser))
        {
            return Forbid();
        }

        Input.Date = DateTime.UtcNow.AddDays(1);
        Input.PlayerLimit = 4;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser is null)
        {
            return Challenge();
        }

        if (!await CanManageRoundsAsync(currentUser))
        {
            return Forbid();
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var round = new Round
        {
            Course = Input.Course.Trim(),
            Date = DateTime.SpecifyKind(Input.Date, DateTimeKind.Utc),
            Notes = string.IsNullOrWhiteSpace(Input.Notes) ? null : Input.Notes.Trim(),
            Holes = Input.Holes,
            PlayerLimit = Input.PlayerLimit,
            Golfers = 0,
            Organizer = currentUser.UserName ?? currentUser.Email ?? "Admin"
        };

        _context.Rounds.Add(round);
        await _context.SaveChangesAsync();

        return RedirectToPage("/Index");
    }

    private async Task<bool> CanManageRoundsAsync(ApplicationUser user)
    {
        var requiredRole = _roundOperationsOptions.RoundOrganizerRole;
        if (!_userManager.SupportsUserRole || string.IsNullOrWhiteSpace(requiredRole))
        {
            return false;
        }

        return await _userManager.IsInRoleAsync(user, requiredRole);
    }

    public class RoundInput
    {
        [Required]
        [StringLength(120)]
        public string Course { get; set; } = string.Empty;

        [Display(Name = "Start Date/Time (UTC)")]
        [Required]
        public DateTime Date { get; set; }

        [StringLength(500)]
        public string? Notes { get; set; }

        [Range(1, 18)]
        public int? Holes { get; set; }

        [Display(Name = "Player Limit")]
        [Range(1, 32)]
        public int PlayerLimit { get; set; } = 4;
    }
}
