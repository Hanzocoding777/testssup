using System.Timers;
using Database.Services;
using Support.Utilities;
using Support.Utilities.TicketMethods;
using Timer = System.Timers.Timer;

namespace Support.Listeners;

public class Timers
{
    public static async Task RegisterTimers()
    {
        var bannerGenerateTimer = new Timer(TimeSpan.FromMinutes(1).TotalMilliseconds);
        bannerGenerateTimer.Elapsed += ResetExpiredTicketBlocks;
        bannerGenerateTimer.AutoReset = true;
        bannerGenerateTimer.Enabled = true;
        
        var resetExpiredTicketBlocksTimer = new Timer(TimeSpan.FromMinutes(15).TotalMilliseconds);
        resetExpiredTicketBlocksTimer.Elapsed += ResetExpiredTicketBlocks;
        resetExpiredTicketBlocksTimer.AutoReset = true;
        resetExpiredTicketBlocksTimer.Enabled = true;

        var autoCloseResolvedTicketsTimer = new Timer(TimeSpan.FromMinutes(15).TotalMilliseconds);
        autoCloseResolvedTicketsTimer.Elapsed += AutoCloseResolvedTickets;
        autoCloseResolvedTicketsTimer.AutoReset = true;
        autoCloseResolvedTicketsTimer.Enabled = true;

        await Task.CompletedTask;
    }

    private static async void ResetExpiredTicketBlocks(object? source, ElapsedEventArgs args)
    {
        var blockedProfiles = MongoManager.GetBlockedProfiles();
        
        foreach (var blockedProfile in blockedProfiles)
        {
            if (DateTime.Now > blockedProfile.TicketBlockDateUnix?.ToDateTime())
            {
                blockedProfile.TicketBlockDateUnix = null;
                await MongoManager.UpdateAsync(blockedProfile);
            }
        }
    }

    private static async void AutoCloseResolvedTickets(object? source, ElapsedEventArgs args)
    { 
        var resolvedProfiles = MongoManager.GetResolvedTickets();

        foreach (var profile in resolvedProfiles)
        {
            if (DateTime.Now > profile.Ticket?.TicketAutoCloseDateUnix?.ToDateTime())
            {
                await CloseTicket.CloseTicketAsync(profile, "Тикет был закрыт автоматически");
            }
        }
    }
}