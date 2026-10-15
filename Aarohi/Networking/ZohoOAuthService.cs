using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Aarohi.Networking
{
    
    public static class ZohoOAuthService
    {
        private static readonly HttpClient _httpClient =
            new HttpClient();

        private static readonly SemaphoreSlim _tokenLock =
            new SemaphoreSlim(1, 1);

        private static string _cachedAccessToken =
            string.Empty;

        private static DateTimeOffset _accessTokenExpiresAt =
            DateTimeOffset.MinValue;

        public static async Task<string> GetAccessTokenAsync(
            CancellationToken cancellationToken = default)
        {
            // Reuse token until shortly before expiry.
            if (IsCachedTokenUsable())
            {
                return _cachedAccessToken;
            }

            await _tokenLock.WaitAsync(
                cancellationToken);

            try
            {
                // Another caller may already have refreshed it.
                if (IsCachedTokenUsable())
                {
                    return _cachedAccessToken;
                }

                ValidateConfiguration();

                using FormUrlEncodedContent content =
                    new FormUrlEncodedContent(
                        new Dictionary<string, string>
                        {
                            ["grant_type"] =
                                "refresh_token",

                            ["client_id"] =
                                ZohoApiConfiguration.ClientId,

                            ["client_secret"] =
                                ZohoApiConfiguration.ClientSecret,

                            ["refresh_token"] =
                                ZohoApiConfiguration.RefreshToken
                        });

                using HttpRequestMessage request =
                    new HttpRequestMessage(
                        HttpMethod.Post,
                        ZohoApiConfiguration.OAuthTokenUrl)
                    {
                        Content = content
                    };

                using HttpResponseMessage response =
                    await _httpClient.SendAsync(
                        request,
                        cancellationToken);

                string json =
                    await response.Content.ReadAsStringAsync(
                        cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    throw new InvalidOperationException(
                        $"Zoho OAuth token request failed. HTTP {(int)response.StatusCode}. Response: {json}");
                }

                using JsonDocument document =
                    JsonDocument.Parse(json);

                JsonElement root =
                    document.RootElement;

                if (!root.TryGetProperty(
                        "access_token",
                        out JsonElement accessTokenElement))
                {
                    string error =
                        root.TryGetProperty(
                            "error",
                            out JsonElement errorElement)
                            ? errorElement.GetString()
                                ?? "Unknown OAuth error."
                            : "Zoho did not return an access token.";

                    throw new InvalidOperationException(
                        error);
                }

                string accessToken =
                    accessTokenElement.GetString()
                    ?? string.Empty;

                if (string.IsNullOrWhiteSpace(
                        accessToken))
                {
                    throw new InvalidOperationException(
                        "Zoho returned an empty access token.");
                }

                int expiresInSeconds =
                    3600;

                if (root.TryGetProperty(
                        "expires_in",
                        out JsonElement expiresElement) &&
                    expiresElement.TryGetInt32(
                        out int parsedExpires) &&
                    parsedExpires > 0)
                {
                    expiresInSeconds =
                        parsedExpires;
                }

                _cachedAccessToken =
                    accessToken;

                _accessTokenExpiresAt =
                    DateTimeOffset.UtcNow
                        .AddSeconds(
                            expiresInSeconds);

                Debug.WriteLine(
                    $"ZOHO OAUTH: Access token generated automatically. Expires in {expiresInSeconds} seconds.");

                return _cachedAccessToken;
            }
            finally
            {
                _tokenLock.Release();
            }
        }

        /// <summary>
        /// Forces the next API call to obtain a fresh access token.
        /// Used automatically if Zoho returns HTTP 401.
        /// </summary>
        public static void InvalidateCachedAccessToken()
        {
            _cachedAccessToken =
                string.Empty;

            _accessTokenExpiresAt =
                DateTimeOffset.MinValue;
        }

        private static bool IsCachedTokenUsable()
        {
            if (string.IsNullOrWhiteSpace(
                    _cachedAccessToken))
            {
                return false;
            }

            // Refresh one minute early.
            return DateTimeOffset.UtcNow <
                   _accessTokenExpiresAt
                       .Subtract(
                           TimeSpan.FromMinutes(1));
        }

        private static void ValidateConfiguration()
        {
            if (string.IsNullOrWhiteSpace(
                    ZohoApiConfiguration.ClientId) ||
                ZohoApiConfiguration.ClientId.Contains(
                    "PUT_NEW_",
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "Zoho ClientId is not configured.");
            }

            if (string.IsNullOrWhiteSpace(
                    ZohoApiConfiguration.ClientSecret) ||
                ZohoApiConfiguration.ClientSecret.Contains(
                    "PUT_NEW_",
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "Zoho ClientSecret is not configured.");
            }

            if (string.IsNullOrWhiteSpace(
                    ZohoApiConfiguration.RefreshToken) ||
                ZohoApiConfiguration.RefreshToken.Contains(
                    "PUT_NEW_",
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "Zoho RefreshToken is not configured.");
            }
        }
    }
}
