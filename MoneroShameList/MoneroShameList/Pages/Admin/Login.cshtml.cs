using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MoneroShameList.Pages.Admin;

public class LoginModel(IConfiguration config) : PageModel
{
    [BindProperty] public string Username { get; set; } = "";
    [BindProperty] public string Password { get; set; } = "";
    public string? Error { get; set; }

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync()
    {
        var adminUser = config["Admin:Username"] ?? "admin";
        var adminPass = config["Admin:Password"] ?? "changeme";

        if (Username != adminUser || Password != adminPass)
        {
            Error = "Invalid credentials.";
            return Page();
        }

        var claims = new List<Claim> { new(ClaimTypes.Name, Username), new(ClaimTypes.Role, "Admin") };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));

        return RedirectToPage("/Admin/Index");
    }

    public async Task<IActionResult> OnPostLogoutAsync()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToPage("/Index");
    }
}
