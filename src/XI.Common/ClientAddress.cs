// network.ZONE_IP_FROM_CLIENT (C# only): the map address a client is told, in the lobby 0x0B and the zone-change
// 0x00B. A zone served on this machine (zone_settings.zoneip in 127.0.0.0/8) is sent as the address that client used
// to reach the login server (its 0xA1, the C++ "search-server ip"); a routable zoneip and the setting off send
// zoneip as is, as the C++ does. IPs are native little-endian uints (IpText.Str2Ip): the first octet is the low byte.

namespace XI.Common
{
    public static class ClientAddress
    {
        public static bool IsLoopback(uint ip) => (ip & 0xFF) == 127;

        public static uint ClientFacingIp(uint zoneIp, uint clientServerIp, bool enabled) =>
            enabled && clientServerIp != 0 && IsLoopback(zoneIp) ? clientServerIp : zoneIp;
    }
}
