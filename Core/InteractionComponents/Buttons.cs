﻿using Discord.Interactions;
using Discord.WebSocket;
using Discord;
using Hartsy.Core.Commands;
using Hartsy.Core.SupaBase;
using Hartsy.Core.SupaBase.Models;
using Hartsy.Core.ImageUtil;
using Microsoft.VisualBasic;

namespace Hartsy.Core.InteractionComponents
{
    public class Buttons(UserCommands commands, SupabaseClient supaBase) : InteractionModuleBase<SocketInteractionContext>
    {
        /// <summary>Handles the interaction when the 'read_rules' button is clicked, assigning or removing roles as necessary.</summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        [ComponentInteraction("read_rules")]
        public async Task ReadRulesButtonHandler()
        {
            SocketRole? memberRole = Context.Guild.Roles.FirstOrDefault(r => r.Name == "Member");
            SocketRole? announcementRole = Context.Guild.Roles.FirstOrDefault(r => r.Name == "Announcement");
            SocketGuildUser user = (SocketGuildUser)Context.User;
            if (ComponentHelpers.IsOnCooldown(Context.User, "read_rules"))
            {
                await RespondAsync("You are on cooldown. Please wait before trying again.", ephemeral: true);
                return;
            }
            List<IRole> rolesToAdd = [];
            List<IRole> rolesToRemove = [];
            if (memberRole != null)
            {
                if (!user.Roles.Contains(memberRole))
                    rolesToAdd.Add(memberRole);
                else
                    rolesToRemove.Add(memberRole);
            }
            if (announcementRole != null)
            {
                if (!user.Roles.Contains(announcementRole))
                    rolesToAdd.Add(announcementRole);
                else
                    rolesToRemove.Add(announcementRole);
            }
            if (rolesToAdd.Count != 0)
            {
                await user.AddRolesAsync(rolesToAdd);
            }
            if (rolesToRemove.Count != 0)
            {
                await user.RemoveRolesAsync(rolesToRemove);
            }
            string response = "";
            if (rolesToAdd.Count != 0)
            {
                response += $"You have been given the {string.Join(", ", rolesToAdd.Select(r => r.Name))} role(s)!\n";
            }
            if (rolesToRemove.Count != 0)
            {
                response += $"The {string.Join(", ", rolesToRemove.Select(r => r.Name))} role(s) have been removed from you!";
            }
            await RespondAsync(response, ephemeral: true);
            // TODO: Add a check if the user has linked their discord account with their Hartsy.AI account and if they are a subscriber
        }

        /// <summary>Handles the interaction when the 'notify_me' button is clicked, toggling the 'Announcement' role for the user.</summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        [ComponentInteraction("notify_me")]
        public async Task NotifyMeButtonHandler()
        {
            SocketRole? role = Context.Guild.Roles.FirstOrDefault(r => r.Name == "Announcement");
            SocketGuildUser user = (SocketGuildUser)Context.User;
            if (ComponentHelpers.IsOnCooldown(Context.User, "notify_me"))
            {
                await RespondAsync("You are on cooldown. Please wait before trying again.", ephemeral: true);
                return;
            }
            if (role != null && user.Roles.Contains(role))
            {
                await user.RemoveRoleAsync(role);
                await RespondAsync("The 'Announcement' role has been removed from you!", ephemeral: true);
            }
            else
            {
                await user.AddRoleAsync(role);
                await RespondAsync("You have been given the 'Announcement' role!", ephemeral: true);
            }
        }

