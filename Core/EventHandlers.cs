﻿using Discord;
using Discord.WebSocket;
using Hartsy.Core.SupaBase;
using System.IO;

namespace Hartsy.Core
{
    public class EventHandlers
    {
        private readonly DiscordSocketClient _client;
        private readonly SupabaseClient _supabaseClient;

        public EventHandlers(DiscordSocketClient client, SupabaseClient supabaseClient)
        {
            _client = client;
            _supabaseClient = supabaseClient;
        }

        public void RegisterHandlers()
        {
            _client.UserJoined += OnUserJoinedAsync;
        }

        private async Task OnUserJoinedAsync(SocketGuildUser user)
        {
            string[] channelNames = { "welcome", "rules", "generate", "info" };
            Dictionary<string, SocketTextChannel> channels = new();
            foreach (string name in channelNames)
            {
                SocketTextChannel channel = user.Guild.TextChannels.FirstOrDefault(c => c.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
                if (channel != null)
                {
                    channels[name] = channel;
                }
            }
            channels.TryGetValue("welcome", out SocketTextChannel welcomeChannel);
            channels.TryGetValue("rules", out SocketTextChannel rulesChannel);
            channels.TryGetValue("generate", out SocketTextChannel generateChannel);
            channels.TryGetValue("info", out SocketTextChannel infoChannel);

            bool isLinked = await _supabaseClient.IsDiscordLinked(user.Id.ToString());
            if (welcomeChannel != null && rulesChannel != null && generateChannel != null)
            {
                string imagePath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "images", "welcome.png");
                string fallbackImageUrl = "https://example.com/welcome-image.png"; // Fallback URL
                MessageComponent button = new ComponentBuilder()
                    .WithButton("Link Discord Account", style: ButtonStyle.Link, url: "https://hartsy.ai")
                    .Build();
                EmbedBuilder embedBuilder = new EmbedBuilder()
                    .WithTitle("Welcome to Hartsy.AI!")
                    .WithDescription($"{user.Mention}, we're thrilled to have you join the **Hartsy.AI** Discord Server!")
                    .AddField("Getting Started", $"Please check out the <#{rulesChannel.Id}> for our community guidelines and the <#{infoChannel?.Id}> for information on how to get the most out of our server.")
                    .AddField("Using the Bot", $"You can use our custom bot in the <#{generateChannel.Id}> channel to generate images. Each image generation will consume one GPUT from your account.")
                    .AddField("About GPUTs", "GPUTs (GPU Time) are used as tokens for generating images. If you need more, you can purchase additional GPUTs on our website. You can make a 1 time purchase or choose a subscription.")
                    .WithFooter("Enjoy your stay and unleash your creativity with Hartsy.AI!")
                    .WithColor(Color.Blue)
                    .WithThumbnailUrl(user.GetAvatarUrl() ?? user.GetDefaultAvatarUrl())
                    .WithCurrentTimestamp();
                // Check if the image file exists
                if (File.Exists(imagePath))
                {
                    using FileStream stream = new(imagePath, FileMode.Open);
                    embedBuilder.WithImageUrl("attachment://welcome.png"); // Use the attached image
                    await welcomeChannel.SendFileAsync(stream, "welcome.png", embed: embedBuilder.Build(), components: button);
                }
                else
                {
                    // Use the fallback URL if the image file is not found
                    embedBuilder.WithImageUrl(fallbackImageUrl);
                    await welcomeChannel.SendMessageAsync(embed: embedBuilder.Build(), components: button);
                }
                if (!isLinked)
                {
                    Embed notLinkedEmbed = new EmbedBuilder()
                        .WithTitle("Link Your Hartsy.AI Account")
                        .WithDescription($"{user.Mention}, you have not linked your Discord account with your Hartsy.AI account. Make a FREE account and log into Hartsy.AI using your Discord credentials. If you have already done that and are still having issues, contact an admin. This may be a bug.")
                        .WithColor(Color.Blue)
                        .WithTimestamp(DateTimeOffset.Now)
                        .Build();
                    try
                    {
                        await user.SendMessageAsync(embed: notLinkedEmbed, components: button);
                    }
                    catch
                    {
                        await welcomeChannel.SendMessageAsync(embed: notLinkedEmbed, components: button);
                    }
                    return;
                }
                await AssignRoleBasedOnSubscription(user);
            }
        }

        private async Task AssignRoleBasedOnSubscription(SocketGuildUser user)
        {
            var userStatus = await _supabaseClient.GetSubStatus(user.Id.ToString());
            if (userStatus != null && userStatus.TryGetValue("PlanName", out object? value))
            {
                string subStatus = value?.ToString() ?? "Free";
                SocketRole subRole = user.Guild.Roles.FirstOrDefault(role => role.Name.Equals(subStatus, StringComparison.OrdinalIgnoreCase));
                if (subRole != null)
                {
                    await user.AddRoleAsync(subRole);
                }
            }
        }
    }
}
