// The Chains of Promathia helpers (scripts/missions/cop/helpers.lua, xi.cop.helpers), ported for Below the Arks (1-2),
// The Mothercrystals (1-3) and the Promyvion spires' battlefields. Crags: Holla 1, Dem 2, Mea 3; each crag has a Light
// key item (Holla, Dem, Mea in id order), a Shattered Telepoint zone (La Theine, Konschtat, Tahrongi), a Promyvion zone
// and a Spire zone (Holla 16/17, Dem 18/19, Mea 20/21). Kept as the Lua has it: numPromyvionCompleted counts the three
// Lights, skipping the excluded crag; the telepoint's option 0 sets the char down in the Hall of Transference; sealing
// memories sets Option 0 first and then sends the zone's MEMORIES_SEALED_OFF, which Tahrongi Canyon's text table lacks
// (the Lua's messageSpecial(nil) raises "bad argument #2 (number expected, got nil)", after the Option is already
// cleared); a cermet gate's number is the last character of its name plus one; an apparatus's crag is the last
// character of its name over three, rounded up; the Hall's door text is sent at priority 1000. Teleport ids are
// xi.teleport.id's (EXITPROMHOLLA 26, EXITPROMDEM 27, EXITPROMMEA 28, LUFAISE 29).
using System;
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Cop
{
    public static class PromyvionHelpers
    {
        public const int Holla = 1;
        public const int Dem = 2;
        public const int Mea = 3;

        const int ExitPromHolla = 26;
        const int Lufaise = 29;

        // shatteredTelepointInfo[zone]: { crag, entry csid, seal csid, hall position, light key item }
        public sealed class TelepointInfo
        {
            public int Crag; public int Entry; public int Seal; public int Light;
            public double X, Y, Z; public int Rot; public int HallZone;
        }

        static readonly TelepointInfo LaTheine = new TelepointInfo
        {
            Crag = Holla, Entry = 202, Seal = 212, Light = KeyItem.LightOfHolla,
            X = -266.76, Y = -0.635, Z = 280.058, Rot = 0, HallZone = 14,
        };

        static readonly TelepointInfo Konschtat = new TelepointInfo
        {
            Crag = Dem, Entry = 913, Seal = 918, Light = KeyItem.LightOfDem,
            X = -267.194, Y = -40.634, Z = -280.019, Rot = 0, HallZone = 14,
        };

        static readonly TelepointInfo Tahrongi = new TelepointInfo
        {
            Crag = Mea, Entry = 913, Seal = 918, Light = KeyItem.LightOfMea,
            X = 280.066, Y = -80.635, Z = -67.096, Rot = 191, HallZone = 14,
        };

        // null: no entry (the Lua's nil)
        public static TelepointInfo InfoOf(int zone) => zone switch
        {
            Zone.LaTheinePlateau => LaTheine,
            Zone.KonschtatHighlands => Konschtat,
            Zone.TahrongiCanyon => Tahrongi,
            _ => null,
        };

        static TelepointInfo Need(int zone) =>
            InfoOf(zone) ?? throw new InvalidOperationException("helpers.lua: attempt to index a nil value");

        // numPromyvionCompleted(player, excludeArea): the Lights held, the excluded crag's (1 to 3) left out; 0: none
        public static int NumCompleted(IQuestPlayerView player, int excludeArea = 0)
        {
            int count = 0;
            for (int keyItem = KeyItem.LightOfHolla; keyItem <= KeyItem.LightOfMea; keyItem++)
            {
                if (player.HasKeyItem(keyItem) &&
                    (excludeArea == 0 || KeyItem.LightOfHolla + excludeArea - 1 != keyItem))
                    count++;
            }

            return count;
        }

        public static bool HasCompleted(IQuestPlayerView player, int zone) => player.HasKeyItem(Need(zone).Light);

        // the Option of the CoP log's current mission (xi.mission.getVar(player, COP, current, 'Option'))
        static int CurrentOption(IQuestContext q) =>
            q.Player.MissionVar(MissionLog.Cop, q.Player.CurrentMission(MissionLog.Cop), "Option");

        // promyvionOnZoneIn
        public static QuestReply PromyvionOnZoneIn(IQuestContext q)
        {
            if (CurrentOption(q) == 0) return QuestReply.Event(50 + NumCompleted(q.Player));
            return null;
        }

        // sendToPromyvionZone
        public static void SendToPromyvionZone(IQuestContext q, int crag)
        {
            if (crag == Holla) q.Player.SetPos(92.033, 0, 80.380, 255, 16);
            else if (crag == Dem) q.Player.SetPos(185.891, 0, -52.331, 128, 18);
            else if (crag == Mea) q.Player.SetPos(-93.268, 0, 170.749, 162, 20);
        }

        // sendToZoneOnFinish
        public static void SendToZoneOnFinish(IQuestContext q)
        {
            if (q.Option == 0) SendToPromyvionZone(q, q.Player.LocalVar("toPromyvion"));
        }

        // shatteredTelepointOnTrigger
        public static QuestReply TelepointOnTrigger(IQuestContext q, int zone)
        {
            var info = Need(zone);
            int promyvionOffset = info.Crag - 1;
            int currentMemory = q.Mission.Var("Option");

            if (q.Player.HasKeyItem(KeyItem.LightOfHolla + promyvionOffset))
                return QuestReply.ProgressEvent(info.Entry);

            if (currentMemory == 0 || currentMemory == info.Crag || NumCompleted(q.Player) == 2)
            {
                int firstEntry = currentMemory == 0 &&
                                 q.Player.CurrentMission(MissionLog.Cop) == Mission.Cop.BelowTheArks ? 1 : 0;
                return QuestReply.ProgressEvent(info.Entry, 0, 0, firstEntry);
            }

            return QuestReply.ProgressEvent(info.Seal, currentMemory - 1);
        }

        // shatteredTelepointEntry
        public static void TelepointEntry(IQuestContext q, int zone)
        {
            if (q.Option == 0)
            {
                var info = Need(zone);
                q.Player.SetPos(info.X, info.Y, info.Z, info.Rot, info.HallZone);
            }
        }

        // shatteredTelepointSealMemory
        public static void TelepointSealMemory(IQuestContext q, int zone)
        {
            if (q.Option != 0) return;
            q.Mission.SetVar("Option", 0);
            switch (zone)
            {
                case Zone.LaTheinePlateau:
                    q.Text.Special(Text.LaTheinePlateau.MEMORIES_SEALED_OFF);
                    break;
                case Zone.KonschtatHighlands:
                    q.Text.Special(Text.KonschtatHighlands.MEMORIES_SEALED_OFF);
                    break;
                default:
                    // zones[zone].text.MEMORIES_SEALED_OFF is nil here
                    throw new InvalidOperationException("helpers.lua:106: bad argument #2 (number expected, got nil)");
            }
        }

        // the digit a Hall NPC's name ends with
        static int LastDigit(IQuestContext q)
        {
            string name = q.Npc.Name;
            return name[name.Length - 1] - '0';
        }

        // cermetGateOnTrigger
        public static QuestReply CermetGateOnTrigger(IQuestContext q)
        {
            int cermetLocation = LastDigit(q) + 1;
            if (q.Mission.Var("Option") != cermetLocation)
                return QuestReply.MessageSpecial(Text.HallOfTransference.DOOR_FIRMLY_SHUT).SetPriority(1000);
            return null;
        }

        // math.ceil(tonumber(string.sub(npc:getName(), -1)) / 3)
        public static int ApparatusCrag(IQuestContext q) => (LastDigit(q) + 2) / 3;

        // largeApparatusOnTrigger
        public static QuestReply LargeApparatusOnTrigger(IQuestContext q)
        {
            int currentMemory = q.Mission.Var("Option");

            if (currentMemory == 0 && q.Player.CurrentMission(MissionLog.Cop) == Mission.Cop.BelowTheArks)
                return QuestReply.ProgressEvent(160);

            if (NumCompleted(q.Player) < 2)
            {
                int cragLocation = ApparatusCrag(q);

                // TODO (Lua): previously-completed promyvions are assumed not to need their memories sealed
                if (currentMemory == cragLocation && !q.Player.HasKeyItem(KeyItem.LightOfHolla + cragLocation - 1))
                {
                    q.Player.SetLocalVar("exitOffset", cragLocation);
                    return QuestReply.ProgressEvent(122 + 3 * (cragLocation - 1));
                }
            }

            return null;
        }

        // largeApparatusOnEventFinish
        public static void LargeApparatusOnEventFinish(IQuestContext q)
        {
            if (q.Option == 0)
            {
                int exitOffset = q.Player.LocalVar("exitOffset") - 1;
                q.Mission.SetVar("Option", 0);
                q.Player.TeleportTo(ExitPromHolla + exitOffset);
            }
        }

        // spireEventFinish; zone is the spire's zone (Holla 17, Dem 19, Mea 21)
        public static void SpireEventFinish(IQuestContext q, int zone)
        {
            if (q.Player.LocalVar("newPromy") != 1) return;

            q.Player.SetLocalVar("newPromy", 0);

            // an offset into the 0-indexed crags
            int promyvionId = (zone - 17) / 2;
            int teleportLocation = ExitPromHolla + promyvionId;

            q.Player.AddKeyItem(KeyItem.LightOfHolla + promyvionId);
            ZoneText cantRemember = zone switch
            {
                Zone.SpireOfHolla => Text.SpireOfHolla.CANT_REMEMBER,
                Zone.SpireOfDem => Text.SpireOfDem.CANT_REMEMBER,
                _ => Text.SpireOfMea.CANT_REMEMBER,
            };
            q.Text.Special(cantRemember, KeyItem.LightOfHolla + promyvionId);
            q.Mission.SetVar("Option", 0);

            int numCompleted = NumCompleted(q.Player);

            if ((q.Player.CurrentMission(MissionLog.Cop) == Mission.Cop.BelowTheArks && numCompleted == 1) ||
                numCompleted == 3)
            {
                q.Mission.Complete();
            }

            if (numCompleted == 3)
            {
                // the fallthrough must not trigger an overriding teleport
                q.Player.SetLocalVar("toLufaise", 1);
                teleportLocation = Lufaise;
            }

            q.Player.TeleportTo(teleportLocation);
        }
    }
}