        [ComponentInteraction("regenerate:*")]
        public async Task RegenerateButtonHandler(string customId)
        {
            await DeferAsync();
            string[] parts = customId.Split(':');
            if (parts.Length < 2)
            {
                Console.WriteLine("Invalid customId format.");
                await FollowupAsync("Error: Invalid custom ID.", ephemeral: true);
                return;
            }
            string userId = parts[0];
            string generationType = parts[1];
            if (Context.User.Id.ToString() != userId)
            {
                Console.WriteLine("Another user tried to click a button.");
                await FollowupAsync("Error: You cannot regenerate another user's image.", ephemeral: true);
                return;
            }
            if (ComponentHelpers.IsOnCooldown(Context.User, "regenerate"))
            {
                await FollowupAsync("You are on cooldown. Please wait before trying again.", ephemeral: true);
                return;
            }
            SocketUserMessage? message = (Context.Interaction as SocketMessageComponent)?.Message;
            if (message == null || message.Embeds.Count == 0)
            {
                Console.WriteLine("Message or embeds are null/empty.");
                await FollowupAsync("Error: Message or embeds are missing.", ephemeral: true);
                return;
            }
            Embed embed = message.Embeds.First();
            var fields = embed.Fields.ToDictionary(f => f.Name, f => f.Value);
            SocketTextChannel? channel = Context.Channel as SocketTextChannel;
            SocketGuildUser? user = Context.User as SocketGuildUser;
            switch (generationType.ToLower())
            {
                case "template":
                    string? text = fields["Text"];
                    string? description = fields["Description"];
                    string? template = fields["Template"];
                    await commands.GenerateFromTemplate(text, template, channel, user, description);
                    break;
                case "flux":
                    string? prompt = fields["Prompt"];
                    string? aspect = fields["AspectRatio"];
                    await commands.GenerateForFlux(prompt, aspect, channel, user);
                    break;
                case "gif":
                    string gifUrl = embed.Image?.Url ?? string.Empty;
                    if (string.IsNullOrEmpty(gifUrl))
                    {
                        Console.WriteLine("GIF URL is null or empty.");
                        await FollowupAsync("Error: GIF URL is missing.", ephemeral: true);
                        return;
                    }
                    string? gifFilePath = await ImageHelpers.DownloadFileFromEmbedAsync(gifUrl, "gif");
                    if (string.IsNullOrEmpty(gifFilePath))
                    {
                        Console.WriteLine("Failed to download the GIF.");
                        await FollowupAsync("Error: Failed to download GIF.", ephemeral: true);
                        return;
                    }
                    string? firstFrameFilePath = ImageHelpers.ExtractFirstFrame(gifFilePath);
                    if (string.IsNullOrEmpty(firstFrameFilePath))
                    {
                        Console.WriteLine("Failed to extract the first frame from the GIF.");
                        await FollowupAsync("Error: Failed to extract the first frame from the GIF.", ephemeral: true);
                        return;
                    }
                    StableSwarmAPI stableSwarmAPI = new();
                    SelectMenus selectMenus = new(commands, supaBase, stableSwarmAPI);
                    SocketInteractionContext context = Context;
                    await selectMenus.GenerateGif(firstFrameFilePath, context);
                    break;
                default:
                    Console.WriteLine($"Unknown generation type: {generationType}");
                    await FollowupAsync("Error: Unknown generation type.", ephemeral: true);
                    break;
            }
        }

        /// <summary>Handles the interaction when the 'delete' button is clicked, removing the associated message.</summary>
        /// <param name="customId">The custom ID associated with the button that triggered the interaction.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        [ComponentInteraction("delete:*")]
        public async Task DeleteButtonHandler(string customId)
        {
            if (ComponentHelpers.IsOnCooldown(Context.User, "delete"))
            {
                await RespondAsync("You are on cooldown. Please wait before trying again.", ephemeral: true);
                return;
            }
            if (Context.User.Id.ToString() != customId)
            {
                Console.WriteLine("Another user tried to click a button");
                await RespondAsync("Error: You cannot delete another users image.", ephemeral: true);
                return;
            }
            await DeferAsync();
            SocketMessageComponent? interaction = Context.Interaction as SocketMessageComponent;
            await interaction?.Message.DeleteAsync()!;
            await FollowupAsync("Message deleted successfully", ephemeral: true);
        }

