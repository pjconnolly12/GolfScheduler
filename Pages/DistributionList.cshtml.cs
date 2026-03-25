using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MyApp.Data;
using MyApp.Models;
using MyApp.Services;

namespace MyApp.Pages;

[Authorize]
public class DistributionListModel : PageModel
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoundOperationsOptions _roundOperationsOptions;

    public DistributionListModel(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        IOptions<RoundOperationsOptions> roundOperationsOptions)
    {
        _context = context;
        _userManager = userManager;
        _roundOperationsOptions = roundOperationsOptions.Value;
    }

    public List<UserDisplay> AvailableUsers { get; private set; } = new();
    public List<UserDisplay> CurrentMembers { get; private set; } = new();

    [BindProperty]
    public string? SelectedUserId { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser is null)
        {
            return Challenge();
        }
        if (!await CanManageDistributionListAsync(currentUser))
        {
            return Forbid();
        }

        await LoadListsAsync(currentUser.Id);
        return Page();
    }

    public async Task<IActionResult> OnPostAddAsync()
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser is null)
        {
            return Challenge();
        }
        if (!await CanManageDistributionListAsync(currentUser))
        {
            return Forbid();
        }

        if (string.IsNullOrWhiteSpace(SelectedUserId))
        {
            ModelState.AddModelError(string.Empty, "Please select a user to add.");
            await LoadListsAsync(currentUser.Id);
            return Page();
        }

        if (SelectedUserId == currentUser.Id)
        {
            ModelState.AddModelError(string.Empty, "You cannot add yourself to your distribution list.");
            await LoadListsAsync(currentUser.Id);
            return Page();
        }

        var exists = await _context.DistributionListMembers
            .AnyAsync(m => m.OwnerUserId == currentUser.Id && m.MemberUserId == SelectedUserId);

        if (!exists)
        {
            _context.DistributionListMembers.Add(new DistributionListMember
            {
                OwnerUserId = currentUser.Id,
                MemberUserId = SelectedUserId
            });

            await _context.SaveChangesAsync();
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostRemoveAsync(string memberUserId)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser is null)
        {
            return Challenge();
        }
        if (!await CanManageDistributionListAsync(currentUser))
        {
            return Forbid();
        }

        var member = await _context.DistributionListMembers
            .FirstOrDefaultAsync(m => m.OwnerUserId == currentUser.Id && m.MemberUserId == memberUserId);

        if (member is not null)
        {
            _context.DistributionListMembers.Remove(member);
            await _context.SaveChangesAsync();
        }

        return RedirectToPage();
    }

    private async Task LoadListsAsync(string ownerUserId)
    {
        var memberIds = await _context.DistributionListMembers
            .Where(m => m.OwnerUserId == ownerUserId)
            .Select(m => m.MemberUserId)
            .ToListAsync();

        CurrentMembers = await _userManager.Users
            .Where(u => memberIds.Contains(u.Id))
            .Select(u => new UserDisplay
            {
                UserId = u.Id,
                UserName = u.UserName ?? "(no username)",
                Email = u.Email ?? "(no email)"
            })
            .OrderBy(u => u.UserName)
            .ToListAsync();

        AvailableUsers = await _userManager.Users
            .Where(u => u.Id != ownerUserId && !memberIds.Contains(u.Id))
            .Select(u => new UserDisplay
            {
                UserId = u.Id,
                UserName = u.UserName ?? "(no username)",
                Email = u.Email ?? "(no email)"
            })
            .OrderBy(u => u.UserName)
            .ToListAsync();
    }

    private async Task<bool> CanManageDistributionListAsync(ApplicationUser user)
    {
        var requiredRole = _roundOperationsOptions.DistributionListManagerRole;
        if (string.IsNullOrWhiteSpace(requiredRole))
        {
            return false;
        }

        return await _userManager.IsInRoleAsync(user, requiredRole);
    }

    public class UserDisplay
    {
        public string UserId { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
    }
}
