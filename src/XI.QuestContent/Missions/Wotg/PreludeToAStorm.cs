// Prelude to a Storm (WotG 31, Log ID 5, Mission ID 30), a line-for-line port of
// scripts/missions/wotg/31_Prelude_to_a_Storm.lua (quest rework C3 T7). Rally Point: Green !pos 54.013 -23.402 -203.103
// 137; Spell-worked Snow !pos 75.989 -24.249 -248.089 137. One section (the mission current), in Xarcabard [S]:
// Rally Point: Red shows REQUIRED_TO_DELIVER (messageName, no entity) and plays nothing; Rally Point: Green plays the
// progress event 21 (137, 300, 200, 100, 0, 6553620, 0, 0) at Status 0, whose finish gives the Magelight Signal Flare
// and sets Status 1; at Status 1 with the flare it shows HELP_FEDERATION_PREPARE and plays nothing, without it plays
// the progress event 25 (137, 23, 2963), whose finish gives the flare again. The zone-in plays the plain event 22 at
// Status 2; its update with option 13 sends the update (0, 23, 1756, 0, 0, 0, 0, 2); its finish gives the reward of
// the Option var (1 an Elixir, 2 a Vile Elixir, 3 a Vile Elixir +1) and, when npcUtil.giveItem answers true,
// completes the mission (Storm's Crescendo current). Ghoyu's Reverie's event 10000 sets Status 2 and sets the char down
// at 96.77 -23.943 -277.87 rot 253 in Xarcabard [S]. Kept as the Lua has it: nothing sets Option (the Lua's TODO says
// the instance would), and an Option with no reward (0, or past 3) hands npcUtil.giveItem a nil, which gives nothing
// and answers true, so the mission completes with no item. No flow statement starts 10000 (the harness has no
// instance run); fake checks pin that finish and its set-down.
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Wotg
{
    public sealed class PreludeToAStorm : MissionScript
    {
        public PreludeToAStorm() : base(XI.Quests.MissionLog.WoTG, "PRELUDE_TO_A_STORM") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/wotg/31_Prelude_to_a_Storm";

        // the Lua's rewardItems, indexed by the Option var from 1
        public static readonly int[] RewardItems = { Item.Elixir, Item.VileElixir, Item.VileElixirP1 };

        // rewardItems[option]: nil for an index the table does not hold
        public static int? RewardItem(int option) =>
            option >= 1 && option <= RewardItems.Length ? RewardItems[option - 1] : null;

        protected override void Build(MissionScriptBuilder b)
        {
            b.Reward(new MissionReward
            {
                NextLog = XI.Quests.MissionLog.WoTG,
                NextMission = Mission.Wotg.StormsCrescendo,
            });

            b.State(current: true)
                .Npc(Zone.XarcabardS, Npc.XarcabardS.RallyPointRed).OnTrigger(q =>
                {
                    q.Text.MessageName(Text.XarcabardS.REQUIRED_TO_DELIVER);

                    return QuestReply.NoAction();
                })
                .Npc(Zone.XarcabardS, Npc.XarcabardS.RallyPointGreen).OnTrigger(q =>
                {
                    int missionStatus = q.Mission.Var("Status");

                    if (missionStatus == 0)
                    {
                        return QuestReply.ProgressEvent(21, 137, 300, 200, 100, 0, 6553620, 0, 0);
                    }
                    else if (missionStatus == 1)
                    {
                        if (q.Player.HasKeyItem(KeyItem.MagelightSignalFlare))
                        {
                            q.Text.MessageName(Text.XarcabardS.HELP_FEDERATION_PREPARE);

                            return QuestReply.NoAction();
                        }
                        else
                        {
                            return QuestReply.ProgressEvent(25, 137, 23, 2963);
                        }
                    }

                    return null;
                })
                .OnZoneIn(Zone.XarcabardS, q => q.Mission.Var("Status") == 2 ? QuestReply.Event(22) : null)
                .OnEventUpdate(Zone.XarcabardS, 22, q =>
                {
                    if (q.Option == 13)
                    {
                        q.Player.UpdateEvent(0, 23, 1756, 0, 0, 0, 0, 2);
                    }
                })
                .OnEventFinish(Zone.XarcabardS, 21, q =>
                {
                    q.Player.GiveKeyItem(KeyItem.MagelightSignalFlare);
                    q.Mission.SetVar("Status", 1);
                })
                .OnEventFinish(Zone.XarcabardS, 22, q =>
                {
                    int rewardItemID = q.Mission.Var("Option");

                    if (q.Player.GiveItemOrNil(RewardItem(rewardItemID)))
                    {
                        q.Mission.Complete();
                    }
                })
                .OnEventFinish(Zone.XarcabardS, 25, q =>
                {
                    q.Player.GiveKeyItem(KeyItem.MagelightSignalFlare);
                })
                .OnEventFinish(Zone.GhoyusReverie, 10000, q =>
                {
                    // the Lua's TODO: the script assumes the instance's clear sends 10000, and that the instance would
                    // set Option (1 to 3) by the time left
                    q.Mission.SetVar("Status", 2);
                    q.Player.SetPos(96.77, -23.943, -277.87, 253, Zone.XarcabardS);
                });
        }
    }
}
