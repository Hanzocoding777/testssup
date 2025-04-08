using Database;
using Database.Services;
using DSharpPlus.Entities;
using Support.Entities;
using static Support.Listeners.ComponentListener;

namespace Support.Utilities.TicketMethods;

public class CloseTicket
{
    public static async Task CloseTicketAsync(UserProfile profile, string closeReason = "Не указана")
    {
        if (profile.Ticket == null)
            throw new Exception("Ticket profile is null");
        
        ClosingTickets.Add(profile.Ticket.TicketChannelId);
        
        var guild = await Bot.Client.GetGuildAsync(Bot.Config.GuildId);
        var ticketChannel = guild.GetChannel(profile.Ticket.TicketChannelId);
        var ticketLogChannel = guild.GetChannel(Bot.Config.Channels.LogTicketsChannelId);
        var ticketlog = (await ticketChannel.GetMessagesAsync(500)).Reverse().Where(x => x.Author.IsBot == false);

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

                        // Отправляем изображение в чат с общими логами тикетов
                        var image = await ticketLogChannel.SendMessageAsync(fileMessage);

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

        var ticketUser = await Bot.Client.GetUserAsync(profile.UserId);
        var staffUser = await Bot.Client.GetUserAsync((ulong)profile.Ticket.TicketSupportUserId);

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
                        $"**Создатель тикета:** \n {ticketUser.Username} \n {ticketUser.Mention} \n Id: {ticketUser.Id} \n Причина: `{closeReason}`")
                    .WithFooter($"Администратор: {staffUser.Username} • {staffUser.Id}", $"{staffUser.AvatarUrl}")
                    .WithColor(new DiscordColor("f2f3f4"));

                var closeTicketBuilder = new DiscordMessageBuilder()
                    .AddFile($"{profile.Ticket.TicketChannelId}.txt", stream)
                    .AddEmbed(closeTicketEmbedBuilder);

                if (profile.Ticket.TicketSubject != "")
                {
                    closeTicketEmbedBuilder.AddField("Тема:", $"```{profile.Ticket.TicketSubject}```");
                }

                if (profile.Ticket.TicketDescription != "")
                {
                    closeTicketEmbedBuilder.AddField("Описание:", $"```{profile.Ticket.TicketDescription}```");
                }

                await closeTicketBuilder.SendAsync(ticketLogChannel);
            }
            catch (Exception e)
            {
                await ErrorMessageSender.SendError("Произошла ошибка при попытке закрыть тикет", e);
                return;
            }
            finally
            {
                await streamWriter.DisposeAsync();
            }
        }
        
        ClosingTickets.Remove(profile.Ticket.TicketChannelId);
        
        profile.Ticket = null;
        await MongoManager.UpdateAsync(profile);
        
        await ticketChannel.DeleteAsync();
    }
}