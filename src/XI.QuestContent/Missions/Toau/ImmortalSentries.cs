// Immortal Sentries (ToAU 2, Log ID 4, Mission ID 1), a line-for-line port of
// scripts/missions/toau/02_Immortal_Sentries.lua (quest rework C3 T1). One section (the mission current).
// Daswil !pos -208.720 -12.889 -779.713 52; Meyaada !pos 22.446 -7.920 573.390 54; Nahshib !pos -274.334 -9.287 -64.255
// 79; Nareema !pos 518.387 -24.707 -467.297 79; Waudeen !pos 673.882 -23.995 367.604 61; Naja Salaheem !pos 22.700
// -8.804 -45.591 50. Aht Urhgan Whitegate: Naja plays 3002 (text table 0) without the Supplies Package, else 3001 (the
// default dialog, not a progress event); 3002's finish sends MEMBER_OF_SALAHEEMS_SENTINELS, completes the mission (the
// PSC Wildcat Badge, Private Second Class, President Salaheem current) and sends ACCESS_TO_A_MOG_LOCKER. Each outpost's
// immortal plays 5 with the package, else 6 (Waudeen: 4 / 5; Nareema adds a second operand 1); the finish with option 1
// takes the package, gives 150 imperial standing and sends HAND_OVER_TO_IMMORTAL and YOUR_IMPERIAL_STANDING. Kept as
// the Lua has it: the handover never completes the mission, and the finish does not test the package again (the
// standing is given with option 1 whether or not the package is held).
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Toau
{
    public sealed class ImmortalSentries : MissionScript
    {
        public ImmortalSentries() : base(XI.Quests.MissionLog.ToAU, "IMMORTAL_SENTRIES") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/toau/02_Immortal_Sentries";

        static void HandlePackage(IQuestContext q)
        {
            q.Player.DelKeyItem(KeyItem.SuppliesPackage);
            q.Player.AddCurrency("imperial_standing", 150);
        }

        // an immortal's trigger: the first event with the package, the second without (Nareema: a second operand 1)
        static QuestReply Immortal(IQuestContext q, int withPackage, int without, bool operand = false)
        {
            int csid = q.Player.HasKeyItem(KeyItem.SuppliesPackage) ? withPackage : without;
            return operand ? QuestReply.ProgressEvent(csid, 1) : QuestReply.ProgressEvent(csid);
        }

        protected override void Build(MissionScriptBuilder b)
        {
            // TODO (Lua): npcUtil.completeMission should support granting IS
            b.Reward(new MissionReward
            {
                KeyItem = KeyItem.PscWildcatBadge,
                Title = Title.PrivateSecondClass,
                NextLog = XI.Quests.MissionLog.ToAU,
                NextMission = Mission.Toau.PresidentSalaheem,
            });

            b.State(current: true)
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.NajaSalaheem).OnTrigger(q =>
                {
                    if (!q.Player.HasKeyItem(KeyItem.SuppliesPackage))
                    {
                        // Naja Salaheem interactions require the 9th argument in events set to 0.
                        // This is because Aht Uhrgan Whitegate uses 2 different dats.
                        return QuestReply.ProgressEvent(3002).TextTable(0);
                    }

                    return QuestReply.Event(3001).TextTable(0); // Default Dialog.
                })
                .OnEventFinish(Zone.AhtUrhganWhitegate, 3002, q =>
                {
                    q.Text.Special(Text.AhtUrhganWhitegate.MEMBER_OF_SALAHEEMS_SENTINELS);
                    q.Mission.Complete();
                    q.Text.Special(Text.AhtUrhganWhitegate.ACCESS_TO_A_MOG_LOCKER);
                })
                .Npc(Zone.ArrapagoReef, Npc.ArrapagoReef.Meyaada).OnTrigger(q => Immortal(q, 5, 6))
                .OnEventFinish(Zone.ArrapagoReef, 5, q =>
                {
                    if (q.Option == 1)
                    {
                        HandlePackage(q);
                        q.Text.Special(Text.ArrapagoReef.HAND_OVER_TO_IMMORTAL, KeyItem.SuppliesPackage);
                        q.Text.Special(Text.ArrapagoReef.YOUR_IMPERIAL_STANDING);
                    }
                })
                .Npc(Zone.BhaflauThickets, Npc.BhaflauThickets.Daswil).OnTrigger(q => Immortal(q, 5, 6))
                .OnEventFinish(Zone.BhaflauThickets, 5, q =>
                {
                    if (q.Option == 1)
                    {
                        HandlePackage(q);
                        q.Text.Special(Text.BhaflauThickets.HAND_OVER_TO_IMMORTAL, KeyItem.SuppliesPackage);
                        q.Text.Special(Text.BhaflauThickets.YOUR_IMPERIAL_STANDING);
                    }
                })
                .Npc(Zone.CaedarvaMire, Npc.CaedarvaMire.Nahshib).OnTrigger(q => Immortal(q, 5, 6))
                .Npc(Zone.CaedarvaMire, Npc.CaedarvaMire.Nareema).OnTrigger(q => Immortal(q, 5, 6, operand: true))
                .OnEventFinish(Zone.CaedarvaMire, 5, q =>
                {
                    if (q.Option == 1)
                    {
                        HandlePackage(q);
                        q.Text.Special(Text.CaedarvaMire.HAND_OVER_TO_IMMORTAL, KeyItem.SuppliesPackage);
                        q.Text.Special(Text.CaedarvaMire.YOUR_IMPERIAL_STANDING);
                    }
                })
                .Npc(Zone.MountZhayolm, Npc.MountZhayolm.Waudeen).OnTrigger(q => Immortal(q, 4, 5))
                .OnEventFinish(Zone.MountZhayolm, 4, q =>
                {
                    if (q.Option == 1)
                    {
                        HandlePackage(q);
                        q.Text.Special(Text.MountZhayolm.HAND_OVER_TO_IMMORTAL, KeyItem.SuppliesPackage);
                        q.Text.Special(Text.MountZhayolm.YOUR_IMPERIAL_STANDING);
                    }
                });
        }
    }
}
