﻿using Discord.Interactions;
using Discord.WebSocket;
using System.Threading.Tasks;
using Discord;

namespace HartsyBot
{
    public class Commands : InteractionModuleBase<SocketInteractionContext>
    {
        [SlashCommand("setup_rules", "Set up rules for the server.")]
        public async Task SetupRulesCommand()
        {
            var rulesChannel = Context.Guild.TextChannels.FirstOrDefault(x => x.Name == "rules");
            if (rulesChannel == null)
            {
                await RespondAsync("Rules channel not found.");
                return;
            }

            // Initialize placeholders
            string titlePlaceholder = "Enter the title", 
                descriptionPlaceholder = "Enter the description",
                footerPlaceholder = "Enter the footer text",
                authorPlaceholder = "Enter the author's name";

            // Get the last message from the rules channel
            var messages = await rulesChannel.GetMessagesAsync(1).FlattenAsync();
            var lastMessage = messages.FirstOrDefault();
            if (lastMessage != null && lastMessage.Embeds.Any())
            {
                var embed = lastMessage.Embeds.First();
                titlePlaceholder = embed.Title ?? titlePlaceholder;
                descriptionPlaceholder = embed.Description ?? descriptionPlaceholder;
                footerPlaceholder = embed.Footer?.Text ?? footerPlaceholder;
                authorPlaceholder = embed.Author?.Name ?? authorPlaceholder;
            }

            var modal = new ModalBuilder()
                .WithTitle("Server Rules")
                .WithCustomId("setup_rules_modal")
                .AddTextInput("Title", "title_input", placeholder: titlePlaceholder, maxLength: 100)
                .AddTextInput("Description", "description_input", placeholder: descriptionPlaceholder, maxLength: 1000)
                .AddTextInput("Footer", "footer_input", placeholder: footerPlaceholder, maxLength: 100)
                .AddTextInput("Author", "author_input", placeholder: authorPlaceholder, maxLength: 100)
                .Build();

            await RespondWithModalAsync(modal);
        }

        [SlashCommand("ping", "Pings the bot.")]
        public async Task PingCommand()
        {
            await RespondAsync("Pong!");
        }

        [ModalInteraction("setup_rules_modal")]
        public async Task OnRulesModalSubmit(SocketModal modal)
        {
            // Extract the data from the modal
            var title = modal.Data.Components.FirstOrDefault(x => x.CustomId == "title_input")?.Value;
            var description = modal.Data.Components.FirstOrDefault(x => x.CustomId == "description_input")?.Value;
            var footer = modal.Data.Components.FirstOrDefault(x => x.CustomId == "footer_input")?.Value;
            var author = modal.Data.Components.FirstOrDefault(x => x.CustomId == "author_input")?.Value;

            // Construct the embed
            var embed = new EmbedBuilder()
                .WithTitle(title)
                .WithDescription(description)
                .WithFooter(footer)
                .WithAuthor(author)
                .Build();

            // Find the 'rules' channel
            var rulesChannel = Context.Guild.TextChannels.FirstOrDefault(x => x.Name == "rules");
            if (rulesChannel != null)
            {
                // Check for the last message in the 'rules' channel
                var messages = await rulesChannel.GetMessagesAsync(1).FlattenAsync();
                var lastMessage = messages.FirstOrDefault();
                if (lastMessage != null)
                {
                    // Delete the last message
                    await lastMessage.DeleteAsync();
                }

                // Define the buttons
                var buttonComponent = new ComponentBuilder()
                    .WithButton("I Read the Rules", "read_rules", ButtonStyle.Success)
                    .WithButton("Notify Me", "notify_me", ButtonStyle.Primary)
                    .Build();

                // Send the new embed with buttons
                await rulesChannel.SendMessageAsync(embed: embed, components: buttonComponent);
            }
            else
            {
                await RespondAsync("Rules channel not found.", ephemeral: true);
            }
        }

    }
}