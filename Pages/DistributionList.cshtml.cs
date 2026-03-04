using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MyApp.Pages;

public class DistributionListModel : PageModel
{
    public IActionResult OnGet()
    {
        return RedirectToPage("/Player/Distribution");
    }
}
