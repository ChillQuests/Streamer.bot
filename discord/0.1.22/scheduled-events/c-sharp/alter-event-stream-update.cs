using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;

public class CPHInline
{
    // These address are assigned as "base" urls for our HTTPClient operations (we don't set the HttpClient.BaseUrl perse, but you'll get the idea
    private const string TWITCH_API_OAUTH_URL = "https://id.twitch.tv/oauth2/token"; // In order to interace with the Twitch API, we need to generate an OAuth token with our Twitch API client id/secret
    private const string TWITCH_API_BASE_URL = "https://api.twitch.tv/helix/"; // We'll be making a call to the Twitch API using the current SB "gameId" to pull the "box_art_url"
    private const string DISCORD_API_BASE_URL = "https://discord.com/api/"; // Main Discord API url, as we will not be using Webhooks to create server scheduled events

    // Const values
    private const string _discordServerId = "<your-discord-server0id>"; // Each Discord server has it's own unique id: https://support.discord.com/hc/en-us/articles/206346498-Where-can-I-find-my-User-Server-Message-ID
    private const string _discordServerBotToken = "<your-discord-bot-token>"; // Assuming you have a Twitch/Discord bot connected to your Discord server: https://discordgsm.com/guide/how-to-get-a-discord-bot-token
    private const string _twitchApiClientId = "<your-twitch-app-client-id>"; // The Twitch API requires you to register an app (your bot) and a client id will be assigned: https://dev.twitch.tv/docs/authentication
    private const string _twitchApiClientSecret = "<your-twitch-app-client-secret>"; // Along with the client id, you will also need a client secret to be passed it to generate the proper OAuth token

    // Discord event global variable
    private string _discordEventId = string.Empty;

    // Twitch API OAuth variables
    private static string? _twitchApiAuthToken;
    private static string? _twitchApiAuthType = "Bearer";

    // Twitch image variables
    private static string? _twitchGameImageUrl;
    private static string? _twitchGameImageContent;

    // Image dimensions for Discord Event background image
    private const string TWITCH_GAME_IMAGE_WIDTH = "800";
    private const string TWITCH_GAME_IMAGE_HEIGHT = "400";

    // Init our HttpClient (don't do "using (HttpClient client = new())")
    // This could have negative effects - https://www.aspnetmonsters.com/2016/08/2016-08-27-httpclientwrong/
    private static HttpClient _httpClient = new();

    // Aid in image dimension replacements
    private static readonly Dictionary<string, string> _imageDimensions = new()
    {
        {
            "{width}", TWITCH_GAME_IMAGE_WIDTH
        },
        {
            "{height}", TWITCH_GAME_IMAGE_HEIGHT
        }
    };

