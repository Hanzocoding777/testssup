﻿using Newtonsoft.Json;

namespace Support.Entities.Config;

public struct Database
{
    [JsonProperty("connection_string")] public string ConnectionString { get; private set; }

    [JsonProperty("database_name")] public string DatabaseName { get; private set; }
}