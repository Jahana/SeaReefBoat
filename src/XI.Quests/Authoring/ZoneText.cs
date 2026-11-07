// XI.Quests -- a zone's text by name (zones[zone].text[Name]): the id differs by client version, so the bridge
// resolves it at run time through the zone's IDs table (quest rework B3a)
namespace XI.Quests.Authoring
{
    public readonly struct ZoneText
    {
        public ZoneText(ushort zone, string name)
        {
            Zone = zone;
            Name = name ?? throw new System.ArgumentNullException(nameof(name));
        }

        public ushort Zone { get; }
        public string Name { get; }

        public override string ToString() => $"{Zone}:{Name}";
    }
}