        /// <summary>Handles the interaction when a vote button is clicked, updating the vote count for an image.</summary>
        /// <param name="customId">The custom ID associated with the button that triggered the interaction.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        [ComponentInteraction("vote:*")]
        public async Task VoteButtonHandler(string customId)
        {
            if (ComponentHelpers.IsOnCooldown(Context.User, "vote"))
            {
                await RespondAsync("You are on cooldown. Please wait before trying again.", ephemeral: true);
                return;
            }
            ISocketMessageChannel channel = Context.Channel;
            SocketMessageComponent? interaction = Context.Interaction as SocketMessageComponent;
            ulong messageId = interaction!.Message.Id;
            switch (customId)
            {
                case "up":
                    await Showcase.UpdateVoteAsync(channel, messageId, Context.User);
                    await RespondAsync("You upvoted this image!", ephemeral: true);
                    break;
                case "down":
                    await Showcase.UpdateVoteAsync(channel, messageId, Context.User);
                    await RespondAsync("You downvoted this image!", ephemeral: true);
                    break;
                default:
                    await RespondAsync("Invalid vote.", ephemeral: true);
                    break;
            }
        }

        /// <summary>Handles the interaction when the 'report' button is clicked, notifying staff of a reported image.</summary>
        /// <param name="userId">The user ID associated with the button that triggered the interaction.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        [ComponentInteraction("report:*")]
        public async Task ReportButtonHandler(string userId)
        {
            SocketGuildUser? user = Context.User as SocketGuildUser;
            SocketGuild guild = Context.Guild;
            if (ComponentHelpers.IsOnCooldown(user!, "report"))
            {
                await RespondAsync("You are on cooldown. Please wait before trying again.", ephemeral: true);
                return;
            }
            SocketUserMessage? message = (Context.Interaction as SocketMessageComponent)?.Message;
            Embed? GetEmbed = message?.Embeds.FirstOrDefault();
            SocketTextChannel? staffChannel = guild.TextChannels.FirstOrDefault(c => c.Name == "staff-chat-🔒");
            if (message != null && staffChannel != null)
            {
                Embed embed = new EmbedBuilder()
                    .WithTitle("Reported Message")
                    .WithDescription($"A message has been reported by {user!.Mention}. " +
                    $"\n\n<@{userId}> may have created an image that breaks the community rules. A mod needs to look at this ASAP!")
                    .AddField("Reported by", user.Mention, true)
                    .AddField("Message Link", $"[Jump to message]({message.GetJumpUrl()})", true)
                    .WithColor(Color.Red)
                    .WithTimestamp(DateTimeOffset.Now)
                    .Build();
                // Send a detailed report to the staff channel
                await staffChannel.SendMessageAsync(embed: embed);
                // Disable the button on the reported message
                MessageComponent component = new ComponentBuilder()
                    .WithButton("Reported", "report", ButtonStyle.Danger, disabled: true)
                    .Build();
                await (message as IUserMessage)?.ModifyAsync(msg => msg.Components = component)!;
                Embed response = new EmbedBuilder()
                    .WithTitle("Message Reported")
                    .WithDescription($"{user.Mention}, Thank you for reporting this message. Our community's safety and integrity are of utmost importance to us.")
                    .AddField("Report Received", "Your report has been successfully submitted to our staff team.")
                    .AddField("Next Steps", "A staff member will review the reported content shortly. If they determine that it violates our community rules, " +
                    "appropriate actions will be taken to address the issue. Deletion of the post has been disabled while staff looks into the issue.")
                    .WithFooter("Thank you for helping to maintain a safe and respectful environment. If you have any further information please contact a mod.")
                    .WithColor(Color.Gold)
                    .WithCurrentTimestamp()
                    .Build();
                // Send the embed in the original channel
                await RespondAsync(embed: response, ephemeral: true);
            }
            else
            {
                await RespondAsync("Failed to report the message. Please try again or contact an admin.", ephemeral: true);
            }
        }

