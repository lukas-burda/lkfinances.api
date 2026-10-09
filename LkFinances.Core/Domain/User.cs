namespace LkFinances.Core.Domain;

public class User
{
    public const int MinPasswordLength = 8;

    private User() { }

    public User(string email)
    {
        EnsureEmail(email);
        Id = Guid.CreateVersion7();
        Email = NormalizeEmail(email);
    }

    public Guid Id { get; private set; }
    public string Email { get; private set; } = "";
    public string PasswordHash { get; private set; } = "";

    public void SetPasswordHash(string hash) => PasswordHash = hash;

    public static string NormalizeEmail(string email) => (email ?? "").Trim().ToLowerInvariant();

    public static void EnsureEmail(string? email)
    {
        var normalized = NormalizeEmail(email ?? "");
        var at = normalized.IndexOf('@');
        if (at <= 0 || at == normalized.Length - 1 || normalized.IndexOf('@', at + 1) >= 0)
            throw new DomainException(nameof(Email), "Informe um email válido.");
    }

    public static void EnsurePassword(string? password)
    {
        if (string.IsNullOrEmpty(password) || password.Length < MinPasswordLength)
            throw new DomainException("Password", $"A senha deve ter pelo menos {MinPasswordLength} caracteres.");
    }
}
