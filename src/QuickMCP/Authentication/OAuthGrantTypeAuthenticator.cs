using System.Net.Http.Headers;
using System.Text.Json;
using QuickMCP.Abstractions;
using QuickMCP.Types;

namespace QuickMCP.Authentication;

/// <summary>
/// Provides OAuth 2.0 custom grant type authentication.
/// This authenticator retrieves and caches access tokens using custom grant types like API keys.
/// </summary>
public class OAuthGrantTypeAuthenticator : IAuthenticator
{
    #region Fields and Properties

    private readonly string _tokenUrl;
    private readonly string _grantType;
    private readonly Dictionary<string, string> _grantParameters;
    private readonly string? _scope;
    private readonly string? _clientId;
    private readonly HttpClient _httpClient;
    private readonly OAuthCache _tokenCache;
    public string Type => Metadata.Type;
    public AuthenticatorMetadata Metadata => GetMetadata();

    #endregion

    #region Factory

    /// <summary>
    /// Retrieves the metadata for the OAuth grant type authenticator, including its name, description,
    /// configuration keys, and type.
    /// </summary>
    /// <returns>An instance of <see cref="AuthenticatorMetadata"/> containing details about the OAuth grant type authenticator.</returns>
    public static AuthenticatorMetadata GetMetadata()
    {
        const string name = "OAuth 2.0 Custom Grant Type Authentication";

        const string description =
            "OAuth 2.0 authentication using custom grant types (e.g., api_key, phone_otp). " +
            "Supports any custom extension grant type that returns an access token.\n\n" +
            "Required Settings:\n" +
            "- tokenUrl: The OAuth token endpoint URL\n" +
            "- grantType: The custom grant type name\n" +
            "- clientId: The OAuth client ID\n\n" +
            "Optional Settings:\n" +
            "- scope: Access scope(s) to request\n\n" +
            "Grant-Specific Parameters:\n" +
            "You can add any additional key-value pairs as grant-specific parameters. " +
            "These will be sent to the token endpoint along with the grant_type.\n\n" +
            "Examples:\n" +
            "For 'api_key' grant type, add:\n" +
            "  - api_key: Your API key value (e.g., 'sk_...')\n\n" +
            "For 'phone_otp' grant type, add:\n" +
            "  - phone_number: The phone number\n" +
            "  - otp: The OTP code\n" +
            "  - otp_id: The OTP session ID\n\n" +
            "Any settings other than 'tokenUrl', 'grantType', 'clientId', and 'scope' will be treated as grant parameters.";

        const string type = "oAuthGrantType";

        List<(string Key, string Description, bool IsRequired)> configKeys =
        [
            ("tokenUrl", "The URL used to retrieve an access token (e.g., https://api.example.com/connect/token).", true),
            ("grantType", "The custom grant type name (e.g., 'api_key', 'phone_otp').", true),
            ("clientId", "The OAuth client ID (e.g., 'MyApp').", true),
            ("scope", "Optional access scope (e.g., 'api', 'openid'). Multiple scopes can be space-separated.", false),
            ("{grant_params}", "Additional key-value pairs specific to your grant type. For example: 'api_key', 'phone_number', 'otp', etc.", false)
        ];
        return new AuthenticatorMetadata(name, description, configKeys, type);
    }

    /// <summary>
    /// Creates an OAuth Grant Type Authenticator.
    /// </summary>
    /// <param name="settings">The settings containing token URL, grant type, and grant-specific parameters.</param>
    /// <returns>An instance of <see cref="OAuthGrantTypeAuthenticator"/>.</returns>
    /// <exception cref="ArgumentException">Thrown when required settings (token URL, grant type) are missing.</exception>
    public static IAuthenticator Create(Dictionary<string, string?> settings)
    {
        if (!settings.TryGetValue("tokenUrl", out var tokenUrl) ||
            !settings.TryGetValue("grantType", out var grantType) ||
            !settings.TryGetValue("clientId", out var clientId))
        {
            throw new ArgumentException(
                "OAuth Grant Type authentication requires 'tokenUrl', 'grantType', and 'clientId' settings");
        }

        if (string.IsNullOrEmpty(tokenUrl) || string.IsNullOrEmpty(grantType) || string.IsNullOrEmpty(clientId))
        {
            throw new ArgumentException(
                "OAuth Grant Type authentication requires 'tokenUrl', 'grantType', and 'clientId' settings");
        }

        settings.TryGetValue("scope", out var scope);

        // Extract grant-specific parameters (all settings except tokenUrl, grantType, clientId, and scope)
        var grantParameters = new Dictionary<string, string>();
        foreach (var setting in settings)
        {
            if (setting.Key != "tokenUrl" &&
                setting.Key != "grantType" &&
                setting.Key != "clientId" &&
                setting.Key != "scope" &&
                !string.IsNullOrEmpty(setting.Value))
            {
                grantParameters[setting.Key] = setting.Value;
            }
        }

        return new OAuthGrantTypeAuthenticator(tokenUrl!, grantType!, clientId!, grantParameters, scope);
    }

