using XI.Map;

namespace XI.Zone
{
    // PET_TYPE
    public enum PetType : byte
    {
        Avatar = 0, Wyvern = 1, JugPet = 2, CharmedMob = 3, Automaton = 4, AdventuringFellow = 5, Chocobo = 6, Luopan = 7,
    }

    // PETID: the pet_list ids the C++ names
    public static class PetId
    {
        public const int FireSpirit = 0, WaterSpirit = 5, LightSpirit = 6, DarkSpirit = 7, Carbuncle = 8, Alexander = 17, Odin = 18,
            Atomos = 19, CaitSith = 20, SheepFamiliar = 21, TurbidToloi = 68, Wyvern = 48, HarlequinFrame = 69, StormwakerFrame = 72,
            Chocobo = 74, Luopan = 75, Siren = 76, SweetCaroline = 77, EnergizedSefina = 106, MaxPetId = 128;

        public static bool IsAvatar(int id) => id >= Carbuncle && id <= CaitSith || id == Siren;
        public static bool IsJugPet(int id) => id >= SheepFamiliar && id <= TurbidToloi || id >= SweetCaroline && id <= EnergizedSefina;
        public static bool IsImmobile(int id) => id == Luopan || id == Alexander || id == Odin || id == Atomos;
    }

    // CPetEntity: a char's avatar, wyvern or jug pet, a CMobEntity whose stats petutils::LoadPet fills from its
    // pet_list row and its master, in the zone's pet list under a dynamic targid (an automaton is an AutomatonEntity).
    // Its skills are in ZoneHost.PetSkills.cs. Not ported: chocobos (unreached: pet 74 has no pet_list row, so LoadPet
    // stops at the lookup as in C++; past it LoadPet refuses one), the pet's own job abilities
    // (CPetEntity::OnAbility: only a script's useJobAbility on a pet reaches it, and no shipped script does; such a pet
    // runs a mob's CBattleEntity::OnAbility, ZoneHost.TrustSkills.cs).
    public class PetEntity : MobEntity
    {
        public readonly PetType PetType;
        public readonly int PetId;
        public byte SpawnLevel;           // m_spawnLevel
        public long JugSpawnMs;           // m_jugSpawnTime
        public long JugDurationMs;        // m_jugDuration
        public byte DmgType;              // the main weapon's damage type (xi::DamageType; mobs have none ported)

        public PetEntity(PetType petType, int petId)
        {
            PetType = petType;
            PetId = petId;
            Ecosystem = XI.Zone.Ecosystem.Unclassified;
            Allegiance = CharData.AllegiancePlayer;
            MobSkillList = 0;
            SpawnAnimation = 1; // the initial spawn has the special spawn-in animation
            // CPetController: SetWeaponSkillEnabled(false) (no special skill, no TP moves of its own)
            MobAbilityEnabled = false;
        }

        // isBstPet: a jug pet (a charmed mob is a MobEntity, not a PetEntity: ZoneHost.PlayerPets.cs counts it itself)
        public bool IsBstPet => PetType == PetType.JugPet;

        // GetScriptName: the pet's script under xi.pets
        public string ScriptName => PetType switch
        {
            PetType.Avatar => "avatar",
            PetType.Wyvern => "wyvern",
            PetType.JugPet => "jug",
            PetType.CharmedMob => "charmed",
            PetType.Automaton => "automaton",
            PetType.AdventuringFellow => "fellow",
            PetType.Chocobo => "chocobo",
            PetType.Luopan => "luopan",
            _ => "",
        };
    }

    // CPetSyncPacket (0x068): the master's pet, its HP, MP, TP, the target it fights and its name; an empty one when
    // the master has no pet
    public static class PetSyncPacket
    {
        public static BasicPacket Create(CharData c)
        {
            var w = new PetSyncWriter(c);
            return w.Packet;
        }

        private sealed class PetSyncWriter
        {
            public readonly BasicPacket Packet;

            public PetSyncWriter(CharData c)
            {
                var w = new PacketWriter(0x068, 0x1C);
                var b = w.Packet.Buffer;
                b[0x04] |= 0x04; // message type
                PackSize(b, 0x18);
                w.U16(0x06, c.TargId);
                w.U32(0x08, c.Id);
                if (c.PPet is MobEntity pet)
                {
                    w.SetSize(0x2C);
                    PackSize(b, 0x18 + pet.Name.Length);
                    w.U16(0x0C, pet.TargId);
                    b[0x0E] = pet.Hpp;
                    b[0x0F] = pet.Mpp;
                    w.U16(0x10, (ushort)pet.Tp);
                    if (pet.BattleTarget is BattleEntity target && pet.Animation == Anim.Attack) w.U32(0x14, target.Id);
                    for (int i = 0; i < pet.Name.Length && 0x18 + i < b.Length; i++) b[0x18 + i] = (byte)pet.Name[i];
                }
                Packet = w.Packet;
            }

            // packBitsBE(buffer + 0x04, size, 0, 6, 10): the message size in the 10 bits from bit 6 of 0x04 (low bits first)
            private static void PackSize(byte[] b, int size)
            {
                int v = b[0x04] | b[0x05] << 8;
                v = v & ~(0x3FF << 6) | (size & 0x3FF) << 6;
                b[0x04] = (byte)v;
                b[0x05] = (byte)(v >> 8);
            }
        }
    }
}
