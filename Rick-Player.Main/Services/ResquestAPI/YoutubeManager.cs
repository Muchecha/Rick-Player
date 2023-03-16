using Rick_Player.Main.Data;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Rick_Player.Main.Services.ResquestAPI;

public class YoutubeManager
{
    private const string AcesseType = "offline";
    private const string ApiKey = "AIzaSyBpYkRFY2fJdOFHhw8W7hRexY4k2pA14CY";
    private const string Scopes = "https://www.googleapis.com/auth/youtube";
    private const string RefreshToken = "1//0h249N9l8i3MeCgYIARAAGBESNwF-L9IrUuFrA0lPOkpPI8ECzG9IDHukOew4Ji27Eh5NhDBTliLkhNVPVX6NqEYRULb67UxcP4E";
    
    private const string OAuth = "https://accounts.google.com/o/oauth2/v2/auth";
    private  const string PostEndPoint = "https://oauth2.googleapis.com/token";
    private const string SearchEndPoint = "https://www.googleapis.com/youtube/v3/search";
    private const string EndPoint = "https://www.googleapis.com/youtube/v3/videos?part=snippet&id=PEHvT929IAE&key=AIzaSyBpYkRFY2fJdOFHhw8W7hRexY4k2pA14CY";

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

    static YoutubeManager()
    {
        string? clientId = ("751770806331-bp6btpu43rjird4b0d4n4k8akcm0550n.apps.googleusercontent.com");
        string? clientSecret = ("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");

        if (clientId is null)
            throw new YoutubeApiException("Environment Variable 'YOUTUBE_CLIENT_ID' was not found.");
        if (clientSecret is null)
            throw new YoutubeApiException("Environment Variable 'YOUTUBE_CLIENT_SECRET' was not found.");

        ClientId = clientId;
        ClientSecret = clientSecret;

        HttpClient = new();
    }

    public YoutubeManager(string redirectUri)
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

    public async Task<Tokens> RequestAccessAndRefreshTokensAsync(string authCode, string originalStateCode, string returnedStateCode)
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

        httpResponse.EnsureSuccessStatusCode();

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

    public async Task<Track?> GetCurrentlyPlayingAsync()
    {
        using HttpResponseMessage httpResponse = await HttpClient.GetAsync(EndPoint);

        httpResponse.EnsureSuccessStatusCode();

        if (httpResponse.StatusCode == HttpStatusCode.OK)
        {
            using JsonDocument jsonResponse = JsonDocument.Parse(await httpResponse.Content.ReadAsStringAsync());

            Track currentTrack = GetTrackFromJson(jsonResponse.RootElement.GetProperty("items"));

            // currentTrack.ProgressMs = jsonResponse.RootElement.GetProperty("progress_ms").GetInt32();

            return currentTrack;
        }
        else
            return null;
    }

    public async Task<List<Track>> SearchTracksAsync(string searchFor)
    {
        KeyValuePair<string, string>[] parameters = new[]
        {
            new KeyValuePair<string, string>("part", "snippet"),
            new KeyValuePair<string, string>("q", searchFor),
            new KeyValuePair<string, string>("type", "youtube#video"),
            new KeyValuePair<string, string>("maxResults", "10"),
            new KeyValuePair<string, string>("key", ApiKey)
        };

        using HttpResponseMessage httpResponse =
            await HttpClient.GetAsync(SearchEndPoint + QueryString.Create(parameters!));

        httpResponse.EnsureSuccessStatusCode();

        JsonDocument jsonResponse = JsonDocument.Parse(await httpResponse.Content.ReadAsStringAsync());

        List<Track> searchedTracks = new();
        foreach (JsonElement item in jsonResponse.RootElement.GetProperty("items")
                     .EnumerateArray())
            searchedTracks.Add(GetTrackFromJson(item));

        return searchedTracks;
    }

    public async Task AddToPlaybackQueueAsync(Track track)
    {
        using HttpResponseMessage httpResponse =
            await HttpClient.PostAsync(
                OAuth + "/me/player/queue" + QueryString.Create("uri", $"spotify:track:{track.VideoId}"), null);

        httpResponse.EnsureSuccessStatusCode();
    }

    private Track GetTrackFromJson(JsonElement item)
    {
        Track track = new();

        JsonElement idElement = item.GetProperty("id");

        if (idElement.ValueKind == JsonValueKind.Object)
        {
            JsonElement videoIdElement = idElement.GetProperty("videoId");
            if (videoIdElement.ValueKind == JsonValueKind.String)
            {
                track.VideoId = videoIdElement.GetString();
            }
        }
        // track.VideoId = item.GetProperty("id").GetProperty("videoId").GetString();
        // track.SongName = item.GetProperty("name").GetString();
        // track.AlbumName = item.GetProperty("album").GetProperty("name").GetString();
        // track.DurationMs = item.GetProperty("duration_ms").GetInt32();
        //
        // foreach (JsonElement cover in item.GetProperty("album").GetProperty("images").EnumerateArray())
        //     track.CoverSizesUrl.Add(cover.GetProperty("url").GetString());
        //
        // foreach (JsonElement artist in item.GetProperty("artists").EnumerateArray())
        //     track.ArtistNames.Add(artist.GetProperty("name").GetString());

        return track;
    }
}