    #endregion

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="OAuthGrantTypeAuthenticator"/> class.
    /// </summary>
    /// <param name="tokenUrl">The URL used to retrieve an access token.</param>
    /// <param name="grantType">The custom grant type name (e.g., "api_key").</param>
    /// <param name="clientId">The OAuth client ID.</param>
    /// <param name="grantParameters">Grant-specific parameters (e.g., { "api_key": "sk_xxx..." }).</param>
    /// <param name="scope">Optional access scope.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown if <paramref name="tokenUrl"/>, <paramref name="grantType"/>, or <paramref name="clientId"/> is null.
    /// </exception>
    public OAuthGrantTypeAuthenticator(
        string tokenUrl,
        string grantType,
        string clientId,
        Dictionary<string, string> grantParameters,
        string? scope = null)
    {
        _tokenUrl = tokenUrl ?? throw new ArgumentNullException(nameof(tokenUrl));
        _grantType = grantType ?? throw new ArgumentNullException(nameof(grantType));
        _clientId = clientId ?? throw new ArgumentNullException(nameof(clientId));
        _grantParameters = grantParameters ?? throw new ArgumentNullException(nameof(grantParameters));
        _scope = scope;
        _httpClient = new HttpClient();
        _tokenCache = new OAuthCache();
    }

    #endregion

    #region IAuthenticator Implementation

    /// <inheritdoc />
    public async Task AuthenticateRequestAsync(HttpRequestMessage request)
    {
        var token = await GetAccessTokenAsync();
        if (!string.IsNullOrEmpty(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
    }

    /// <inheritdoc />
    public async Task<Dictionary<string, string>> GetAuthHeadersAsync()
    {
        var token = await GetAccessTokenAsync();
        if (!string.IsNullOrEmpty(token))
        {
            return new Dictionary<string, string>
            {
                ["Authorization"] = $"Bearer {token}"
            };
        }

        return new Dictionary<string, string>();
    }

    /// <inheritdoc />
    public async Task<bool> IsAuthenticatedAsync()
    {
        var token = await GetAccessTokenAsync();
        return !string.IsNullOrEmpty(token);
    }

    #endregion

    #region Private Methods

    /// <summary>
    /// Retrieves an access token, utilizing the cache if available, otherwise fetching a new one.
    /// </summary>
    /// <returns>The access token or null if unavailable.</returns>
    private async Task<string?> GetAccessTokenAsync()
    {
        // Check cache first
        var cachedToken = _tokenCache.GetToken();
        if (cachedToken != null)
        {
            return cachedToken;
        }

        try
        {
            // Build request for token with grant type and parameters
            var formData = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("grant_type", _grantType),
                new KeyValuePair<string, string>("client_id", _clientId!)
            };

            // Add grant-specific parameters
            foreach (var param in _grantParameters)
            {
                formData.Add(new KeyValuePair<string, string>(param.Key, param.Value));
            }

            // Add scope if provided
            if (!string.IsNullOrEmpty(_scope))
            {
                formData.Add(new KeyValuePair<string, string>("scope", _scope));
            }

            var content = new FormUrlEncodedContent(formData);

            // Request new token
            var response = await _httpClient.PostAsync(_tokenUrl, content);
            response.EnsureSuccessStatusCode();

            var tokenJson = await response.Content.ReadAsStringAsync();
            var tokenData = JsonSerializer.Deserialize<JsonElement>(tokenJson);

            var accessToken = tokenData.GetProperty("access_token").GetString();
            var expiresIn = tokenData.TryGetProperty("expires_in", out var expiresInElement)
                ? expiresInElement.GetInt32()
                : 3600;

            if (accessToken != null)
            {
                _tokenCache.SetToken(accessToken, expiresIn);
                return accessToken;
            }
        }
        catch (Exception)
        {
            throw;
        }

        return null;
    }

    #endregion
}
