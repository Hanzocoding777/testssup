using Database;
using Database.Services;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using Support.Entities;
using Support.Logic;
using Support.Utilities;
using Support.Utilities.TicketMethods;
using Formatter = DSharpPlus.Formatter;

namespace Support.Listeners;

public class ComponentListener
{
    internal static List<DiscordChannel> ClaimedTickets = new();
    internal static Dictionary<DiscordMessage, DiscordChannel> OpenUserCalls = new();

    /// <summary>
    /// Represents a list of closing ticket at the moment
    /// Key = ticket channel id
    /// </summary>
    internal static List<ulong> ClosingTickets = new();

    // кд вызова админа для пользователя
    internal static Dictionary<DiscordUser, DateTime> OpenCallUserCooldown = new();

    // Кд вызова админа по каналу
    internal static Dictionary<ulong, DateTime> OpenCallChannelCooldown = new();

    [AsyncListener(EventTypes.ComponentInteractionCreated)]
    public static async Task ClientOnInteractionReceived(DiscordClient client, ComponentInteractionCreateEventArgs args)
    {
        switch (args.Id)
        {
            case var customid when customid.Contains("CreateTicket"):
            {
                var member = await args.Guild.GetMemberAsync(args.User.Id);

                var responeEmbedBuilder = new DiscordEmbedBuilder();

                if (member.Roles.Any(x => x.Id == Bot.Config.Roles.StaffRoleId || x.Id == Bot.Config.Roles.OperatorRoleId || x.Id == Bot.Config.Roles.HeadRoleId || x.Id == Bot.Config.Roles.GrandModeratorRoleId || x.Id == Bot.Config.Roles.ModeratorRoleId))
                {
                    responeEmbedBuilder.WithDescription("\u274c Администрация не может создать обращение.");
                    responeEmbedBuilder.WithColor(DiscordColor.Red);

                    await args.Interaction.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, new DiscordInteractionResponseBuilder().AddEmbed(responeEmbedBuilder).AsEphemeral());
                    break;
                }

                var profile = await MongoManager.GetUserAsync(args.User);
                
                if (profile.TicketBlockDateUnix != null)
                {
                    var builder = new DiscordMessageBuilder()
                        .WithEmbed(new DiscordEmbedBuilder()
                            .WithDescription($"❌ Вы были заблокированы в системе создания обращения.\n" +
                                             $"Вы сможете создать новое обращение через: {Formatter.Timestamp((DateTime)profile.TicketBlockDateUnix?.ToDateTime()!)}")
                            .WithColor(DiscordColor.Red)
                        );

                    await args.Interaction.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, new DiscordInteractionResponseBuilder(builder).AsEphemeral());
                    break;
                }


                if (profile.Ticket != null)
                {
                    bool succes = true;
                    DiscordChannel? ticketChannelReceived = null;

                    try
                    {
                        ticketChannelReceived = args.Guild.GetChannel(profile.Ticket.TicketChannelId);
                    }
                    catch (Exception)
                    {
                        succes = false;
                    }

                    if (succes && ticketChannelReceived != null)
                    {
                        responeEmbedBuilder.WithDescription("❌ У вас уже имеется ранее открытое обращение.\n" +
                                                            $"Ваше обращение - {ticketChannelReceived.Mention}");
                        responeEmbedBuilder.WithColor(DiscordColor.Red);

                        await args.Interaction.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, new DiscordInteractionResponseBuilder().AddEmbed(responeEmbedBuilder).AsEphemeral());
                        break;
                    }
                }

                DiscordInteractionResponseBuilder modal;
                
                if (customid.Split("-")[0].Contains("BetaTest"))
                {
                    modal = new DiscordInteractionResponseBuilder()
                        .WithTitle("Обращение")
                        .WithCustomId("CreateTicketModal")
                        .AddComponents(new TextInputComponent("Тема вашего обращения:", "TicketSubject", "Beta Test", value: "Beta Test", required: false, max_length: 50))
                        .AddComponents(new TextInputComponent("Обращение:", "TicketDescription", "Привет, хочу получить доступ к бета тесту", value: "Привет, хочу получить доступ к бета тесту", required: false, style: TextInputStyle.Paragraph, max_length: 500));
                }
                else
                {
                    modal = new DiscordInteractionResponseBuilder()
                        .WithTitle("Обращение")
                        .WithCustomId("CreateTicketModal")
                        .AddComponents(new TextInputComponent("Тема вашего обращения:", "TicketSubject", "Жалоба | Вопрос | Другое", required: false, max_length: 50))
                        .AddComponents(new TextInputComponent("Обращение:", "TicketDescription", "Привет, меня оскорбили в канале, есть видео запись, хотел бы подать жалобу на игрока...", required: false, style: TextInputStyle.Paragraph, max_length: 500));
                }

