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

    // Twitch API OAuth variables
    private static string _twitchApiAuthToken;
    private static string _twitchApiAuthType = "Bearer";

    // Twitch image variables
    private static string _twitchGameImageUrl;
    private static string _twitchGameImageContent;

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
        var _gameId = args["gameId"];  // We'll use this to perform a GET against the Twich "games" endpoint for the current game's box art
        var _game = args["game"]; // Name of the current game
        var _targetChannelTitle = args["targetChannelTitle"];  // Stream title
        var _targetUserName = args["targetUserName"]; // Used for the Event "Location" when we build our channel url

        // Initialize Twitch OAuth token generation call
        var contentBody = $"client_id={_twitchApiClientId}&client_secret={_twitchApiClientSecret}&grant_type=client_credentials";
        var authRequestContent = new StringContent(contentBody, Encoding.UTF8, "application/x-www-form-urlencoded");

        var authResponseContent = _httpClient.PostAsync(TWITCH_API_OAUTH_URL, authRequestContent).Result;
        if (authResponseContent != null && authResponseContent.IsSuccessStatusCode)
        {
            // Read results content and extract desired values from payload
            var authResponsePayload = JObject.Parse(authResponseContent.Content.ReadAsStringAsync().Result);
            _twitchApiAuthToken = (string)authResponsePayload["access_token"];  // This is the generate OAuth "Bearer" token that we'll pass along to the Twitch Resource API
        }

        // Now that we have our OAuth token, set our API client for Twitch to include the new token
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
                // via a Replace operation
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

        // Build API POST JSON payload for our call to the Discord API
        // (use an "Add target info for broadcaster" sub-action prior to "Execute Code")
        var content = JsonConvert.SerializeObject(new
        {
            name = string.Concat("🔴 ", "LIVE - ", _game), // Format as you'd like, "_game" is the actual game title
            scheduled_start_time = DateTimeOffset.UtcNow.AddSeconds(2), // You may need to play with this a bit depending on your internel connection speed
            scheduled_end_time = DateTimeOffset.UtcNow.AddDays(1), // I default to 24 hours for the event duration, but we'll delete the event when the stream goes offline
            entity_type = 3, // I use "External" as I want to set my Twitch channel url as the "location" of the event
            privacy_level = "2", // Only option right now is "2" (viewable by all Guild/Server members)
            entity_metadata = new // API uses the "Location" value as an embedded object
            {
                location = $"https://www.twitch.tv/{_targetUserName}"
            },
            description = _targetChannelTitle, // Stream title, you could swap out "name" and "description", but longer stream titles could make things...ugly
            image = _twitchGameImageContent ?? null // If we were able to pull a valid game image from the Twitch API call, set the event background image here
        });

        // Reset out HttpClient and prep for Discord POST call
        _httpClient.DefaultRequestHeaders.Clear();

        // You'll need your Discord bot's token with "Manage Events" permission
        var requestContent = new StringContent(content, Encoding.UTF8, "application/json");
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bot", _discordServerBotToken);

        // Attempt POST and read the response to extract the newly created event id.
        // We will save the event id as an SB global variable so we can cleanup when stream ends.
        var response = _httpClient.PostAsync(string.Concat(DISCORD_API_BASE_URL, "/guilds/", _discordServerId, "/scheduled-events"), requestContent).Result;
        if (response.Content != null && response.IsSuccessStatusCode)
        {
            var eventPayload = JObject.Parse(response.Content.ReadAsStringAsync().Result);
            CPH.SetGlobalVar("discordEventId", (string)eventPayload["id"], true);
        };

        return true;
    }

    /// <summary>
    ///   We don't need the entire Scheduled Event POST response payload, just the id
    /// </summary>
    private class GuildEvent
    {
        public int id { get; set; }
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