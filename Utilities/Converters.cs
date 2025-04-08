using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System.Globalization;

namespace Support.Utilities;

public class Converters
{
    public class JsonConverter
    {
        public static readonly JsonSerializerSettings Settings = new()
        {
            MetadataPropertyHandling = MetadataPropertyHandling.Ignore,
            DateParseHandling = DateParseHandling.None,
            Converters =
            {
                new IsoDateTimeConverter { DateTimeStyles = DateTimeStyles.AssumeUniversal }
            },
        };
    }
}