using System.Security.Cryptography;

namespace SchoolDesk.Core;

/// <summary>PBKDF2-HMAC-SHA256, случайная соль, сравнение за постоянное время.</summary>
public static class PasswordService
{
    const int Iterations = 210_000;
    const string Prefix = "pbkdf2-sha256";

    public static string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var h = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, 32);
        return $"{Prefix}${Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(h)}";
    }

    public static bool Verify(string password, string? stored)
    {
        if (string.IsNullOrEmpty(stored)) return false;
        var p = stored.Split('$');
        if (p.Length != 4 || p[0] != Prefix || !int.TryParse(p[1], out var it)) return false;
        try
        {
            var salt = Convert.FromBase64String(p[2]);
            var expected = Convert.FromBase64String(p[3]);
            var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, it, HashAlgorithmName.SHA256, expected.Length);
            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }
        catch { return false; }
    }

    public static string? Validate(string password) =>
        password.Length < 6 ? "Пароль должен быть не короче 6 символов." : null;
}

/// <summary>Защита от подбора: после 5 неудач задержка 30 с, далее удваивается (макс. 15 мин).</summary>
public static class Lockout
{
    static int _fails;
    static DateTime _until = DateTime.MinValue;
    static readonly object L = new();

    public static bool IsLocked(out TimeSpan left)
    {
        lock (L)
        {
            left = _until - DateTime.UtcNow;
            if (left > TimeSpan.Zero) return true;
            left = TimeSpan.Zero;
            return false;
        }
    }

    public static void Register(bool success)
    {
        lock (L)
        {
            if (success) { _fails = 0; return; }
            _fails++;
            if (_fails >= 5)
                _until = DateTime.UtcNow.AddSeconds(Math.Min(30 * Math.Pow(2, _fails - 5), 900));
        }
    }
}
