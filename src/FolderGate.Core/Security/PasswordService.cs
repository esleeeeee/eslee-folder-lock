using System.Security.Cryptography;
using System.Text;
using FolderGate.Core.Models;

namespace FolderGate.Core.Security;

public sealed class PasswordService
{
    public const int DefaultIterations = 210_000;
    public const int MinimumPasswordLength = 4;
    private const int SaltSize = 32;
    private const int HashSize = 32;

    public PasswordRecord CreatePasswordRecord(string password)
    {
        ValidatePasswordInput(password);

        byte[] salt = RandomNumberGenerator.GetBytes(SaltSize);
        byte[] hash = HashPassword(password, salt, DefaultIterations);

        return new PasswordRecord
        {
            Iterations = DefaultIterations,
            SaltBase64 = Convert.ToBase64String(salt),
            HashBase64 = Convert.ToBase64String(hash)
        };
    }

    public bool Verify(string password, PasswordRecord? record)
    {
        if (record is null || string.IsNullOrWhiteSpace(record.SaltBase64) || string.IsNullOrWhiteSpace(record.HashBase64))
        {
            return false;
        }

        try
        {
            byte[] salt = Convert.FromBase64String(record.SaltBase64);
            byte[] expected = Convert.FromBase64String(record.HashBase64);
            byte[] actual = HashPassword(password, salt, record.Iterations);
            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }
        catch (FormatException)
        {
            return false;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static byte[] HashPassword(string password, byte[] salt, int iterations)
    {
        if (iterations < 100_000)
        {
            throw new ArgumentOutOfRangeException(nameof(iterations), "PBKDF2 iteration count is too low.");
        }

        return Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password),
            salt,
            iterations,
            HashAlgorithmName.SHA256,
            HashSize);
    }

    private static void ValidatePasswordInput(string password)
    {
        if (string.IsNullOrWhiteSpace(password))
        {
            throw new ArgumentException("비밀번호를 입력해야 합니다.", nameof(password));
        }

        if (password.Length < MinimumPasswordLength)
        {
            throw new ArgumentException($"비밀번호는 최소 {MinimumPasswordLength}자 이상이어야 합니다.", nameof(password));
        }
    }
}
