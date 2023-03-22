using Rick_Player.Main.Data;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Rick_Player.Main.Services.ResquestAPI;

public class YoutubeManagerDois
{
    private const string AcesseType = "offline";
    private const string ApiKey = "YOUR_API_KEY";
    private const string Scopes = "https://www.googleapis.com/auth/youtube";
    private const string RefreshToken = "YOUR_REFRESH_TOKEN";

    private const string OAuth = "https://accounts.google.com/o/oauth2/v2/auth";
    private const string PostEndPoint = "https://oauth2.googleapis.com/token";
    private const string SearchEndPoint = "https://www.googleapis.com/youtube/v3/search";

    private const string EndPoint =
        "https://www.googleapis.com/youtube/v3/videos?part=snippet&id=PEHvT929IAE&key=" + ApiKey;

    private static readonly string ClientId;
    private static readonly string ClientSecret;

    private static readonly HttpClient HttpClient;

    private readonly string _redirectUri;

    public struct Tokens
    {
        public string AccessToken { get; set; }
        public string RefreshToken { get; set; }

        public Tokens(string accessToken, string refreshToken)
        {
            AccessToken = accessToken;
            RefreshToken = refreshToken;
        }
    }

    static YoutubeManagerDois()
    {
        string? clientId = ("YOUR_CLIENT_ID");
        string? clientSecret = ("YOUR_CLIENT_SECRET");

        if (clientId is null)
            throw new YoutubeApiException("Environment Variable 'YOUTUBE_CLIENT_ID' was not found.");
        if (clientSecret is null)
            throw new YoutubeApiException("Environment Variable 'YOUTUBE_CLIENT_SECRET' was not found.");

        ClientId = clientId;
        ClientSecret = clientSecret;

        HttpClient = new();
    }

    public YoutubeManagerDois(string redirectUri)
    {
        _redirectUri = redirectUri;
    }

    private void SetBasicAuthHeader() => HttpClient.DefaultRequestHeaders.Authorization =
        new AuthenticationHeaderValue("Basic",
            $"{Convert.ToBase64String(Encoding.ASCII.GetBytes($"{ClientId}:{ClientSecret}"))}");

    private void SetBearerAuthHeader(string accessToken) => HttpClient.DefaultRequestHeaders.Authorization =
        new AuthenticationHeaderValue("Bearer", accessToken);

    public Uri RequestUserAuthorizationUri(string state)
    {
        KeyValuePair<string, string?>[] parameters = new[]
        {
            new KeyValuePair<string, string?>("client_id", ClientId),
            new KeyValuePair<string, string?>("redirect_uri", "https://localhost:44357/validate"),
            new KeyValuePair<string, string?>("response_type", "code"),
            new KeyValuePair<string, string?>("scope", Scopes),
            new KeyValuePair<string, string?>("access_type", AcesseType),
            new KeyValuePair<string, string?>("state", state)
        };

        return new Uri(OAuth + QueryString.Create(parameters).ToString());
    }

    public async Task<Tokens> RequestAccessAndRefreshTokensAsync(string authCode, string originalStateCode,
        string returnedStateCode)
    {
        if (originalStateCode != returnedStateCode)
            throw new YoutubeApiException("Invalid state code returned by the server.");

        var content = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string?>("client_id", ClientId),
            new KeyValuePair<string, string?>("client_secret", ClientSecret),
            new KeyValuePair<string, string?>("code", authCode),
            new KeyValuePair<string, string?>("grant_type", "authorization_code"),
            new KeyValuePair<string, string?>("redirect_uri", "https://localhost:44357/validate")
        });

        SetBasicAuthHeader();
        using HttpResponseMessage httpResponse =
            await HttpClient.PostAsync(PostEndPoint, content);
            
            
            
                using JsonDocument jsonResponse = JsonDocument.Parse(await httpResponse.Content.ReadAsStringAsync());

    string? accessToken = jsonResponse.RootElement.GetProperty("access_token").GetString();
    // string? refreshToken = jsonResponse.RootElement.GetProperty("refresh_token").GetString();
    string? refreshToken = RefreshToken;

    if (accessToken is null)
        throw new YoutubeApiException("Returned access token is null.");

    if (refreshToken is null)
        throw new YoutubeApiException("Returned refresh token is null.");

    SetBearerAuthHeader(accessToken);

    return new Tokens(accessToken, refreshToken);
}

public async Task<string> RefreshAccessTokenAsync(string refreshToken)
{
    var content = new FormUrlEncodedContent(new[]
    {
        new KeyValuePair<string, string?>("grant_type", "refresh_token"),
        new KeyValuePair<string, string?>("refresh_token", refreshToken)
    });

    SetBasicAuthHeader();
    using HttpResponseMessage httpResponse =
        await HttpClient.PostAsync(PostEndPoint, content);

    httpResponse.EnsureSuccessStatusCode();

    using JsonDocument jsonResponse = JsonDocument.Parse(await httpResponse.Content.ReadAsStringAsync());

    string? newAccessToken = jsonResponse.RootElement.GetProperty("access_token").GetString();
    if (newAccessToken is null)
        throw new YoutubeApiException("Returned access token is null.");

    SetBearerAuthHeader(newAccessToken);

    return newAccessToken;
}

public async Task<string> SearchVideo(string query)
{
    // Check if access token is valid before making the request
    if (DateTimeOffset.Now > AccessTokenExpiration)
        await RefreshAccessTokenAsync(Tokens.RefreshToken);

    KeyValuePair<string, string?>[] parameters = new[]
    {
        new KeyValuePair<string, string?>("q", query),
        new KeyValuePair<string, string?>("part", "snippet"),
        new KeyValuePair<string, string?>("type", "video"),
        new KeyValuePair<string, string?>("videoDefinition", "high"),
        new KeyValuePair<string, string?>("maxResults", "1"),
        new KeyValuePair<string, string?>("key", ApiKey)
    };

    using HttpResponseMessage httpResponse = await HttpClient.GetAsync(SearchEndPoint + QueryString.Create(parameters));

    httpResponse.EnsureSuccessStatusCode();

    JObject jsonResponse = JObject.Parse(await httpResponse.Content.ReadAsStringAsync());

    return (string)jsonResponse.SelectToken("items[0].id.videoId");
}

public async Task<Video> GetVideo(string videoId)
{
    // Check if access token is valid before making the request
    if (DateTimeOffset.Now > AccessTokenExpiration)
        await RefreshAccessTokenAsync(Tokens.RefreshToken);

    KeyValuePair<string, string?>[] parameters = new[]
    {
        new KeyValuePair<string, string?>("part", "snippet"),
        new KeyValuePair<string, string?>("id", videoId),
        new KeyValuePair<string, string?>("key", ApiKey)
    };

    using HttpResponseMessage httpResponse = await HttpClient.GetAsync(EndPoint + QueryString.Create(parameters));

    httpResponse.EnsureSuccessStatusCode();

    JObject jsonResponse = JObject.Parse(await httpResponse.Content.ReadAsStringAsync());

    return jsonResponse["items"].FirstOrDefault()?.ToObject<Video>()
        ?? throw new YoutubeApiException($"Could not find a video with ID '{videoId}'.");
}
