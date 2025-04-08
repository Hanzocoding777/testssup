using Newtonsoft.Json;

namespace Support.Entities.Config;

public struct Channels
{
    [JsonProperty("error_channel_id")]
    public ulong ErrorChannelId { get; private set; }

    [JsonProperty("logtickets_channel_id")]
    public ulong LogTicketsChannelId { get; private set; }

    [JsonProperty("voicetickets_channel_id")]
    public ulong VoiceTicketsChannelId { get; private set; }
}