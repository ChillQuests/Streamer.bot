using Newtonsoft.Json;
using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

public class CPHInline
{
    // "Base" url for our HTTPClient operation against the Discord API (we don't set the HttpClient.BaseUrl perse, but you'll get the idea
    private const string DISCORD_API_BASE_URL = "https://discord.com/api/"; // Main Discord API url, as we will not be using Webhooks to create server scheduled events

    // Const values
    private const string _discordServerId = "<your-discord-server0id>"; // Each Discord server has it's own unique id: https://support.discord.com/hc/en-us/articles/206346498-Where-can-I-find-my-User-Server-Message-ID
    private const string _discordServerBotToken = "<your-discord-bot-token>"; // Assuming you have a Twitch/Discord bot connected to your Discord server: https://discordgsm.com/guide/how-to-get-a-discord-bot-token

    // Init our HttpClient (don't do "using (HttpClient client = new())")
    // This could have negative effects - https://www.aspnetmonsters.com/2016/08/2016-08-27-httpclientwrong/
    private static HttpClient _httpClient = new();

    // Placeholder for created Discord Event Id
    private static string? _discordEventId = string.Empty;

    public bool Execute()
    {
        // Pull the currently running event id from Global Vals (created/set on the creation of the event)
        _discordEventId = CPH.GetGlobalVar<string>("discordEventId", true);
        RemoveEvent();
        Thread.Sleep(5000); // Since we can't 'force' the overried to async, I disappointingly added this to give the code enough time to complete before we exited out.
        CPH.SetGlobalVar("discordEventId", string.Empty, true);
        return true;
    }

    private static async void RemoveEvent()
    {
        await DeleteAPIResponseAsync($"{DISCORD_API_BASE_URL}/guilds/{_discordServerId}/scheduled-events/{_discordEventId}");
    }

    private static async Task<HttpStatusCode> DeleteAPIResponseAsync(string path)
    {
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bot", _discordServerBotToken);
        using HttpResponseMessage response = await _httpClient.DeleteAsync(path);
        response.EnsureSuccessStatusCode();
        CPH.SetGlobalVar("discordEventId", string.Empty, true);
        return response.StatusCode;
    }
}
