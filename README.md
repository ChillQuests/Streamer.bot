# Streamer.Bot Custom Actions

If you're looking for a powerful bot/stream management tool for your own Twitch/YouTube stream bot needs, [download Streamer.Bot here](https://streamer.bot/).

This repository is more of a sandbox that contains a combination of C# code and importable scripts that can be used to execute logic from Streamer.Bot actions that I use for my own Twitch chat bot.

NOTE: I write code for a living, but please don't judge the ugliness of these offerings - I'm not going for elegance and my 'career coding standards' are MUCH higher than what you'll see here. :)

## Current Offerings:

[twitch-golive-scheduled-event](https://github.com/ChillQuests/Streamer.bot/tree/main/src/twitch-golive-scheduled-event)

When your stream goes "live", your bot will create a scheduled event in your Discord Server for all members to see, similar to posting a "live" notification to an "Announcements" channel, but actually provide a more visible event at the top of your server that is hard to miss.  Discord members will find it difficult to _not_ know when you are streaming.

Change Log:
8/11/2023 - Initial Commit
8/13/2023 - Add support for event specific logic (used to assist in "Go Offlne" code to only delete the event created during the "Go Live" event as to not blindly delete ALL events in case there are other events that we want to keep for other purposes.

[twitch-endstream-delete-events](https://github.com/ChillQuests/Streamer.bot/tree/main/src/twitch-endstream-delete-events)

When your stream is finished and to go offline, this action will delete ONLY the event created during the "Go Live" event.

Change Log:
8/11/2023 - Initial Commit
8/13/2023 - Add support for event specific logic (used to assist in "Go Offlne" code to only delete the event created during the "Go Live" event as to not blindly delete ALL events in case there are other events that we want to keep for other purposes.
