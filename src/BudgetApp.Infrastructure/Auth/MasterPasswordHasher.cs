using System.Security.Cryptography;
using System.Text;

namespace BudgetApp.Infrastructure.Auth;

public sealed class MasterPasswordHasher
{
    private const int SaltSize = 16;
    private const int KeySize = 32;
    private const int DefaultIterations = 100_000;
    private const char Delimiter = '$';

    public string HashPassword(string password)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(password);

        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var key = Rfc2898DeriveBytes.Pbkdf2(password, salt, DefaultIterations, HashAlgorithmName.SHA256, KeySize);

        return string.Join(Delimiter,
            "pbkdf2-sha256",
            DefaultIterations.ToString(),
            Convert.ToBase64String(salt),
            Convert.ToBase64String(key));
    }

    public bool VerifyPassword(string passwordHash, string password)
    {
        if (string.IsNullOrWhiteSpace(passwordHash) || string.IsNullOrEmpty(password))
        {
            return false;
        }

        var segments = passwordHash.Split(Delimiter);
        if (segments.Length != 4 || segments[0] != "pbkdf2-sha256")
        {
            return false;
        }

        if (!int.TryParse(segments[1], out var iterations))
        {
            return false;
        }

        var salt = Convert.FromBase64String(segments[2]);
        var expectedKey = Convert.FromBase64String(segments[3]);
        var actualKey = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, expectedKey.Length);

        return CryptographicOperations.FixedTimeEquals(expectedKey, actualKey);
    }
}
