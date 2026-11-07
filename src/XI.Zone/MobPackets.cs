using XI.Map;

namespace XI.Zone
{
    // CEntityUpdatePacket (0x00E) for a mob or a pet: the TYPE_MOB / TYPE_PET branch of updateWith (a char's pet has
    // the 0x08 bit at 0x27 and is never triggerable). A mob has no status effects yet, so the Terror bit is never set.
    public static partial class EntityUpdatePacket
    {
        private const ushort ZonePsoxja = 9;
        private const byte AllegiancePlayer = 1;
        private const byte AnimationDeath = 3;

        public static BasicPacket Mob(MobEntity mob, EntityUpdate type, byte updatemask)
        {
            var w = new PacketWriter(0x00E, 0x58);
            w.U32(0x04, mob.Id);
            UpdateWith(w.Packet, mob, type, updatemask);
            return w.Packet;
        }

        public static void UpdateWith(BasicPacket packet, MobEntity mob, EntityUpdate type, byte updatemask)
        {
            var w = new PacketWriter(packet);
            var b = packet.Buffer;
            var model = (ModelType)mob.Look[0];
            w.U16(0x08, mob.TargId);
            b[0x0A] |= updatemask;

            switch (type)
            {
                case EntityUpdate.Despawn:
                    b[0x1F] = 0x02; // despawn animation
                    b[0x0A] = 0x30;
                    updatemask = UpdateAllMob;
                    break;
                case EntityUpdate.Spawn:
                    updatemask = UpdateAllMob;
                    if (model == ModelType.Equipped || model == ModelType.Chocobo) updatemask = 0x57;
                    if (mob.AnimationSub != 0) b[0x2A] = 4;
                    if (mob.SpawnAnimation == 1) b[0x28] |= 0x04;
                    b[0x0A] = updatemask;
                    break;
            }

            if ((updatemask & UpdatePos) != 0)
            {
                b[0x0B] = mob.Rotation;
                w.F32(0x0C, mob.X);
                w.F32(0x10, mob.Y);
                w.F32(0x14, mob.Z);
                w.U16(0x18, mob.Moving);
                w.U16(0x1A, mob.BattleTargId << 1);
                b[0x1C] = mob.Speed;
                b[0x1D] = mob.AnimationSpeed;
            }

            b[0x20] = mob.Allegiance == AllegiancePlayer && mob.Status == NpcEntity.StatusUpdate ? NpcEntity.StatusNormal : mob.Status;

            if ((updatemask & UpdateHp) != 0)
            {
                b[0x1E] = mob.Hpp;
                b[0x1F] = mob.Animation;
                b[0x2A] |= mob.AnimationSub;
                w.U32(0x21, mob.EntityFlags);
                b[0x25] = (byte)(mob.Hp > 0 ? 0x08 : 0);
                b[0x27] = mob.NamePrefix;
                if (mob.PMaster is CharData) b[0x27] |= 0x08;
                // the client renders Pso'Xja's mobs only with CliPriorityFlag
                if (mob.Zone == ZonePsoxja) b[0x28] |= 0x20;
                if (mob.Hp > 0 && mob.Animation == AnimationDeath) b[0x28] |= 0x08;
                if (mob.Status == NpcEntity.StatusNormal && mob is not PetEntity and not TrustEntity) b[0x28] |= 0x40; // triggerable
                b[0x29] = mob.Allegiance;
                b[0x2B] = mob.NameVis;
            }

            if (mob.PriorityRender) b[0x28] |= 0x20;

            // a trust's special spawn animation, which also lets it be despawned
            if (mob is TrustEntity) b[0x28] |= 0x45;

            if ((updatemask & UpdateStatus) != 0) w.U32(0x2C, mob.OwnerId);

            if ((updatemask & UpdateName) != 0)
            {
                w.SetSize(0x48);
                w.Name(0x34, mob.PacketName.Length == 0 ? mob.Name : mob.PacketName, PacketNameLength);
            }

            switch (model)
            {
                case ModelType.Standard:
                case ModelType.Unk5:
                case ModelType.Automaton:
                    w.U32(0x30, (uint)(mob.Look[0] | mob.Look[1] << 16));
                    break;
                case ModelType.Equipped:
                case ModelType.Chocobo:
                    w.SetSize(0x48);
                    for (int k = 0; k < 10; k++) w.U16(0x30 + k * 2, mob.Look[k]);
                    break;
                case ModelType.Door:
                    w.SetSize(0x48);
                    w.U16(0x30, mob.Look[0]);
                    w.Name(0x34, mob.Name, PacketNameLength);
                    break;
            }

            if (mob.IsRenamed) WriteRenamed(w, mob.TargId, mob.PacketName, updatemask);
        }
    }
}
