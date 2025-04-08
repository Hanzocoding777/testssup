using Newtonsoft.Json;

namespace Support.Entities.Config;

public struct Roles
{
    [JsonProperty("head_role_id")]
    public ulong HeadRoleId { get; private set; }

    [JsonProperty("grand_moderator_role_id")]
    public ulong GrandModeratorRoleId { get; private set; }

    [JsonProperty("moderator_role_id")]
    public ulong ModeratorRoleId { get; private set; }

    [JsonProperty("staff_role_id")]
    public ulong StaffRoleId { get; private set; }

    [JsonProperty("operator_role_id")]
    public ulong OperatorRoleId { get; private set; }
}