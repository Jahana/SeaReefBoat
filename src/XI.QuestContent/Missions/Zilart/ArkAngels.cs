// Ark Angels (Zilart 14, Log ID 3, Mission ID 26), a line-for-line port of scripts/missions/rotz/14_Ark_Angels.lua
// (quest rework C3, C6). Gilgamesh !pos 122.452 -9.009 -12.052 252; blank_divine_might !pos -40 0 -151 178; the five
// fights' entrances qm1_1 to qm1_5 in La'Loff Amphitheater (180). Section 1 (current): Norg's Gilgamesh plays 171.
// Section 2 (current, status 0): the Shrine of Ru'Avitau's blank_divine_might plays the cutscene 53 (917, 1408, 1550),
// whose finish sets status 1. Section 3 (current, status 1): La'Loff Amphitheater's 32001 finish reads battlefieldWin
// - 288 (Ark Angels 1 to 5 and Divine Might are the battlefields 288 to 293): 0 to 4 gives that fight's shard (Apathy,
// Cowardice, Envy, Arrogance, Rage: the battlefield order, not the key items'); 5 (Divine Might) gives all five, from
// the Shard of Apathy's id to the Shard of Rage's, and sets the DivineMight char var to 2 while Divine Might is
// accepted. Then, with all five shards held, the mission completes (The Sealed Shrine next; no key item, no title). The
// fights' wins set battlefieldWin (Battlefields/LaloffAmphitheater/ArkAngels1.cs to ArkAngels5.cs; Divine Might's is
// Lua).
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Zilart
{
    public sealed class ArkAngels : MissionScript
    {
        public ArkAngels() : base(XI.Quests.MissionLog.Zilart, "ARK_ANGELS") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/rotz/14_Ark_Angels";

        // Table index based on Battlefield ID - Offset, this ordering is different from the keyItem table
        static readonly int[] KeyItemOffset =
        {
            KeyItem.ShardOfApathy,
            KeyItem.ShardOfCowardice,
            KeyItem.ShardOfEnvy,
            KeyItem.ShardOfArrogance,
            KeyItem.ShardOfRage,
        };

        protected override void Build(MissionScriptBuilder b)
        {
            b.Reward(new MissionReward
            {
                NextLog = XI.Quests.MissionLog.Zilart,
                NextMission = Mission.Zilart.TheSealedShrine,
            });

            b.State(current: true)
                .Npc(Zone.Norg, Npc.Norg.Gilgamesh).OnTrigger(QuestReply.Event(171));

            b.State(current: true, status: 0)
                .Npc(Zone.TheShrineOfRuavitau, Npc.TheShrineOfRuavitau.blankdivinemight)
                    .OnTrigger(QuestReply.ProgressCutscene(53, 917, 1408, 1550))
                .OnEventFinish(Zone.TheShrineOfRuavitau, 53, q => q.Player.SetMissionStatus(MissionLog.Zilart, 1));

            b.State(current: true, status: 1)
                .OnEventFinish(Zone.LaloffAmphitheater, 32001, q =>
                {
                    // Ark Angels and Divine Might are sequential, offset starts at 288
                    int keyItemIndex = q.Player.LocalVar("battlefieldWin") - 288;

                    // Single Fights
                    if (keyItemIndex >= 0 && keyItemIndex <= 4)
                    {
                        q.Player.GiveKeyItem(KeyItemOffset[keyItemIndex]);
                    }
                    // Divine Might
                    else if (keyItemIndex == 5)
                    {
                        for (int keyItemId = KeyItem.ShardOfApathy; keyItemId <= KeyItem.ShardOfRage; keyItemId++)
                        {
                            q.Player.GiveKeyItem(keyItemId);
                        }

                        if (q.Player.QuestStatus(QuestLog.Outlands, "DIVINE_MIGHT") == QuestStatus.Accepted)
                        {
                            q.Player.SetCharVar("DivineMight", 2);
                        }
                    }

                    if (q.Player.HasKeyItem(KeyItem.ShardOfApathy) &&
                        q.Player.HasKeyItem(KeyItem.ShardOfArrogance) &&
                        q.Player.HasKeyItem(KeyItem.ShardOfCowardice) &&
                        q.Player.HasKeyItem(KeyItem.ShardOfEnvy) &&
                        q.Player.HasKeyItem(KeyItem.ShardOfRage))
                    {
                        q.Mission.Complete();
                    }
                });
        }
    }
}
