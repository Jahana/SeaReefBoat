// Port of world/character_cache.{h,cpp}: charId -> the map process (IPP) the character is on.

using System.Collections.Generic;

namespace XI.World
{
    public sealed class CharacterCache
    {
        private readonly Dictionary<uint, Ipp> _charIdToIpp = new();

        public void UpdateCharacter(uint charId, Ipp ipp) => _charIdToIpp[charId] = ipp;

        public void RemoveCharacter(uint charId) => _charIdToIpp.Remove(charId);

        public Ipp? GetCharacterIpp(uint charId) => _charIdToIpp.TryGetValue(charId, out var ipp) ? ipp : null;
    }
}