                await args.Interaction.CreateResponseAsync(InteractionResponseType.Modal, modal);
                break;
            }

            case "CreateVoiceTicket":
            {
                var member = await args.Guild.GetMemberAsync(args.User.Id);

                var responeEmbedBuilder = new DiscordEmbedBuilder();

                if (member.Roles.Any(x => x.Id == Bot.Config.Roles.StaffRoleId || x.Id == Bot.Config.Roles.OperatorRoleId || x.Id == Bot.Config.Roles.HeadRoleId || x.Id == Bot.Config.Roles.GrandModeratorRoleId || x.Id == Bot.Config.Roles.ModeratorRoleId))
                {
                    responeEmbedBuilder.WithDescription("\u274c Администрация не может создать обращение.");
                    responeEmbedBuilder.WithColor(DiscordColor.Red);

                    await args.Interaction.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, new DiscordInteractionResponseBuilder().AddEmbed(responeEmbedBuilder).AsEphemeral());
                    break;
                }

                if (member.VoiceState?.Channel == null)
                {
                    responeEmbedBuilder.WithDescription("\u274c Вам необходимо находиться в голосовом канале для вызова администрации.");
                    responeEmbedBuilder.WithColor(DiscordColor.Red);

                    await args.Interaction.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, new DiscordInteractionResponseBuilder().AddEmbed(responeEmbedBuilder).AsEphemeral());
                    break;
                }

                if (OpenCallChannelCooldown.TryGetValue(member.VoiceState.Channel.Id, out var dateTimeChannel))
                {
                    if (DateTime.Now < dateTimeChannel)
                    {
                        responeEmbedBuilder.WithDescription($"❌ Пожалуйста, подождите перед созданием нового обращения.\n" +
                                                            $"Вы сможете создать новое обращение {Formatter.Timestamp(dateTimeChannel)}");
                        responeEmbedBuilder.WithColor(DiscordColor.Red);

                        await args.Interaction.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, new DiscordInteractionResponseBuilder().AddEmbed(responeEmbedBuilder).AsEphemeral());
                        break;
                    }

                    OpenCallChannelCooldown.Remove(member.VoiceState.Channel.Id);
                }

                if (OpenCallUserCooldown.TryGetValue(member, out var dateTimeUser))
                {
                    if (DateTime.Now < dateTimeUser)
                    {
                        responeEmbedBuilder.WithDescription($"❌ Пожалуйста, подождите перед созданием нового обращения.\n" +
                                                            $"Вы сможете создать новое обращение {Formatter.Timestamp(dateTimeChannel)}");
                        responeEmbedBuilder.WithColor(DiscordColor.Red);

                        await args.Interaction.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, new DiscordInteractionResponseBuilder().AddEmbed(responeEmbedBuilder).AsEphemeral());
                        break;
                    }

                    OpenCallUserCooldown.Remove(member);
                    
                }

                var profile = await MongoManager.GetUserAsync(args.User);

                if (profile.TicketBlockDateUnix != null)
                {
                    var builder = new DiscordMessageBuilder()
                        .WithEmbed(new DiscordEmbedBuilder()
                            .WithDescription($"❌ Вы были заблокированы в системе создания обращения.\n" +
                                             $"Вы сможете создать новое обращение через: {Formatter.Timestamp((DateTime)profile.TicketBlockDateUnix?.ToDateTime()!)}")
                            .WithColor(DiscordColor.Red)
                        );

                    await args.Interaction.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, new DiscordInteractionResponseBuilder(builder).AsEphemeral());
                    break;
                }

                var modal = new DiscordInteractionResponseBuilder()
                    .WithTitle("Обращение")
                    .WithCustomId("CreateVoiceTicketModal")
                    .AddComponents(new TextInputComponent("Тема вашего обращения:", "TicketSubject", "Жалоба | Вопрос | Другое", required: false, max_length: 50))
                    .AddComponents(new TextInputComponent("Обращение:", "TicketDescription", "Привет, меня тут человек в голосовом канале оскорбляет...", required: false, style: TextInputStyle.Paragraph, max_length: 500));

                await args.Interaction.CreateResponseAsync(InteractionResponseType.Modal, modal);
                break;
            }

            case "СloseVoiceTicket":
            {
                var member = await args.Guild.GetMemberAsync(args.User.Id);

                var responeEmbedBuilder = new DiscordEmbedBuilder();

                if (!member.Roles.Any(x => x.Id == Bot.Config.Roles.StaffRoleId || x.Id == Bot.Config.Roles.OperatorRoleId || x.Id == Bot.Config.Roles.HeadRoleId || x.Id == Bot.Config.Roles.GrandModeratorRoleId || x.Id == Bot.Config.Roles.ModeratorRoleId))
                {
                    responeEmbedBuilder.WithDescription("\u274c Обращение может закрыть только администрация сервера.");
                    responeEmbedBuilder.WithColor(DiscordColor.Red);

                    await args.Interaction.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, new DiscordInteractionResponseBuilder().AddEmbed(responeEmbedBuilder).AsEphemeral());
                }

                if (!OpenUserCalls.ContainsKey(args.Message) || member.VoiceState == null || OpenUserCalls.GetValueOrDefault(args.Message) != member.VoiceState.Channel)
                {
                    responeEmbedBuilder.WithDescription("\u274c Вы не находитесь в канале в котором произошла проблема.");
                    responeEmbedBuilder.WithColor(DiscordColor.Red);

                    await args.Interaction.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, new DiscordInteractionResponseBuilder().AddEmbed(responeEmbedBuilder).AsEphemeral());
                    break;
                }

                var callAdminEmbedReceived = args.Message.Embeds[0];

                var callAdminEmbed = new DiscordEmbedBuilder()
                    .WithTimestamp(DateTime.Now)
                    .WithTitle($"{callAdminEmbedReceived.Title}")
                    .WithDescription($"{callAdminEmbedReceived.Description.Replace($"<#{member.VoiceState.Channel.Id}>", $"`🔊{member.VoiceState.Channel.Name}`")}")
                    .WithFooter($"Завершил: {args.User.Id} • {args.User.Username}", $"{args.User.AvatarUrl}")
                    .WithImageUrl(Constants.EmptyLineImageUrl)
                    .WithColor(new DiscordColor("782e36"));

                switch (callAdminEmbedReceived.Fields?.Count)
                {
                    case 1 when callAdminEmbedReceived.Fields[0].Name == "Тема:":
                        callAdminEmbed.AddField("Тема:", $"{callAdminEmbedReceived.Fields[0].Value}");
                        break;
                    case 1:
                        callAdminEmbed.AddField("Описание:", $"{callAdminEmbedReceived.Fields[0].Value}");
                        break;
                    case 2:
                        callAdminEmbed.AddField("Тема:", $"{callAdminEmbedReceived.Fields[0].Value}");
                        callAdminEmbed.AddField("Описание:", $"{callAdminEmbedReceived.Fields[1].Value}");
                        break;
                }

                var callAdminMessageCloseBuilder = new DiscordMessageBuilder()
                    .WithAllowedMentions(Mentions.All)
                    .WithContent(args.Guild.GetRole(Bot.Config.Roles.OperatorRoleId).Mention)
                    .WithEmbed(callAdminEmbed);

                callAdminMessageCloseBuilder.AddComponents(new DiscordComponent[]
                {
                    new DiscordButtonComponent(ButtonStyle.Danger, "none", "Завершить", true)
                });

                await args.Message.ModifyAsync(callAdminMessageCloseBuilder);
                OpenUserCalls.Remove(args.Message);

                await args.Interaction.CreateResponseAsync(InteractionResponseType.UpdateMessage);
                break;
            }

            /*case "FAQ":
            {
                switch (args.Values.First())
                {
                    case "StatisticFAQ":
                    {
                        var builder = new DiscordInteractionResponseBuilder()
                            .AddEmbed(new DiscordEmbedBuilder()
                                .WithTitle("Что такое статистика?")
                                .WithDescription($"Мы упростили поиск напарника добавив канал статистика, чтобы любой мог увидеть информацию из игры RUST {args.Guild.GetChannel(Bot.Config.Channels.StatisticChannelId).Mention}")
                                .AddField("Команды:", "`/регистрация` `ссылка на steam профиль` - привязать steam аккаунт\n" +
                                                      "`/статистика` - узнасть свою статистику\n" +
                                                      "`/статистика` `@name` - узнать статистику игрока")
                                .WithColor(DiscordColor.Red)
                            );

                        await args.Interaction.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, builder.AsEphemeral());
                        break;
                    }
                }
                break;
            }*/

            case "AcceptTicket":
            {
                if (ClosingTickets.Contains(args.Interaction.Channel.Id))
                {
                    var closingBuilder = new DiscordInteractionResponseBuilder()
                        .AddEmbed(new DiscordEmbedBuilder()
                            .WithDescription("❌ Тикет уже закрывается!")
                            .WithColor(DiscordColor.Red)
                        );

                    await args.Interaction.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, closingBuilder.AsEphemeral());
                    break;
                }
                
                var member = await args.Guild.GetMemberAsync(args.User.Id);

                var responeEmbedBuilder = new DiscordEmbedBuilder();
                responeEmbedBuilder.WithColor(DiscordColor.Red);

                if (!member.Roles.Any(x => x.Id == Bot.Config.Roles.StaffRoleId || x.Id == Bot.Config.Roles.OperatorRoleId || x.Id == Bot.Config.Roles.HeadRoleId || x.Id == Bot.Config.Roles.GrandModeratorRoleId || x.Id == Bot.Config.Roles.ModeratorRoleId))
                {
                    responeEmbedBuilder.WithDescription("❌ **Управлять обращением может только администрация сервера.**");
                    await args.Interaction.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, new DiscordInteractionResponseBuilder().AddEmbed(responeEmbedBuilder).AsEphemeral());
                    break;
                }

                if (ClaimedTickets.Contains(args.Channel))
                {
                    responeEmbedBuilder.WithTitle("❌ **Произошла ошибка при попытке забрать тикет.**");
                    responeEmbedBuilder.WithDescription("Скорее всего это связано с тем, что его уже забрал другой администратор.");

                    await args.Interaction.CreateResponseAsync(InteractionResponseType.UpdateMessage, new DiscordInteractionResponseBuilder().AddEmbed(responeEmbedBuilder).AsEphemeral());
                    break;
                }

                // Добавляеем номер канала, чтобы сигнализиривать, что тикет принят
                ClaimedTickets.Add(args.Channel);

                var profile = MongoManager.GetUserByTicketChannel(args.Channel.Id);

                if (profile?.Ticket == null)
                    throw new Exception("Ticket profile is null");

                if (profile.Ticket.TicketStatus == TicketStatus.Completed || profile.Ticket.TicketStatus == TicketStatus.WaitingSolution)
                {
                    responeEmbedBuilder.WithDescription("❌ Обращение было уже принято другим администратором.");
                    break;
                }

                bool succes = true;

                DiscordMember? ticketMember = null;

                try
                {
                    ticketMember = await args.Guild.GetMemberAsync(profile.UserId);
                }
                catch (Exception)
                {
                    succes = false;
                }

                if (succes == false)
                {
                    var closeTicketMessage = new DiscordMessageBuilder()
                        .AddEmbed(new DiscordEmbedBuilder()
                            .WithTitle("Пользователь вышел с сервера")
                            .WithDescription("Пользователь покинул сервер, поэтому он больше не имеет доступ к тикету. Тикет будет скоро закрыт автоматически.")
                            .WithColor(DiscordColor.Red)
                        );

                    await args.Interaction.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, new DiscordInteractionResponseBuilder(closeTicketMessage));
                    profile.Ticket.TicketSupportUserId = args.Interaction.User.Id;
                    await MongoManager.UpdateAsync(profile);
                    await CloseTicket.CloseTicketAsync(profile, "Пользователь вышел с сервера");
                }
                else
                {
                    var acceptBuilderMessage = new DiscordMessageBuilder()
                        .WithAllowedMentions(Mentions.All)
                        .WithContent(ticketMember?.Mention)
                        .AddEmbed(new DiscordEmbedBuilder()
                            .WithDescription($"🦸 **Ваше обращение принял администратор** {member.Mention}")
                            .WithColor(DiscordColor.Red)
                        );

                    await args.Message.Channel.SendMessageAsync(acceptBuilderMessage);

                    // Убираем канал у роли Support
                    await args.Channel.AddOverwriteAsync(args.Guild.GetRole(Bot.Config.Roles.OperatorRoleId), deny: Permissions.AccessChannels);

                    // Прописываем администратора принявшего тикет
                    await args.Channel.AddOverwriteAsync(member, allow: Permissions.AccessChannels | Permissions.SendMessages | Permissions.AttachFiles | Permissions.EmbedLinks | Permissions.ReadMessageHistory);

                    // Добавляем сообщения для администратора, принявшего тикет
                    var ephemeralModeratorMessage = new DiscordMessageBuilder()
                        .AddEmbed(new DiscordEmbedBuilder()
                            .WithTitle("Вы приняли обращение")
                            .WithDescription("Вы приняли обращение, теперь его можете видеть только вы и **старшая** администрация. \n \n " +
                                             "Поздоровайтесь с пользователем в чате и помогите с решением его проблемы!")
                            .WithColor(DiscordColor.Red)
                        );

                    await args.Interaction.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, new DiscordInteractionResponseBuilder(ephemeralModeratorMessage).AsEphemeral());

                    profile.Ticket.TicketStatus = TicketStatus.WaitingSolution;
                    profile.Ticket.TicketSupportUserId = args.User.Id;
                    await MongoManager.UpdateAsync(profile);

                    ClaimedTickets.Remove(args.Channel);
                }
                break;
            }

            case "ControlTicket":
            {
                if (ClosingTickets.Contains(args.Interaction.Channel.Id))
                {
                    var closingBuilder = new DiscordInteractionResponseBuilder()
                        .AddEmbed(new DiscordEmbedBuilder()
                            .WithDescription("❌ Тикет уже закрывается!")
                            .WithColor(DiscordColor.Red)
                        );

                    await args.Interaction.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, closingBuilder.AsEphemeral());
                    break;
                }
                
                var member = await args.Guild.GetMemberAsync(args.User.Id);

                var responeEmbedBuilder = new DiscordEmbedBuilder();
                responeEmbedBuilder.WithColor(DiscordColor.Red);

                if (!member.Roles.Any(x => x.Id == Bot.Config.Roles.StaffRoleId || x.Id == Bot.Config.Roles.OperatorRoleId || x.Id == Bot.Config.Roles.HeadRoleId || x.Id == Bot.Config.Roles.GrandModeratorRoleId || x.Id == Bot.Config.Roles.ModeratorRoleId))
                {
                    responeEmbedBuilder.WithDescription("❌ **Управлять обращением может только администрация сервера.**");
                    await args.Interaction.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, new DiscordInteractionResponseBuilder().AddEmbed(responeEmbedBuilder).AsEphemeral());
                    break;
                }

                var components = new List<DiscordComponent>();

                var profile = MongoManager.GetUserByTicketChannel(args.Channel.Id);

                if (profile?.Ticket == null)
                    throw new Exception("Ticket profile is null");

                var ticketUser = await client.GetUserAsync(profile.UserId);

                var responseEmbedBuilder = new DiscordEmbedBuilder();
                responseEmbedBuilder.WithTitle("Панель управления обращением");
                responseEmbedBuilder.WithDescription($"Описание обращения {args.Channel.Mention}:");
                responseEmbedBuilder.AddField("Создатель обращения:", $"{ticketUser.Mention} - {ticketUser.Username}");
                responseEmbedBuilder.WithColor(DiscordColor.Red);

                switch (profile.Ticket.TicketStatus)
                {
                    case TicketStatus.Opened:
                        {
                            components.Add(new DiscordButtonComponent(ButtonStyle.Success, "AcceptTicket", "Принять обращение", emoji: new DiscordComponentEmoji(DiscordEmoji.FromUnicode("✅"))));
                            responseEmbedBuilder.AddField("Статус:", $"```{DiscordEmoji.FromName(client, ":green_circle:")} Открыто```");
                            break;
                        }
                        
                    case TicketStatus.WaitingSolution:
                        {
                            var ticketSupportUser = await client.GetUserAsync((ulong)profile.Ticket.TicketSupportUserId!);

                            responeEmbedBuilder.AddField("Обращение принял:", $"{ticketSupportUser.Mention} - {ticketSupportUser.Username}");
                            components.Add(new DiscordButtonComponent(ButtonStyle.Success, "CloseTicket", "Завершить обращение", emoji: new DiscordComponentEmoji(DiscordEmoji.FromUnicode("✅"))));
                            responseEmbedBuilder.AddField("Статус:", $"```{DiscordEmoji.FromName(client, ":yellow_circle:")} В процессе решения```");
                            break;
                        }
                    case TicketStatus.Completed:
                        {
                            var ticketSupportUser = await client.GetUserAsync((ulong)profile.Ticket.TicketSupportUserId!);

                            responeEmbedBuilder.AddField("Обращение принял:", $"{ticketSupportUser.Mention} - {ticketSupportUser.Username}");
                            components.Add(new DiscordButtonComponent(ButtonStyle.Success, "CloseTicket", "Завершить обращение", true, emoji: new DiscordComponentEmoji(DiscordEmoji.FromUnicode("✅"))));
                            responseEmbedBuilder.AddField("Статус:", $"``` {DiscordEmoji.FromName(client, ":red_circle:")} Завершено```");
                            break;
                        }
                }

                responseEmbedBuilder.AddField("Тема обращения:", $"```{profile.Ticket.TicketSubject} ```");
                responseEmbedBuilder.AddField("Описание обращения:", $"```{profile.Ticket.TicketDescription} ```");

                if (member.Roles.Any(x => x.Id == Bot.Config.Roles.HeadRoleId || x.Id == Bot.Config.Roles.GrandModeratorRoleId || x.Id == Bot.Config.Roles.ModeratorRoleId))
                {
                    components.Add(new DiscordButtonComponent(ButtonStyle.Danger, "HardCloseTicket", "Закрыть обращение", false, new DiscordComponentEmoji(DiscordEmoji.FromUnicode("⛔"))));
                }
                else
                {
                    components.Add(new DiscordButtonComponent(ButtonStyle.Danger, "HardCloseTicket", "Закрыть обращение", true, new DiscordComponentEmoji(DiscordEmoji.FromUnicode("⛔"))));
                }

                var response = new DiscordInteractionResponseBuilder()
                    .AddEmbed(responseEmbedBuilder)
                    .AddComponents(components);

                await args.Interaction.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, response.AsEphemeral());
                break;
            }

            case "CloseTicket":
            {
                if (ClosingTickets.Contains(args.Interaction.Channel.Id))
                {
                    var closingBuilder = new DiscordInteractionResponseBuilder()
                        .AddEmbed(new DiscordEmbedBuilder()
                            .WithDescription("❌ Тикет уже закрывается!")
                            .WithColor(DiscordColor.Red)
                        );

                    await args.Interaction.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, closingBuilder.AsEphemeral());
                    break;
                }
                
                var profile = MongoManager.GetUserByTicketChannel(args.Channel.Id);

                if (profile?.Ticket == null)
                    throw new Exception("Ticket profile is null");

                DiscordMember ticketMember;
                
                try
                {
                    ticketMember = await args.Guild.GetMemberAsync(profile.UserId);
                }
                catch (Exception)
                {
                    var leftUserReponse = new DiscordInteractionResponseBuilder()
                        .AddEmbed(new DiscordEmbedBuilder()
                            .WithTitle("Пользователь вышел с сервера")
                            .WithDescription("Пользователь покинул сервер и обращение будет автоматически закрыто.")
                            .WithColor(DiscordColor.Red)
                        );
                    
                    await args.Interaction.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, leftUserReponse);
                    
                    ClosingTickets.Add(args.Interaction.Channel.Id);
                    await CloseTicket.CloseTicketAsync(profile, "Пользователь вышел с сервера");
                    break;
                }

                var responeEmbedBuilder = new DiscordEmbedBuilder();
                responeEmbedBuilder.WithColor(DiscordColor.Red);

                if (profile.Ticket.TicketStatus == TicketStatus.Completed)
                {
                    responeEmbedBuilder.WithDescription("Обращение уже было завершено.");

                    await args.Interaction.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, new DiscordInteractionResponseBuilder().AddEmbed(responeEmbedBuilder).AsEphemeral());
                    break;
                }

                if (profile.Ticket.TicketSupportUserId != args.User.Id)
                {
                    responeEmbedBuilder.WithDescription("Обращение может завершить только тот администратор, который его принял.");

                    await args.Interaction.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, new DiscordInteractionResponseBuilder().AddEmbed(responeEmbedBuilder).AsEphemeral());
                    break;
                }

                profile.Ticket.TicketAutoCloseDateUnix = DateTime.Now.AddHours(8).ToUnixTimestamp();
                profile.Ticket.TicketStatus = TicketStatus.Completed;
                await MongoManager.UpdateAsync(profile);

                var response = new DiscordMessageBuilder()
                    .WithAllowedMentions(Mentions.All)
                    .WithContent((await client.GetUserAsync(profile.UserId)).Mention)
                    .AddEmbed(new DiscordEmbedBuilder()
                        .WithTitle("Администратор хочет закрыть обращение")
                        .WithDescription("Если у вас не осталось вопросов, то вы можете закрыть обращения.\n\n" +
                                         $"Обращение будет автоматически закрыто через: {Formatter.Timestamp((DateTime)profile.Ticket.TicketAutoCloseDateUnix.Value.ToDateTime())}")
                        .WithColor(DiscordColor.Red)
                    ).AddComponents(new DiscordComponent[]
                    {
                        new DiscordButtonComponent(ButtonStyle.Success, "ContinueTicket", "Остались вопросы", emoji: new DiscordComponentEmoji(DiscordEmoji.FromUnicode("✅"))),
                        new DiscordButtonComponent(ButtonStyle.Danger, "SuccesClose", "Нет вопросов", emoji: new DiscordComponentEmoji(DiscordEmoji.FromUnicode("⛔"))),
                    });

                if (profile.Ticket.TicketSupportUserId != null)
                {
                    // Забираем доступ к отправлению сообщения у принявшего обращение
                    var staffMember = await args.Guild.GetMemberAsync((ulong)profile.Ticket.TicketSupportUserId);
                    await args.Channel.AddOverwriteAsync(staffMember, allow: Permissions.AccessChannels | Permissions.AttachFiles | Permissions.EmbedLinks | Permissions.ReadMessageHistory, deny: Permissions.SendMessages);
                }

                // Забираем доступ к отправлению сообщения у пользователя создавшего обращение
                await args.Channel.AddOverwriteAsync(ticketMember, allow: Permissions.AccessChannels | Permissions.AttachFiles | Permissions.EmbedLinks | Permissions.ReadMessageHistory, deny: Permissions.SendMessages);

                await args.Channel.SendMessageAsync(response);
                await args.Interaction.CreateResponseAsync(InteractionResponseType.DeferredMessageUpdate);
                break;
            }

            case "SuccesClose":
            {
                if (ClosingTickets.Contains(args.Interaction.Channel.Id))
                {
                    var closingBuilder = new DiscordInteractionResponseBuilder()
                        .AddEmbed(new DiscordEmbedBuilder()
                            .WithDescription("❌ Тикет уже закрывается!")
                            .WithColor(DiscordColor.Red)
                        );

                    await args.Interaction.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, closingBuilder.AsEphemeral());
                    break;
                }

                var profile = MongoManager.GetUserByTicketChannel(args.Channel.Id);

                if (profile?.Ticket == null)
                    throw new Exception("Ticket profile is null");

                var ticketMember = await Bot.Client.GetUserAsync(profile.UserId);

                if (args.User != ticketMember)
                {
                    var wrongUserBuilder = new DiscordInteractionResponseBuilder()
                        .AddEmbed(new DiscordEmbedBuilder()
                            .WithTitle("Ошибка использования")
                            .WithDescription("❌ Вы не можете использовать эту интеракцию. Эту интеракцию может использовать только владелец обращения.")
                            .WithColor(DiscordColor.Red)
                        );

                    await args.Interaction.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, wrongUserBuilder.AsEphemeral());
                    break;
                }

                var modalResponse = new DiscordInteractionResponseBuilder()
                    .WithTitle("Отзыв о качестве решения обращения")
                    .WithCustomId("SuccesCloseModal")
                    .AddComponents(new TextInputComponent("Оценка от 0 до 10", "Evaluation", "5", required: false, max_length: 2))
                    .AddComponents(new TextInputComponent("Отзыв, ваши предлоежния", "Review", "Помогли в моей проблеме быстро", required: false, style: TextInputStyle.Paragraph, max_length: 500));

                await args.Interaction.CreateResponseAsync(InteractionResponseType.Modal, modalResponse);
                break;
            }

            case "ContinueTicket":
            {
                if (ClosingTickets.Contains(args.Interaction.Channel.Id))
                {
                    var closingBuilder = new DiscordInteractionResponseBuilder()
                        .AddEmbed(new DiscordEmbedBuilder()
                            .WithDescription("❌ Тикет уже закрывается!")
                            .WithColor(DiscordColor.Red)
                        );

                    await args.Interaction.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, closingBuilder.AsEphemeral());
                    break;
                }
                
                var profile = MongoManager.GetUserByTicketChannel(args.Channel.Id);

                if (profile?.Ticket == null)
                    throw new Exception("Ticket profile is null");

                var ticketMember = await args.Guild.GetMemberAsync(profile.UserId);

                if (args.User != ticketMember)
                {
                    var wrongUserBuilder = new DiscordInteractionResponseBuilder()
                        .AddEmbed(new DiscordEmbedBuilder()
                            .WithTitle("Ошибка использования")
                            .WithDescription("❌ Вы не можете использовать эту интеракцию. Эту интеракцию может использовать только владелец обращения.")
                            .WithColor(DiscordColor.Red)
                        );

                    await args.Interaction.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, wrongUserBuilder.AsEphemeral());
                    break;
                }

                var response = new DiscordMessageBuilder()
                    .WithAllowedMentions(Mentions.All)
                    .AddEmbed(new DiscordEmbedBuilder()
                        .WithDescription("Пользователь продолжил обращение")
                        .WithColor(DiscordColor.Green)
                    );

                if (profile.Ticket.TicketSupportUserId != null)
                {
                    // Возвращаем доступ к отправлению сообщения у принявшего обращение
                    var staffMember = await args.Guild.GetMemberAsync((ulong)profile.Ticket.TicketSupportUserId);
                    await args.Channel.AddOverwriteAsync(staffMember, allow: Permissions.AccessChannels | Permissions.SendMessages | Permissions.AttachFiles | Permissions.EmbedLinks | Permissions.ReadMessageHistory);
                    response.WithContent(staffMember.Mention);
                }

                // Возвращаем доступ к отправлению сообщения у пользователя создавшего обращение
                await args.Channel.AddOverwriteAsync(ticketMember, allow: Permissions.AccessChannels | Permissions.SendMessages | Permissions.AttachFiles | Permissions.EmbedLinks | Permissions.ReadMessageHistory);

                profile.Ticket.TicketAutoCloseDateUnix = null;
                profile.Ticket.TicketStatus = TicketStatus.WaitingSolution;
                await MongoManager.UpdateAsync(profile);

                await args.Interaction.CreateResponseAsync(InteractionResponseType.UpdateMessage);
                await args.Message.DeleteAsync();
                await args.Channel.SendMessageAsync(response);
                break;
            }

            case "HardCloseTicket":
            {
                if (ClosingTickets.Contains(args.Interaction.Channel.Id))
                {
                    var closingBuilder = new DiscordInteractionResponseBuilder()
                        .AddEmbed(new DiscordEmbedBuilder()
                            .WithDescription("❌ Тикет уже закрывается!")
                            .WithColor(DiscordColor.Red)
                        );

                    await args.Interaction.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, closingBuilder.AsEphemeral());
                    break;
                }

                var closeBuilder = new DiscordInteractionResponseBuilder()
                    .AddEmbed(new DiscordEmbedBuilder()
                        .WithDescription("❌ Тикет будет принудительно закрыт!")
                        .WithColor(DiscordColor.Red)
                    );

                await args.Interaction.CreateResponseAsync(InteractionResponseType.UpdateMessage, closeBuilder.AsEphemeral());

                ClosingTickets.Add(args.Interaction.Channel.Id);

                var ticketlog = (await args.Interaction.Channel.GetMessagesAsync()).Reverse().Where(x => x.Author.IsBot == false);

                var log = string.Empty;

                int attachmentCount = 1;

                foreach (var msg in ticketlog)
                {
                    if (msg.Attachments.Count == 0)
                    {
                        log += $"[{msg.CreationTimestamp.LocalDateTime:MM/dd/yyy HH:mm}] {msg.Author.Username}: {msg.Content} \n";
                    }
                    // Логика сохранения вложенных картинок
                    else
                    {
                        log += $"[{msg.CreationTimestamp.LocalDateTime:MM/dd/yyy HH:mm}] {msg.Author.Username}: {msg.Content} \n";

                        foreach (var attachment in msg.Attachments)
                        {
                            try
                            {
                                using HttpClient httpClient = new();
                                await using var memoryStream = await httpClient.GetStreamAsync(attachment.Url);

                                var fileMessage = new DiscordMessageBuilder()
                                    .WithContent($"Вложение#{attachmentCount}")
                                    .AddFile(attachment.FileName, memoryStream);

                                // Проблема с отправкой промежуточной картитнки в чат с мусором
                                var image = await args.Interaction.Guild.GetChannel(Bot.Config.Channels.LogTicketsChannelId).SendMessageAsync(fileMessage);

                                log += $"| Вложение#{attachmentCount}: {image.JumpLink}\n";
                                attachmentCount++;
                            }
                            catch
                            {
                                log += "| Вложение: произошла ошибка при копировании вложения\n";
                            }
                        }
                    }
                }

                var profile = MongoManager.GetUserByTicketChannel(args.Channel.Id);

                if (profile?.Ticket == null)
                    throw new Exception("Ticket profile is null");

                var ticketUser = await client.GetUserAsync(profile.UserId);

                DiscordUser? staffUser = null;

                if (profile.Ticket.TicketSupportUserId != null)
                {
                    staffUser = await client.GetUserAsync((ulong)profile.Ticket.TicketSupportUserId);
                }

                using (var stream = new MemoryStream())
                {
                    var streamWriter = new StreamWriter(stream);

                    try
                    {
                        await streamWriter.WriteAsync(log);
                        await streamWriter.FlushAsync();
                        stream.Seek(0, SeekOrigin.Begin);

                        var closeTicketBuilder = new DiscordMessageBuilder()
                            .AddFile($"{args.Interaction.Channel.Id}.txt", stream)
                            .WithEmbed(new DiscordEmbedBuilder()
                                .WithTimestamp(DateTime.Now)
                                .WithTitle($"**Лог #Обращение-{profile.Ticket.TicketNumber}**")
                                .WithDescription($"**Создатель тикета:** \n {ticketUser.Username} \n {ticketUser.Mention} \n Id: {ticketUser.Id} \n" +
                                                 $"`Обращение было принудительно закрыто: {args.User.Username}`")
                                .WithFooter($"Администратор: {staffUser?.Username} • {staffUser?.Id}")
                                .WithColor(new DiscordColor("f2f3f4"))
                            );

                        await closeTicketBuilder.SendAsync(args.Interaction.Guild.GetChannel(Bot.Config.Channels.LogTicketsChannelId));
                    }
                    finally
                    {
                        await streamWriter.DisposeAsync();
                    }
                }
                
                // Удаляем запись о забранном тикете
                ClosingTickets.Remove(args.Interaction.Channel.Id);
                
                await args.Interaction.Channel.DeleteAsync();

                // Удаляем тикет
                profile.Ticket = null;
                await MongoManager.UpdateAsync(profile);
                break;
            }
        }
    }

    [AsyncListener(EventTypes.ModalSubmitted)]
    public static async Task ClientOnModalSubmitReceived(DiscordClient client, ModalSubmitEventArgs args)
    {
        switch (args.Interaction.Data.CustomId)
        {
            case "CreateTicketModal":
            {
                var member = await args.Interaction.Guild.GetMemberAsync(args.Interaction.User.Id);

                var ticketNumber = ConfigSerialize.UpdateTicketNumber();

                var ticketChannel = await args.Interaction.Guild.CreateChannelAsync($"{DiscordEmoji.FromName(client, ":green_circle:")} Обращение-{ticketNumber}", ChannelType.Text, args.Interaction.Channel.Parent, overwrites: new[]
                {
                    new DiscordOverwriteBuilder(args.Interaction.Guild.EveryoneRole)
                    {
                        Denied = Permissions.AccessChannels | Permissions.SendMessages
                    },

                    new DiscordOverwriteBuilder(member)
                    {
                        Allowed = Permissions.AccessChannels | Permissions.AttachFiles | Permissions.SendMessages | Permissions.EmbedLinks | Permissions.ReadMessageHistory
                    },

                    // Добавляем в канал роль оператора
                    new DiscordOverwriteBuilder(args.Interaction.Guild.GetRole(Bot.Config.Roles.OperatorRoleId))
                    {
                        Allowed = Permissions.AccessChannels
                    }
                });

                var ticketMessageBuilder = new DiscordMessageBuilder()
                    .WithAllowedMentions(Mentions.All)
                    .WithContent($"{args.Interaction.User.Mention} | {args.Interaction.Guild.GetRole(Bot.Config.Roles.OperatorRoleId).Mention}")
                    .AddEmbed(new DiscordEmbedBuilder()
                        .WithImageUrl(Constants.SupportImageUrl)
                        .WithColor(Constants.PubgColor)
                    )
                    .AddEmbed(new DiscordEmbedBuilder()
                        .WithTitle("**Добро пожаловать в поддержку!**")
                        .WithDescription($"```Мы готовы вам помочь``` \n\n" +
                                         $"**По вопросам рекламы и сотрудничества:** \n {args.Interaction.Guild.Owner.Mention} - {args.Interaction.Guild.Owner.Username}\n \n ```Ваш, {args.Interaction.Guild.Name}```")
                        .WithImageUrl(Constants.EmptyLineImageUrl)
                        .WithColor(Constants.PubgColor))
                    .AddEmbed(new DiscordEmbedBuilder()
                        .WithTitle("Обращение:")
                        .AddField("Тема:", $"```{args.Values.First(x => x.Key == "TicketSubject").Value} ```")
                        .AddField("Описание:", $"```{args.Values.First(x => x.Key == "TicketDescription").Value} ```")
                        .WithColor(Constants.PubgColor)
                        .WithImageUrl(Constants.EmptyLineImageUrl)
                    ).AddComponents(new DiscordComponent[]
                    {
                        new DiscordButtonComponent(ButtonStyle.Success, "ControlTicket", "Управление обращением", emoji: new DiscordComponentEmoji(DiscordEmoji.FromName(client, ":globe_with_meridians:", false)))
                    });

                await ticketChannel.SendMessageAsync(ticketMessageBuilder);

                var profile = await MongoManager.GetUserAsync(args.Interaction.User);

                // Creates default form of user profile for db
                profile.Ticket = new TicketEntry()
                {
                    TicketOpenDateUnix = DateTime.Now.ToUnixTimestamp(),
                    TicketChannelId = ticketChannel.Id,
                    TicketNumber = ticketNumber,
                    TicketSupportUserId = null, // Becasuse no one have accepted ticket
                    TicketSubject = args.Values.First(x => x.Key == "TicketSubject").Value,
                    TicketDescription = args.Values.First(x => x.Key == "TicketDescription").Value,
                    TicketStatus = TicketStatus.Opened
                };

                await MongoManager.UpdateAsync(profile);

                // Отправялем сообщение пользователю о том, что его канал был создан
                var ephemeralTicketCreate = new DiscordMessageBuilder()
                    .WithEmbed(new DiscordEmbedBuilder()
                        .WithDescription($"📩 Ваше обращение было создано. \n Нажмите на {ticketChannel.Mention}, чтобы перейти.")
                        .WithColor(DiscordColor.Red)
                    );

                await args.Interaction.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, new DiscordInteractionResponseBuilder(ephemeralTicketCreate).AsEphemeral());
                break;
            }

            case "CreateVoiceTicketModal":
            {
                var member = await args.Interaction.Guild.GetMemberAsync(args.Interaction.User.Id);

                if (member.VoiceState == null)
                {
                    var ephemeralCallAdmin = new DiscordMessageBuilder()
                        .WithEmbed(new DiscordEmbedBuilder()
                            .WithDescription("\u274c Вам необходимо находиться в голосовом канале для вызова администрации.")
                            .WithColor(DiscordColor.Red)
                        );

                    await args.Interaction.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, new DiscordInteractionResponseBuilder(ephemeralCallAdmin).AsEphemeral());
                    break;
                }

                var callAdminEmbedBuilder = new DiscordEmbedBuilder()
                    .WithTitle($"{args.Interaction.User.Username} • Вызывает администрацию")
                    .WithDescription($"Пользователь {args.Interaction.User.Mention} вызывает в {member.VoiceState?.Channel.Mention}")
                    .WithImageUrl(Constants.EmptyLineImageUrl)
                    .WithColor(new DiscordColor("43b581"));

                if (args.Values.First(x => x.Key == "TicketSubject").Value != "")
                {
                    callAdminEmbedBuilder.AddField("Тема:", $"```{args.Values.First(x => x.Key == "TicketSubject").Value}```");
                }

                if (args.Values.First(x => x.Key == "TicketDescription").Value != "")
                {
                    callAdminEmbedBuilder.AddField("Описание:", $"```{args.Values.First(x => x.Key == "TicketDescription").Value}```");
                }

                var callAdminMessageOpenBuiler = new DiscordMessageBuilder()
                    .WithAllowedMentions(Mentions.All)
                    .WithContent(args.Interaction.Guild.GetRole(Bot.Config.Roles.OperatorRoleId).Mention)
                    .WithEmbed(callAdminEmbedBuilder);

                callAdminMessageOpenBuiler.AddComponents(new DiscordComponent[]
                {
                    new DiscordButtonComponent(ButtonStyle.Success, "СloseVoiceTicket", "Завершить")
                });

                var callAdminMessage = await callAdminMessageOpenBuiler.SendAsync(args.Interaction.Guild.GetChannel(Bot.Config.Channels.VoiceTicketsChannelId));

                OpenUserCalls.Add(callAdminMessage, member.VoiceState!.Channel);

                // Добавляем новые данные в списки кд для пользователя и канала
                OpenCallUserCooldown.Add(args.Interaction.User, DateTime.Now.AddMinutes(1));
                OpenCallChannelCooldown.Add(member.VoiceState.Channel.Id, DateTime.Now.AddMinutes(1));

                var ephemeralOpenCallAdmin = new DiscordMessageBuilder()
                    .WithEmbed(new DiscordEmbedBuilder()
                        .WithDescription("\ud83e\uddb8 Ожидайте, администратор скоро подключится к вам по вашему обращению.")
                        .WithColor(DiscordColor.Red)
                    );

                await args.Interaction.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, new DiscordInteractionResponseBuilder(ephemeralOpenCallAdmin).AsEphemeral());
                break;
            }

            case "SuccesCloseModal":
            {
                if (ClosingTickets.Contains(args.Interaction.Channel.Id))
                {
                    var closingBuilder = new DiscordInteractionResponseBuilder()
                        .AddEmbed(new DiscordEmbedBuilder()
                            .WithDescription("❌ Тикет уже закрывается!")
                            .WithColor(DiscordColor.Red)
                        );

                    await args.Interaction.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, closingBuilder.AsEphemeral());
                    break;
                }
                
                ClosingTickets.Add(args.Interaction.Channel.Id);

                await args.Interaction.CreateResponseAsync(InteractionResponseType.UpdateMessage);

                var profile = MongoManager.GetUserByTicketChannel(args.Interaction.Channel.Id);

                if (profile?.Ticket == null)
                    throw new Exception("Ticket profile is null");

                var ticketlog = (await args.Interaction.Channel.GetMessagesAsync(500)).Reverse().Where(x => x.Author.IsBot == false);

                var log = string.Empty;

                int attachmentCount = 1;

                foreach (var msg in ticketlog)
                {
                    if (msg.Attachments.Count == 0)
                    {
                        log += $"[{msg.CreationTimestamp.LocalDateTime:MM/dd/yyy HH:mm}] {msg.Author.Username}: {msg.Content} \n";
                    }
                    // Логика сохранения вложенных картинок
                    else
                    {
                        log += $"[{msg.CreationTimestamp.LocalDateTime:MM/dd/yyy HH:mm}] {msg.Author.Username}: {msg.Content} \n";

                        foreach (var attachment in msg.Attachments)
                        {
                            try
                            {
                                using HttpClient httpClient = new();
                                await using var memoryStream = await httpClient.GetStreamAsync(attachment.Url);

                                var fileMessage = new DiscordMessageBuilder()
                                    .WithContent($"Вложение#{attachmentCount}")
                                    .AddFile(attachment.FileName, memoryStream);
                                
                                var image = await args.Interaction.Guild.GetChannel(Bot.Config.Channels.LogTicketsChannelId).SendMessageAsync(fileMessage);

                                log += $"| Вложение#{attachmentCount}: {image.JumpLink}\n";
                                attachmentCount++;
                            }
                            catch
                            {
                                log += "| Вложение: произошла ошибка при копировании вложения\n";
                            }
                        }
                    }
                }

                var ticketUser = await client.GetUserAsync(profile.UserId);
                var staffUser = await client.GetUserAsync((ulong)profile.Ticket.TicketSupportUserId!);

                using (var stream = new MemoryStream())
                {
                    var streamWriter = new StreamWriter(stream);

                    try
                    {
                        await streamWriter.WriteAsync(log);
                        await streamWriter.FlushAsync();
                        stream.Seek(0, SeekOrigin.Begin);
                        
                        var closeTicketEmbedBuilder = new DiscordEmbedBuilder()
                            .WithTimestamp(DateTime.Now)
                            .WithTitle($"**Лог #Обращение-{profile.Ticket.TicketNumber}**")
                            .WithDescription(
                                $"**Создатель тикета:** \n {ticketUser.Username} \n {ticketUser.Mention} \n Id: {ticketUser.Id} \n")
                            .WithFooter($"Администратор: {staffUser.Username} • {staffUser.Id}", $"{staffUser.AvatarUrl}")
                            .WithColor(new DiscordColor("f2f3f4"));

                        var closeTicketBuilder = new DiscordMessageBuilder()
                            .AddFile($"{args.Interaction.Channel.Id}.txt", stream)
                            .AddEmbed(closeTicketEmbedBuilder);

                        if (args.Values.First(x => x.Key == "Evaluation").Value != "")
                        {
                            var evaluationBuilder = new DiscordEmbedBuilder()
                                .WithTitle("Отзыв о обращении")
                                .AddField("Оценка:", $"```{args.Values.First(x => x.Key == "Evaluation").Value}```")
                                .AddField("Отзыв:", $"```{args.Values.First(x => x.Key == "Review").Value} ```")
                                .WithColor(new DiscordColor("f2f3f4"));

                            closeTicketBuilder.AddEmbed(evaluationBuilder);
                        }
                        
                        closeTicketEmbedBuilder.AddField("Тема:", $"```{profile.Ticket.TicketSubject}```");
                        closeTicketEmbedBuilder.AddField("Описание:", $"```{profile.Ticket.TicketDescription}```");

                        await closeTicketBuilder.SendAsync(args.Interaction.Guild.GetChannel(Bot.Config.Channels.LogTicketsChannelId));
                    }
                    catch (Exception e)
                    {
                        await ErrorMessageSender.SendError("Произошла ошибка при попытке закрыть тикет", e);
                        break;
                    }
                    finally
                    {
                        await streamWriter.DisposeAsync();
                    }
                }

                profile.Ticket = null;
                await MongoManager.UpdateAsync(profile);

                await args.Interaction.Channel.SendMessageAsync(new DiscordMessageBuilder().AddEmbed(new DiscordEmbedBuilder()
                    .WithDescription("✅ Обращение было успешно обработано и скоро будет закрыто.")
                    .WithColor(DiscordColor.Green)));

                await Task.Delay(5000);

                ClosingTickets.Remove(args.Interaction.Channel.Id);
                await args.Interaction.Channel.DeleteAsync();
                break;
            }
        }
    }
}