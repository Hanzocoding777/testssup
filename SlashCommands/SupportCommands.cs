using Database;
using Database.Services;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.SlashCommands;
using Support.Utilities;

namespace Support.SlashCommands;

[GuildOnly]
public class SupportCommands : ApplicationCommandModule
{
    [SlashCommand("сообщение-поддержка", "Отправить сообщение поддержки")]
    public async Task SupportMessage(InteractionContext ctx)
    {
        var builder = new DiscordMessageBuilder()
            .AddEmbed(new DiscordEmbedBuilder()
                .WithImageUrl(Constants.SupportImageUrl)
                .WithColor(Constants.PubgColor))
            .AddEmbed(new DiscordEmbedBuilder()
                .WithDescription("```             🔥 PUBG RU | Поддержка```\n" +
                                 "> **Есть проблемы или жалобы на других игроков? Мы готовы помочь! ** \n \n" +
                                 "```Нажмите на кнопку, чтобы связаться с нашей поддержкой```")
                .WithImageUrl(Constants.EmptyLineImageUrl)
                .WithColor(Constants.PubgColor)
            ).AddComponents(new DiscordComponent[]
            {
                new DiscordButtonComponent(ButtonStyle.Danger, "CreateTicket1", " ᠌᠌ ᠌ ᠌ ᠌ ᠌ ᠌ ᠌ ᠌ ᠌ ᠌᠌ ᠌ ᠌ ᠌Поддержка ᠌ ᠌ ᠌ ᠌ ᠌ ᠌ ᠌ ᠌ ᠌ ᠌ ᠌ ᠌᠌ ᠌ ᠌",
                    emoji: new DiscordComponentEmoji(DiscordEmoji.FromName(ctx.Client, ":incoming_envelope:"))),
                new DiscordButtonComponent(ButtonStyle.Success, "CreateVoiceTicket", " ᠌ ᠌ ᠌ ᠌ ᠌ ᠌Голосовая поддержка ᠌ ᠌᠌ ᠌ ᠌",
                    emoji: new DiscordComponentEmoji(DiscordEmoji.FromName(ctx.Client, ":loud_sound:"))),
            });

        await ctx.Channel.SendMessageAsync(builder);

        await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
            new DiscordInteractionResponseBuilder().WithContent("Сообщение поодержки было успешно создано").AsEphemeral());
    }

    [SlashCommand("Заблокировать", "Заблокировать пользователя в системе поддержки")]
    public async Task SupportBlock(InteractionContext ctx, [Option("Пользователь", "Заблокировать пользователя в системе поддержки")] DiscordUser user,
        [Option("Длительность", "Длительность блокировки пользователя")] BlockDuration blockDuration = BlockDuration.OneHour, 
        [Option("Уведомить", "Уведомить пользователя о блокировке в личных сообщениях")] NotificateUser notificateUser = NotificateUser.Yes,
        [Option("Причина", "Причина блокировки пользователя (пользователь не увидит причину блокировки)")] string? reason = "Не указана")
    {
        var member = await ctx.Guild.GetMemberAsync(user.Id);

        var discordEmbedBuilder = new DiscordEmbedBuilder();
        discordEmbedBuilder.WithColor(DiscordColor.Red);

        if (member.Roles.Any(x => x.Id == Bot.Config.Roles.StaffRoleId))
        {
            discordEmbedBuilder.WithDescription("❌ Вы не можете заблокировать этого пользователя в системе поддержки.");

            await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, new DiscordInteractionResponseBuilder().AddEmbed(discordEmbedBuilder));
            return;
        }


        var profile = await MongoManager.GetUserAsync(user);

        if (profile.TicketBlockDateUnix != null)
        {
            discordEmbedBuilder.WithDescription($"Пользователь уже был заблокирован с системе поддержки.\n" +
                                                 $"Его бан истекает {Formatter.Timestamp((DateTime)profile.TicketBlockDateUnix?.ToDateTime()!)}");

            await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, new DiscordInteractionResponseBuilder().AddEmbed(discordEmbedBuilder));
            return;
        }

        profile.Ticket ??= new TicketEntry();

        // set block duration to database
        switch (blockDuration)
        {
            case BlockDuration.OneMinute:
                profile.TicketBlockDateUnix = DateTime.Now.AddMinutes(1).ToUnixTimestamp();
                break;

            case BlockDuration.FiveMinutes:
                profile.TicketBlockDateUnix = DateTime.Now.AddMinutes(5).ToUnixTimestamp();
                break;

            case BlockDuration.TenMinutes:
                profile.TicketBlockDateUnix = DateTime.Now.AddMinutes(10).ToUnixTimestamp();
                break;

            case BlockDuration.OneHour:
                profile.TicketBlockDateUnix = DateTime.Now.AddHours(1).ToUnixTimestamp();
                break;

            case BlockDuration.OneDay:
                profile.TicketBlockDateUnix = DateTime.Now.AddDays(1).ToUnixTimestamp();
                break;

            case BlockDuration.OneWeek:
                profile.TicketBlockDateUnix = DateTime.Now.AddDays(7).ToUnixTimestamp();
                break;

            // max block in DateTime format - ?
            case BlockDuration.ForeverBlock:
                profile.TicketBlockDateUnix = DateTime.Now.AddDays(9999).ToUnixTimestamp();
                break;
        }

        await MongoManager.UpdateAsync(profile);

        bool succes = true;

        if (notificateUser == NotificateUser.Yes)
        {
            var notificationEmbedBuilder = new DiscordEmbedBuilder()
                .WithTitle("Блокировка в системе поддержки")
                .WithDescription($"Здравствуйте, вы были заблокированы в системе поддержки на сервере **{ctx.Guild.Name}**.\n\n" +
                                 $"Блокировка будет снята: {Formatter.Timestamp((DateTime)profile.TicketBlockDateUnix?.ToDateTime()!)}")
                .WithThumbnail(member.AvatarUrl)
                .WithColor(DiscordColor.Red);

            try
            {
                await member.SendMessageAsync(new DiscordMessageBuilder().AddEmbed(notificationEmbedBuilder));
            }
            catch
            {
                succes = false;
                notificationEmbedBuilder.WithFooter("Сообщение о блокировке не было доставлено");
            }
        }

        var responseEmbedBuilder = new DiscordEmbedBuilder()
            .WithAuthor($"{member.Username}", null, member.AvatarUrl)
            .WithTitle("Блокировка в системе поддержки")
            .WithDescription($"Пользователь {member.Mention} получил блокировку в системе вызова администратора.\n\n" +
                             $"Блокировка будет снята: {Formatter.Timestamp((DateTime)profile.TicketBlockDateUnix?.ToDateTime()!)}\n" +
                             $"Причина блокировки: `{reason}`")
            .WithFooter($"Администратор: {ctx.User.Username} • {ctx.User.Id}", $"{ctx.User.AvatarUrl}")
            .WithColor(DiscordColor.Yellow);

        if (succes == false)
        {
            responseEmbedBuilder.WithDescription(
                $"Пользователь {member.Mention} получил блокировку в системе вызова администратора.\n\n" +
                $"Блокировка будет снята: {Formatter.Timestamp((DateTime)profile.TicketBlockDateUnix?.ToDateTime()!)}\n\n" +
                $"Причина блокировки: `{reason}`\n\n" +
                $"⚠ Сообщение о блокировке не было доставлено\"");
        }

        await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, new DiscordInteractionResponseBuilder().AddEmbed(responseEmbedBuilder));
    }

    [SlashCommand("Разблокировать", "Разблокировать пользователя в системе поддержки")]
    public async Task SupportUnBlock(InteractionContext ctx,
        [Option("Пользователь", "Разблокировать пользователя в системе поддержки")] DiscordUser user)
    {
        var profile = await MongoManager.GetUserAsync(user);
        
        var discordEmbedBuilder = new DiscordEmbedBuilder();

        if (profile.TicketBlockDateUnix == null)
        {
            discordEmbedBuilder.WithDescription("Пользователь не заблокирован с системе поддержки.");
            discordEmbedBuilder.WithColor(DiscordColor.Red);

            await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, new DiscordInteractionResponseBuilder().AddEmbed(discordEmbedBuilder));
            return;
        }

        await MongoManager.UpdateAsync(profile);

        var response = new DiscordInteractionResponseBuilder()
            .AddEmbed(new DiscordEmbedBuilder()
                .WithAuthor($"{user.Username}", null, user.AvatarUrl)
                .WithTitle("Блокировка в системе поддержки")
                .WithDescription(
                    $"Пользователь {user.Mention} был успешно разблокирован в системе поддержки.")
                .WithFooter($"Администратор: {ctx.User.Username} • {ctx.User.Id}",
                    $"{ctx.User.AvatarUrl}")
                .WithColor(DiscordColor.Green));

        await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, response);
    }

    public enum BlockDuration
    {
        [ChoiceName("60 Секунд")]
        OneMinute,
        [ChoiceName("5 Минут")]
        FiveMinutes,
        [ChoiceName("10 Минут")]
        TenMinutes,
        [ChoiceName("1 Час")]
        OneHour,
        [ChoiceName("1 День")]
        OneDay,
        [ChoiceName("1 Неделя")]
        OneWeek,
        [ChoiceName("Навсегда (27 лет)")]
        ForeverBlock,
    }

    public enum NotificateUser
    {
        [ChoiceName("Да")]
        Yes,
        [ChoiceName("Нет")]
        No,
    }
}