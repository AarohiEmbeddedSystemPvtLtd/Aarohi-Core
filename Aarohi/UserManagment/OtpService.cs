using System;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace Aarohi.UserManagment
{
    /// <summary>
    /// Cryptographically secure, concurrency-safe OTP generation and verification service.
    /// Eliminates predictable System.Random generation, process-wide static variable collisions,
    /// and TOCTOU verification races. Protects in-memory OTP hashes using ephemeral HMAC-SHA256 keying.
    /// </summary>
    public static class OtpService
    {
        private sealed class OtpEntry
        {
            public byte[] KeyedHash { get; set; } = Array.Empty<byte>();
            public DateTime ExpiresAtUtc { get; set; }
            public int FailedAttempts { get; set; }
            public DateTime LastGeneratedAtUtc { get; set; }
        }

        private static readonly ConcurrentDictionary<string, OtpEntry> _otpSessions =
            new(StringComparer.OrdinalIgnoreCase);

        private static readonly ConcurrentDictionary<string, object> _userLocks =
            new(StringComparer.OrdinalIgnoreCase);

        // Ephemeral 256-bit HMAC key generated once per application process lifetime
        private static readonly byte[] _hmacKey = RandomNumberGenerator.GetBytes(32);

        private static readonly TimeSpan OtpLifetime = TimeSpan.FromMinutes(5);
        private static readonly TimeSpan RequestCooldown = TimeSpan.FromSeconds(60);
        public const int MaxAllowedAttempts = 3;

        private static object GetLock(string email) =>
            _userLocks.GetOrAdd(email.Trim().ToLowerInvariant(), _ => new object());

        private static byte[] ComputeKeyedHash(string otp)
        {
            using var hmac = new HMACSHA256(_hmacKey);
            return hmac.ComputeHash(Encoding.UTF8.GetBytes(otp));
        }

        /// <summary>
        /// Generates a cryptographically secure 6-digit numeric OTP for the specified recipient.
        /// Enforces a 60-second rate-limiting cooldown between requests.
        /// </summary>
        /// <param name="email">Recipient email address.</param>
        /// <returns>The 6-digit OTP string.</returns>
        public static string GenerateOtp(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
                throw new ArgumentException("Email cannot be empty.", nameof(email));

            string key = email.Trim().ToLowerInvariant();
            lock (GetLock(key))
            {
                if (_otpSessions.TryGetValue(key, out var existing))
                {
                    var elapsed = DateTime.UtcNow - existing.LastGeneratedAtUtc;
                    if (elapsed < RequestCooldown)
                    {
                        int waitSec = (int)(RequestCooldown - elapsed).TotalSeconds;
                        throw new InvalidOperationException($"Please wait {waitSec} seconds before requesting a new verification code.");
                    }
                }

                // Cryptographically secure 6-digit code: 100,000 to 999,999
                int code = RandomNumberGenerator.GetInt32(100000, 1000000);
                string otp = code.ToString("D6");

                var entry = new OtpEntry
                {
                    KeyedHash = ComputeKeyedHash(otp),
                    ExpiresAtUtc = DateTime.UtcNow.Add(OtpLifetime),
                    FailedAttempts = 0,
                    LastGeneratedAtUtc = DateTime.UtcNow
                };

                _otpSessions[key] = entry;
                return otp;
            }
        }

        /// <summary>
        /// Verifies a user-entered OTP against the active session in constant time.
        /// Enforces a maximum of 3 failed attempts, checks expiration, and ensures single-use consumption.
        /// </summary>
        /// <param name="email">Recipient email address.</param>
        /// <param name="enteredOtp">The OTP entered by the user.</param>
        /// <returns>True if the OTP matches, has not expired, and has not exceeded attempt limits; otherwise false.</returns>
        public static bool VerifyOtp(string email, string enteredOtp)
        {
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(enteredOtp))
                return false;

            string key = email.Trim().ToLowerInvariant();
            lock (GetLock(key))
            {
                if (!_otpSessions.TryGetValue(key, out var entry))
                    return false;

                if (DateTime.UtcNow > entry.ExpiresAtUtc)
                {
                    _otpSessions.TryRemove(key, out _);
                    return false;
                }

                if (entry.FailedAttempts >= MaxAllowedAttempts)
                {
                    _otpSessions.TryRemove(key, out _);
                    return false;
                }

                byte[] enteredHash = ComputeKeyedHash(enteredOtp.Trim());
                if (CryptographicOperations.FixedTimeEquals(enteredHash, entry.KeyedHash))
                {
                    // Atomic single-use consumption: immediately remove active session
                    _otpSessions.TryRemove(key, out _);
                    return true;
                }

                entry.FailedAttempts++;
                if (entry.FailedAttempts >= MaxAllowedAttempts)
                {
                    _otpSessions.TryRemove(key, out _);
                }

                return false;
            }
        }

        /// <summary>
        /// Invalidates any active OTP session for the specified email.
        /// </summary>
        public static void InvalidateOtp(string email)
        {
            if (string.IsNullOrWhiteSpace(email)) return;
            string key = email.Trim().ToLowerInvariant();
            lock (GetLock(key))
            {
                _otpSessions.TryRemove(key, out _);
            }
        }
    }
}
