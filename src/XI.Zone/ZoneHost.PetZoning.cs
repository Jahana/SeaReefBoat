using System;
using XI.Persistence;

namespace XI.Zone
{
    // CCharEntity::petZoningInfo: a char's pet carried through a zone change. setPetZoningInfo when the char asks to
    // zone (and in SendToZone, before the pet is taken out of the old zone), resetPetZoningInfo on a job change; the
    // pet columns of char_stats (SaveCharStats / LoadChar) and the jugpet- char vars carry it to the next zone, whose
    // 0x00C (game ok) spawns it again (petutils::SpawnPet with spawningFromZone: no special spawn animation, then
    // CPetEntity::loadPetZoningInfo's HP, MP, TP and jug time). Alexander, Odin and Atomos never come along; a jug pet
    // only with map.KEEP_JUGPET_THROUGH_ZONING.
    public sealed class PetZoningInfo
    {
        public int PetId;
        public PetType PetType = PetType.Avatar;
        public byte PetLevel;
        public int PetHp, PetMp, PetTp;
        public bool RespawnPet;
        public long JugSpawnMs;    // jugSpawnTime on the zone clock (0 when unset)
        public long JugDurationMs; // jugDuration
    }

    public sealed partial class ZoneHost
    {
        private const string JugPetSpawnTimeVar = "jugpet-spawn-time", JugPetDurationVar = "jugpet-duration-seconds";

        // CCharEntity::setPetZoningInfo
        private void SetPetZoningInfo(CharData c)
        {
            if (c.PPet is not PetEntity pet) return;
            var z = c.PetZoning;
            z.PetId = pet.PetId;
            bool keep = true;
            if (pet.PetType == PetType.JugPet)
            {
                if (!ChatCfg.KeepJugpetThroughZoning) keep = false;
                else
                {
                    z.JugSpawnMs = pet.JugSpawnMs;
                    z.JugDurationMs = pet.JugDurationMs;
                }
            }
            // Alexander, Odin and Atomos cannot persist through zoning.
            if (keep && pet.PetType is PetType.JugPet or PetType.Avatar && (pet.PetId == PetId.Alexander || pet.PetId == PetId.Odin || pet.PetId == PetId.Atomos))
                keep = false;
            if (keep && pet.PetType is PetType.JugPet or PetType.Avatar or PetType.Automaton or PetType.Wyvern)
            {
                z.PetLevel = pet.SpawnLevel;
                z.PetHp = pet.Hp;
                z.PetTp = pet.Tp;
                z.PetMp = pet.Mp;
                z.PetType = pet.PetType;
            }
            z.RespawnPet = true;
        }

        // CCharEntity::resetPetZoningInfo (the pet id stays)
        private static void ResetPetZoningInfo(CharData c)
        {
            var z = c.PetZoning;
            z.PetLevel = 0;
            z.PetHp = z.PetTp = z.PetMp = 0;
            z.RespawnPet = false;
            z.PetType = PetType.Avatar;
            z.JugSpawnMs = 0;
            z.JugDurationMs = 0;
        }

        // CCharEntity::shouldPetPersistThroughZoning: the pet out (or the one being carried) is a wyvern, an avatar, an
        // automaton, or a jug pet under map.KEEP_JUGPET_THROUGH_ZONING
        private bool ShouldPetPersistThroughZoning(CharData c)
        {
            var pet = c.PPet as PetEntity;
            if (pet == null && !c.PetZoning.RespawnPet) return false;
            var petType = pet?.PetType ?? c.PetZoning.PetType;
            return petType is PetType.Wyvern or PetType.Avatar or PetType.Automaton ||
                   petType == PetType.JugPet && ChatCfg.KeepJugpetThroughZoning;
        }

        // the "Save pet if any" every zone request does
        private void SavePetForZoning(CharData c)
        {
            if (ShouldPetPersistThroughZoning(c)) SetPetZoningInfo(c);
        }

        // charutils::LoadChar's jug state: the jugpet- char vars (a UTC spawn time and a duration in seconds), cleared
        // once read
        private void LoadPetZoningCharVars(CharData c)
        {
            if (!c.PetZoning.RespawnPet) return;
            long now = NowMs();
            uint jugTimestamp = (uint)CharVar(c, JugPetSpawnTimeVar);
            c.PetZoning.JugSpawnMs = now - (UnixNow() - (long)jugTimestamp) * 1000L;
            c.PetZoning.JugDurationMs = CharVar(c, JugPetDurationVar) * 1000L;
            ClearCharVarsWithPrefix(c, "jugpet-");
        }

        // CCharEntity::clearCharVarsWithPrefix
        private void ClearCharVarsWithPrefix(CharData c, string prefix)
        {
            if (prefix.Length < 5)
            {
                log_($"Prefix too short to clear with: '{prefix}'");
                return;
            }
            foreach (var name in new System.Collections.Generic.List<string>(c.CharVars.Keys))
                if (name.StartsWith(prefix, StringComparison.Ordinal)) c.CharVars[name] = (0, 0);
            Exec("DELETE FROM char_vars WHERE charid = ? AND varname LIKE ?", BoundValue.Of(c.Id), BoundValue.Of(prefix + "%"));
        }

        // SaveCharStats' jug part: the spawn time and duration as char vars under map.KEEP_JUGPET_THROUGH_ZONING (else
        // there is nothing to write or clear)
        private void SaveJugPetCharVars(CharData c)
        {
            if (!ChatCfg.KeepJugpetThroughZoning) return;
            if (c.PetZoning.JugSpawnMs > 0)
            {
                long jugTimestamp = UnixNow() - (NowMs() - c.PetZoning.JugSpawnMs) / 1000;
                SetCharVar(c, JugPetSpawnTimeVar, (int)jugTimestamp, 0);
                SetCharVar(c, JugPetDurationVar, (int)(c.PetZoning.JugDurationMs / 1000), 0);
            }
            else if (CharVar(c, JugPetSpawnTimeVar) != 0)
            {
                SetCharVar(c, JugPetSpawnTimeVar, 0, 0);
                SetCharVar(c, JugPetDurationVar, 0, 0);
            }
        }

        // GP_CLI_COMMAND_GAMEOK's "respawn any pets from last zone": where pets are allowed and not in a mog house
        private void RespawnZoningPet(CharData c)
        {
            if (!AreaOf(c).CanUseMisc("pet") || c.MoghouseId != 0) return;
            if (ShouldPetPersistThroughZoning(c)) SpawnPet(c, c.PetZoning.PetId, true);
            ResetPetZoningInfo(c);
        }

        // CPetEntity::loadPetZoningInfo: the HP, MP and TP it left with, and a jug pet's time
        private void LoadPetZoningInfo(PetEntity pet)
        {
            if (pet.PMaster is not CharData master) return;
            pet.Tp = (ushort)master.PetZoning.PetTp;
            pet.Hp = master.PetZoning.PetHp;
            pet.Mp = master.PetZoning.PetMp;
            if (pet.PetType == PetType.JugPet)
            {
                pet.JugDurationMs = master.PetZoning.JugDurationMs;
                pet.JugSpawnMs = master.PetZoning.JugSpawnMs;
            }
        }
    }
}