        /// <summary>Handles the interaction when a user selects an image, providing options based on the action type.</summary>
        /// <param name="customId">The custom ID associated with the select menu that triggered the interaction.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        [ComponentInteraction("choose_image:*")]
        public async Task ChooseImageButtonHandler(string customId)
        {
            Console.WriteLine($"Custom ID: {customId}"); // Debugging
            if (ComponentHelpers.IsOnCooldown(Context.User, "choose_image"))
            {
                await RespondAsync("You are on cooldown. Please wait before trying again.", ephemeral: true);
                return;
            }
            if (Context.User is SocketGuildUser user)
            {
                Users? userInfo = await supaBase.GetUserByDiscordId(user.Id.ToString());
                if (userInfo == null)
                {
                    MessageComponent components = new ComponentBuilder()
                    .WithButton("Link Account", style: ButtonStyle.Link, url: "https://hartsy.ai")
                    .Build();
                    Embed embed = new EmbedBuilder()
                        .WithTitle("Link Your Hartsy.AI Account")
                        .WithDescription($"{user.Mention}, you have not linked your Discord account with your Hartsy.AI account. Make a FREE account " +
                                                            "and log into Hartsy.AI using your Discord credentials. If you have already done that and are still having issues" +
                                                            " contact an admin. This may be a bug.\n\nGo to [Hartsy.ai](https://hartsy.ai) to check sub status or add GPUTs")
                        .WithColor(Color.Blue)
                        .WithTimestamp(DateTimeOffset.Now)
                        .Build();
                    await user.SendMessageAsync(embed: embed, components: components);
                    return;
                }
                string? subStatus = userInfo.PlanName;
                if (subStatus == null)
                {
                    await RespondAsync("Error: Subscription status not found.", ephemeral: true);
                    return;
                }
                try
                {
                    string[] splitCustomId = customId.Split(":");
                    ulong userId = ulong.Parse(splitCustomId[1]);
                    string type = splitCustomId[0].ToString();
                    SocketMessageComponent? interaction = Context.Interaction as SocketMessageComponent;
                    string username = interaction!.User.Username;
                    ulong messageId = interaction!.Message.Id;
                    SelectMenuBuilder selectMenu = new();
                    Console.WriteLine($"Title: {interaction.Message.Embeds.FirstOrDefault()?.Title}"); // Debugging
                    if ((type == "showcase" || type == "save") && interaction.Message.Embeds.FirstOrDefault()?.Title == "✨ GIF Generation Completed!")
                    {
                        selectMenu = new SelectMenuBuilder()
                            .WithPlaceholder("Confirm GIF Selection")
                            .AddOption("Selected GIF", "new_image.gif");
                    }
                    else
                    {
                        selectMenu = new SelectMenuBuilder()
                            .WithPlaceholder("Select an image")
                            .AddOption("Image 1", "image_0.jpeg")
                            .AddOption("Image 2", "image_1.jpeg")
                            .AddOption("Image 3", "image_2.jpeg")
                            .AddOption("Image 4", "image_3.jpeg");
                    }
                    if (type == "i2i")
                    {
                        selectMenu.WithCustomId($"select_image:i2i:{userId}:{messageId}");
                        ComponentBuilder selectBuilder = new ComponentBuilder()
                            .WithSelectMenu(selectMenu);
                        EmbedBuilder itiEmbed = new EmbedBuilder()
                            .WithTitle("Select Image")
                            .WithDescription("Choose an image and we will generate 4 new images based off of that.")
                            .WithColor(Color.Purple)
                            .WithCurrentTimestamp();
                        await RespondAsync(embed: itiEmbed.Build(), components: selectBuilder.Build(), ephemeral: true);
                        return;
                    }
                    else if (type == "save")
                    {
                        selectMenu.WithCustomId($"select_image:add:{userId}:{messageId}");
                        ComponentBuilder selectBuilder = new ComponentBuilder()
                            .WithSelectMenu(selectMenu);
                        EmbedBuilder saveEmbed = new EmbedBuilder()
                            .WithTitle("Select Image")
                            .WithDescription("Select the image you wish to save to the gallery")
                            .WithColor(Color.Blue)
                            .WithCurrentTimestamp();
                        await RespondAsync(embed: saveEmbed.Build(), components: selectBuilder.Build(), ephemeral: true);
                        return;
                    }
                    else if (type == "gif")
                    {
                        selectMenu.WithCustomId($"select_image:gif:{userId}:{messageId}");
                        ComponentBuilder selectBuilder = new ComponentBuilder()
                            .WithSelectMenu(selectMenu);
                        EmbedBuilder gifEmbed = new EmbedBuilder()
                            .WithTitle("Select Images")
                            .WithDescription("Select the images you wish to create a GIF from")
                            .WithColor(Color.Green)
                            .WithCurrentTimestamp();
                        await RespondAsync(embed: gifEmbed.Build(), components: selectBuilder.Build(), ephemeral: true);
                        return;
                    }
                    else if (type == "showcase")
                    {
                        selectMenu.WithCustomId($"select_image:showcase:{userId}:{messageId}");
                        ComponentBuilder selectBuilder = new ComponentBuilder()
                            .WithSelectMenu(selectMenu);
                        EmbedBuilder showcaseEmbed = new EmbedBuilder()
                            .WithTitle("Select Image for Showcase")
                            .WithDescription("Select an image to add to the #showcase channel. Other users will be able to vote on your image.")
                            .WithColor(Color.Blue)
                            .WithCurrentTimestamp();
                        await RespondAsync(embed: showcaseEmbed.Build(), components: selectBuilder.Build(), ephemeral: true);
                        return;
                    }
                }
                catch
                {
                    await RespondAsync("Error: Failed to send a direct message to the user.", ephemeral: true);
                }
            }
        }

