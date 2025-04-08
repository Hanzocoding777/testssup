using Newtonsoft.Json;
namespace Support.Entities.Config;

public class Config
{
    [JsonProperty("discord_api_token")]
    internal string DiscordApiToken { get; private set; }

    [JsonProperty("guild_id")]
    internal ulong GuildId { get; private set; }

    [JsonProperty("database")]
    internal Database Database { get; private set; }

    [JsonProperty("logger")]
    internal Logger Logger { get; private set; }

    [JsonProperty("channels")]
    internal Channels Channels { get; private set; }

    [JsonProperty("roles")]
    internal Roles Roles { get; private set; }

    [JsonProperty("ticket_number")]
    internal int TicketNumber { get; set; }
}