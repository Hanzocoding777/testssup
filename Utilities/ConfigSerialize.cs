using Newtonsoft.Json;
using System.Text;
using Support.Entities.Config;

namespace Support.Utilities;

public static class ConfigSerialize
{
    public static int UpdateTicketNumber()
    {
        var input = File.ReadAllText("config.json", new UTF8Encoding(false));
        var config = JsonConvert.DeserializeObject<Config>(input);
        config.TicketNumber += 1;

        // Saving config with same values but updated fields
        var newjson = JsonConvert.SerializeObject(config, Formatting.Indented);
        File.WriteAllText("config.json", newjson, new UTF8Encoding(false));
        return config.TicketNumber;
    }
}