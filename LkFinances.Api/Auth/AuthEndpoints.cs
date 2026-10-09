using System.Security.Claims;
using LkFinances.Core.Data;
using LkFinances.Core.Domain;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace LkFinances.Api.Auth;

public sealed record LoginRequest(string Email, string Password);

public sealed record RegisterRequest(string Email, string Password);

public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);

public sealed record MeResponse(string Email);

public static class AuthEndpoints
{
    public const string AuthRateLimit = "auth";

    public static void MapAuth(this IEndpointRouteBuilder app)
    {
        var auth = app.MapGroup("/api/auth");

        auth.MapPost("/register", async (RegisterRequest request, FinanceDb db, IPasswordHasher<User> hasher, HttpContext http, CancellationToken ct) =>
        {
            if (await db.Users.AnyAsync(u => u.Email == User.NormalizeEmail(request.Email ?? ""), ct))
                return Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "Este email já está cadastrado.");

            var user = Accounts.CreateWithPrincipalLedger(db, hasher, request.Email, request.Password);
            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException ex) when (ex.IsUniqueViolation())
            {
                return Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "Este email já está cadastrado.");
            }

            await Accounts.SignInAsync(http, user);
            return Results.Created("/api/auth/me", new MeResponse(user.Email));
        }).RequireRateLimiting(AuthRateLimit);

        auth.MapPost("/login", async (LoginRequest request, FinanceDb db, IPasswordHasher<User> hasher, HttpContext http, CancellationToken ct) =>
        {
            var email = User.NormalizeEmail(request.Email ?? "");
            var user = await db.Users.SingleOrDefaultAsync(u => u.Email == email, ct);
            if (user is null || hasher.VerifyHashedPassword(user, user.PasswordHash, request.Password ?? "") == PasswordVerificationResult.Failed)
                return Results.Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Email ou senha inválidos.");

            await Accounts.SignInAsync(http, user);
            return Results.NoContent();
        }).RequireRateLimiting(AuthRateLimit);

        auth.MapPost("/logout", async (HttpContext http) =>
        {
            await http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Results.NoContent();
        });

        auth.MapGet("/me", (ClaimsPrincipal user) => new MeResponse(user.FindFirstValue(ClaimTypes.Email) ?? ""))
            .RequireAuthorization();

        auth.MapPost("/password", async (ChangePasswordRequest request, ClaimsPrincipal principal, FinanceDb db, IPasswordHasher<User> hasher, CancellationToken ct) =>
        {
            var user = await db.Users.SingleAsync(u => u.Id == principal.Id(), ct);
            if (hasher.VerifyHashedPassword(user, user.PasswordHash, request.CurrentPassword ?? "") == PasswordVerificationResult.Failed)
                return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Senha atual inválida.");

            User.EnsurePassword(request.NewPassword);
            user.SetPasswordHash(hasher.HashPassword(user, request.NewPassword));
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        }).RequireAuthorization();
    }
}
