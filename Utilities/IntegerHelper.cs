namespace Support.Utilities;

public static class IntegerHelper
{
    public static ulong ToUnixTimestamp(this DateTime datetime)
    {
        return (ulong)datetime.Subtract(new DateTime(1970, 1, 1)).TotalSeconds;
    }
    
    public static DateTime ToDateTime(this ulong unixTimestamp)
    {
        DateTime unixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Local);
        return unixEpoch.AddSeconds(unixTimestamp);
    }
}