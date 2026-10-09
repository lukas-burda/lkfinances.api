using System.Security.Claims;
using LkFinances.Core.Data;
using LkFinances.Core.Domain;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;

namespace LkFinances.Api.Auth;

public static class Accounts
{
    public static User CreateWithPrincipalLedger(FinanceDb db, IPasswordHasher<User> hasher, string email, string password)
    {
        User.EnsurePassword(password);
        var user = new User(email);
        user.SetPasswordHash(hasher.HashPassword(user, password));
        db.Users.Add(user);
        db.Ledgers.Add(new Ledger(user.Id, Ledger.DefaultName));
        return user;
    }

    public static Task SignInAsync(HttpContext http, User user)
    {
        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()), new Claim(ClaimTypes.Email, user.Email)],
            CookieAuthenticationDefaults.AuthenticationScheme);
        return http.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));
    }
}
