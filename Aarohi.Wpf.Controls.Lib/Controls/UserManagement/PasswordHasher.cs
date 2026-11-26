using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Aarohi.Wpf.Controls.Lib.Controls.UserManagement;

public static class PasswordHasher
{
    public static string ComputeSha256Hex(string value)
    {
        string input = value ?? string.Empty;
        byte[] data = Encoding.UTF8.GetBytes(input);
        byte[] hash = SHA256.HashData(data);

        StringBuilder builder = new(hash.Length * 2);
        foreach (byte b in hash)
        {
            builder.Append(b.ToString("x2", CultureInfo.InvariantCulture));
        }

        return builder.ToString();
    }

    public static bool IsDevPassword(string password, DateTime? now = null)
    {
        string expected = GetDevPassword(now);
        return string.Equals(password?.Trim(), expected, StringComparison.Ordinal);
    }

    public static string GetDevPassword(DateTime? now = null)
    {
        DateTime current = now ?? DateTime.Now;
        return current.ToString("ddMMyyyyHH", CultureInfo.InvariantCulture);
    }

    public static bool Sha256Matches(string plainTextPassword, string storedHash)
    {
        if (string.IsNullOrWhiteSpace(storedHash))
        {
            return false;
        }

        string computedHash = ComputeSha256Hex(plainTextPassword);
        return string.Equals(computedHash, storedHash.Trim(), StringComparison.OrdinalIgnoreCase);
    }
}
