using System;
using System.Security.Cryptography;
using System.Text;

namespace Aarohi.UserManagment
{
    /// <summary>
    /// Provides cryptographic password hashing and verification services conforming to NIST SP 800-63B.
    /// Uses PBKDF2 with HMAC-SHA256, per-user 128-bit cryptographically secure random salt,
    /// constant-time subkey comparison, and bounded iteration limits to prevent algorithmic DoS.
    /// Also supports transparent backward-compatible migration for legacy SHA-256 and plaintext records.
    /// </summary>
    public static class PasswordSecurity
    {
        public const int SaltSizeBytes = 16;       // 128-bit salt
        public const int KeySizeBytes = 32;        // 256-bit derived key
        public const int DefaultIterations = 100000;
        public const int MinAllowedIterations = 100000;
        public const int MaxAllowedIterations = 500000; // Algorithmic DoS prevention ceiling
        private const string FormatPrefix = "$PBKDF2$V1$";

        public enum VerificationResult
        {
            Failed,
            Success,
            SuccessRehashNeeded,
            MalformedRecord
        }

        /// <summary>
        /// Hashes a plaintext password using PBKDF2 with HMAC-SHA256 and a cryptographically secure random salt.
        /// </summary>
        /// <param name="password">Plaintext password to hash.</param>
        /// <param name="iterations">Iteration count (defaults to 100,000).</param>
        /// <returns>Formatted password verifier string: $PBKDF2$V1$iterations$salt$hash</returns>
        public static string HashPassword(string password, int iterations = DefaultIterations)
        {
            if (string.IsNullOrEmpty(password))
                throw new ArgumentNullException(nameof(password));

            if (iterations < MinAllowedIterations || iterations > MaxAllowedIterations)
                throw new ArgumentOutOfRangeException(nameof(iterations), $"Iterations must be between {MinAllowedIterations} and {MaxAllowedIterations}.");

            byte[] salt = RandomNumberGenerator.GetBytes(SaltSizeBytes);
            byte[] subkey = Rfc2898DeriveBytes.Pbkdf2(
                password,
                salt,
                iterations,
                HashAlgorithmName.SHA256,
                KeySizeBytes);

            return $"{FormatPrefix}{iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(subkey)}";
        }

        /// <summary>
        /// Verifies a user-entered password against a stored verifier string in constant time.
        /// Supports standard PBKDF2 format, as well as legacy SHA-256 and plaintext records for transparent rehash.
        /// </summary>
        /// <param name="enteredPassword">The plaintext password entered by the user.</param>
        /// <param name="storedHash">The verifier string stored in the database or credential store.</param>
        /// <returns>A <see cref="VerificationResult"/> indicating success, failure, or rehash necessity.</returns>
        public static VerificationResult VerifyPassword(string enteredPassword, string storedHash)
        {
            if (string.IsNullOrEmpty(enteredPassword) || string.IsNullOrEmpty(storedHash))
                return VerificationResult.Failed;

            // 1. Standard PBKDF2 verification
            if (storedHash.StartsWith(FormatPrefix, StringComparison.Ordinal))
            {
                try
                {
                    string[] parts = storedHash.Split('$');
                    if (parts.Length != 6)
                        return VerificationResult.MalformedRecord;

                    if (!int.TryParse(parts[3], out int iterations))
                        return VerificationResult.MalformedRecord;

                    // Bounds check iteration count to prevent CPU exhaustion DoS
                    if (iterations < MinAllowedIterations || iterations > MaxAllowedIterations)
                        return VerificationResult.MalformedRecord;

                    byte[] salt = Convert.FromBase64String(parts[4]);
                    byte[] expectedSubkey = Convert.FromBase64String(parts[5]);

                    if (salt.Length != SaltSizeBytes || expectedSubkey.Length != KeySizeBytes)
                        return VerificationResult.MalformedRecord;

                    byte[] actualSubkey = Rfc2898DeriveBytes.Pbkdf2(
                        enteredPassword,
                        salt,
                        iterations,
                        HashAlgorithmName.SHA256,
                        KeySizeBytes);

                    if (CryptographicOperations.FixedTimeEquals(actualSubkey, expectedSubkey))
                    {
                        return iterations < DefaultIterations
                            ? VerificationResult.SuccessRehashNeeded
                            : VerificationResult.Success;
                    }

                    return VerificationResult.Failed;
                }
                catch (FormatException)
                {
                    return VerificationResult.MalformedRecord;
                }
                catch (Exception)
                {
                    return VerificationResult.Failed;
                }
            }

            // 2. Legacy Migration Check: Unsalted SHA-256 (64 hex characters)
            if (storedHash.Length == 64 && IsHexString(storedHash))
            {
                try
                {
                    using var sha = SHA256.Create();
                    byte[] hashBytes = sha.ComputeHash(Encoding.UTF8.GetBytes(enteredPassword));

                    byte[] expectedHashBytes = HexStringToByteArray(storedHash);
                    if (CryptographicOperations.FixedTimeEquals(hashBytes, expectedHashBytes))
                    {
                        return VerificationResult.SuccessRehashNeeded;
                    }

                    // Never fall through to the plaintext string check (prevents Pass-The-Hash).
                    return VerificationResult.Failed;
                }
                catch
                {
                    return VerificationResult.Failed;
                }
            }

            // 3. Legacy Migration Check: Plaintext string match (only for non-hash legacy accounts)
            byte[] enteredBytes = Encoding.UTF8.GetBytes(enteredPassword);
            byte[] storedBytes = Encoding.UTF8.GetBytes(storedHash);
            if (enteredBytes.Length == storedBytes.Length && CryptographicOperations.FixedTimeEquals(enteredBytes, storedBytes))
            {
                return VerificationResult.SuccessRehashNeeded;
            }

            return VerificationResult.Failed;
        }

        private static bool IsHexString(string test)
        {
            foreach (char c in test)
            {
                bool isHex = (c >= '0' && c <= '9') ||
                             (c >= 'a' && c <= 'f') ||
                             (c >= 'A' && c <= 'F');
                if (!isHex) return false;
            }
            return true;
        }

        private static byte[] HexStringToByteArray(string hex)
        {
            byte[] bytes = new byte[hex.Length / 2];
            for (int i = 0; i < hex.Length; i += 2)
            {
                bytes[i / 2] = Convert.ToByte(hex.Substring(i, 2), 16);
            }
            return bytes;
        }
    }
}