    public bool Execute()
    {
        // Get input vars from previous sub-action
        var _gameId = args["gameId"]; // "New" Twitch game id
        var _game = args["game"]; // "New" Twitch game name
        var _status = args["status"]; // "New" stream title

        // Get Streamer.bot global var for existing event id (if exists)
        _discordEventId = CPH.GetGlobalVar<string>("discordEventId", true) ?? string.Empty;

        // If we don't have an existing live event, exit out - nothing to do
        if (_discordEventId == string.Empty)
        {
            return true;
        }

        // Initialize Twitch OAuth token
        var contentBody = $"client_id={_twitchApiClientId}&client_secret={_twitchApiClientSecret}&grant_type=client_credentials";
        var authRequestContent = new StringContent(contentBody, Encoding.UTF8, "application/x-www-form-urlencoded");

        var authResponseContent = _httpClient.PostAsync(TWITCH_API_OAUTH_URL, authRequestContent).Result;
        if (authResponseContent != null && authResponseContent.IsSuccessStatusCode)
        {
            // Read results content and extract desired values from payload
            var authResponsePayload = JObject.Parse(authResponseContent.Content.ReadAsStringAsync().Result);
            _twitchApiAuthToken = (string)authResponsePayload["access_token"];
        }

        // Now that we have our OAuth token, set our API client for Twitch to include the new token
        // and "application/json" context in the default headers for the rest of our calls
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(_twitchApiAuthType, _twitchApiAuthToken);
        _httpClient.DefaultRequestHeaders.Add("Client-Id", _twitchApiClientId);

        // Make the call to to the "games" resource to return game's image offered by Twitch to Discord's required Data URI Scheme
        var gameDetail = _httpClient.GetAsync(string.Concat(TWITCH_API_BASE_URL, "games?id=", _gameId)).Result;
        if (gameDetail != null && gameDetail.IsSuccessStatusCode)
        {
            // Read results content and extract desired values from payload
            var gameDetailPayload = JObject.Parse(gameDetail.Content.ReadAsStringAsync().Result);

            // Check to see if the call has a valid "box_art_url" value
            if (!string.IsNullOrWhiteSpace((string)gameDetailPayload["data"][0]["box_art_url"]))
            {
                // Extract Twitch game image
                _twitchGameImageUrl = (string)gameDetailPayload["data"][0]["box_art_url"];

                // Twitch returns the "box_art_url" with placeholders for image dimensions.
                // ex: https://static-cdn.jtvnw.net/ttv-boxart/American%20Truck%20Simulator-{width}x{height}.jpg
                // We want to change those to attempt the fit for the Discord event background
                // via a string.Replace
                _twitchGameImageUrl = ReplaceImageDimensions(_twitchGameImageUrl);
            }

            // Next, Discord expects the image to be passed in using the "Data Image Scheme"
            // so now we pull the actual image and perform the required conversion
            var gameImageDataResponse = _httpClient.GetAsync(_twitchGameImageUrl).Result;
            if (gameImageDataResponse != null && gameImageDataResponse.IsSuccessStatusCode)
            {
                _twitchGameImageContent = "data:image/png;base64," + Convert.ToBase64String(gameImageDataResponse.Content.ReadAsByteArrayAsync().Result);
            }
        }

        // Build API PATCH JSON payload (use an "Add target info for broadcaster" sub-action prior to "Execute Code")
        var content = JsonConvert.SerializeObject(new
        {
            name = string.Concat("🔴 ", "LIVE - ", _game), // Format as you'd like, "_game" is the updated game title
            description = _status, // Updated stream title
            image = _twitchGameImageContent ?? null // If we were able to pull a valid game image from the Twitch API call, set the event background image here
        });

        // Reset out HttpClient and prep for Discord POST call
        _httpClient.DefaultRequestHeaders.Clear();

        // You'll need your Discord bot's token with "Manage Events" permission
        var requestContent = new StringContent(content, Encoding.UTF8, "application/json");
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bot", _discordServerBotToken);

        // HttpClient doesn't support "Patch" out-of-the-box in the version of .NET that SB is running,
        // so we have to encourage it a bit
        // Clarification: .NET Core does, but Streamer.Bot seems to not use Core.. Yet?
        var method = new HttpMethod("PATCH");
        var request = new HttpRequestMessage(method, string.Concat(DISCORD_API_BASE_URL, "guilds/", _discordServerId, "/scheduled-events/", _discordEventId))
        {
            Content = new StringContent(content, Encoding.UTF8, "application/json")
        };

        // Attempt PATCH and just ensure that we don't receive any error based status codes
        using (HttpResponseMessage response = _httpClient.SendAsync(request).Result)
        {
            response.EnsureSuccessStatusCode();
            string responseBody = response.Content.ReadAsStringAsync().Result;
        }

        return true;
    }

    /// <summary>
    ///   Aids in replacing the Twitch image URL dimension values
    /// </summary>
    /// <param name="imageUrl">Full URL of the Twitch image returned from GET operation on game id</param>
    /// <returns>Modified Twitch image URL with updated "width"/"height" parameters</returns>
    private static string ReplaceImageDimensions(string imageUrl)
    {
        foreach (string replace in _imageDimensions.Keys)
        {
            imageUrl = imageUrl.Replace(replace, _imageDimensions[replace]);
        }
        return imageUrl;
    }
}