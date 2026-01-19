using System.Runtime.CompilerServices;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using WebFileBrowser.Models;
using WebFileBrowser.Services;

namespace WebFileBrowser.Controllers;

[AllowAnonymous]
public class AccountController : Controller
{
    private readonly IUserAuthenticationService _userAuthenticationService;

    public AccountController(IUserAuthenticationService userAuthenticationService)
    {
        this._userAuthenticationService = userAuthenticationService;
    }

    public IActionResult LoginAsync()
    {
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> LoginAsync(LoginViewModel loginRequest, string returnUrl)
    {
        if(!_userAuthenticationService.Authenticate(loginRequest.Username, loginRequest.Password))
        {
            return View();
        }

        var claims = new List<Claim>()
        {
            new Claim(ClaimTypes.Name, loginRequest.Username),
            // new Claim("FullName", "Andrew Person"),
            new Claim(ClaimTypes.Role, "Administrator"),
        };

        var claimsIdentity = new ClaimsIdentity(
            claims, CookieAuthenticationDefaults.AuthenticationScheme);

        var authProperties = new AuthenticationProperties
        {
            //AllowRefresh = <bool>,
            // Refreshing the authentication session should be allowed.

            //ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(10),
            // The time at which the authentication ticket expires. A 
            // value set here overrides the ExpireTimeSpan option of 
            // CookieAuthenticationOptions set with AddCookie.

            //IsPersistent = true,
            // Whether the authentication session is persisted across 
            // multiple requests. When used with cookies, controls
            // whether the cookie's lifetime is absolute (matching the
            // lifetime of the authentication ticket) or session-based.

            //IssuedUtc = <DateTimeOffset>,
            // The time at which the authentication ticket was issued.

            //RedirectUri = <string>
            // The full path or absolute URI to be used as an http 
            // redirect response value.
        };

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme, 
            new ClaimsPrincipal(claimsIdentity), 
            authProperties);
        
        if(!string.IsNullOrEmpty(returnUrl)){
            return LocalRedirect(returnUrl);
        }
        else
        {
            return RedirectToAction("Index", "Home");
        }
    }

    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync();
        return LocalRedirect("/");
    }
}