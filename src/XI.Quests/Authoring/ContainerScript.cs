// XI.Quests -- quest rework C2 (docs/superpowers/specs/2026-11-05-quest-rework-c2-mission-platform-design.md,
// section 3.1): the kind-neutral base of the C# containers. A quest, a mission and a battlefield are each a container
// of the Lua interaction framework; this is what the three share. Nothing here touches the engine.
namespace XI.Quests.Authoring
{
    public enum ContainerKind : byte { Quest, Mission, Battlefield }

    // What every C# container shares: its kind, its key and the Lua file it replaces.
    public abstract class ContainerScript
    {
        public abstract ContainerKind Kind { get; }
        // the require name of the Lua file this container replaces: its slot in the containers list
        public virtual string LegacyPath => null;
        // the container's stable key (quest, mission or battlefield key)
        public abstract string Key { get; }
    }

    // Where the containers' Lua files lie under scripts/, the rules every reader shares.
    public static class ContainerFolders
    {
        // a zone's folder under scripts/battlefields: its zone_settings name with '-' as '_' (the folder of
        // Riverne-Site_A01 is Riverne_Site_A01)
        public static string Battlefield(string zoneName) => zoneName.Replace('-', '_');

        // the zone's battlefield folder under battlefieldsDir: <battlefieldsDir>/<zone name> when that folder exists,
        // else <battlefieldsDir>/<Battlefield(zone name)> (whether or not it exists)
        public static string BattlefieldDir(string battlefieldsDir, string zoneName)
        {
            string dir = System.IO.Path.Combine(battlefieldsDir, zoneName);
            return System.IO.Directory.Exists(dir)
                ? dir : System.IO.Path.Combine(battlefieldsDir, Battlefield(zoneName));
        }
    }
}