        /// <summary>Handles the 'interrupt_gif' button when clicked, sending a request to interrupt the GIF generation process.</summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        [ComponentInteraction("interrupt:*")]
        public async Task HandleInterruptButton(string customId)
        {
            string[] splitCustomId = customId.Split(":");
            string userId = splitCustomId[0];
            string sessionId = splitCustomId[1];
            if (Context.User.Id.ToString() != userId)
            {
                Embed embed = new EmbedBuilder()
                .WithTitle("Error")
                .WithDescription("You cannot interrupt another user's image.")
                .WithColor(Color.Red)
                .Build();
                await RespondAsync(embed: embed, ephemeral: true);
                return;
            }
            await DeferAsync();
            StableSwarmAPI swarmAPI = new();
            await swarmAPI.InterruptGeneration(sessionId);
            EmbedBuilder interruptEmbed = new EmbedBuilder()
                .WithTitle("GIF Generation Interrupted")
                .WithDescription("The GIF generation process has been successfully interrupted by the user.")
                .WithColor(Color.Red)
                .WithCurrentTimestamp();
            if (Context.Interaction is SocketMessageComponent message)
            {
                ComponentBuilder componentBuilder = new ComponentBuilder()
                    .WithButton("Interrupt", $"interrupt:{userId}:{sessionId}", ButtonStyle.Secondary, disabled: true)
                    .WithButton("Delete", $"delete:{userId}", ButtonStyle.Danger)
                    .WithButton("Regenerate", $"regenerate:{userId}:{message.Message.Id}", ButtonStyle.Success);

                await message.ModifyOriginalResponseAsync(msg =>
                {
                    msg.Content = string.Empty;
                    msg.Embeds = new[] { interruptEmbed.Build() };
                    msg.Components = componentBuilder.Build();
                });
            }
        }
    }
}
