// Divine Might (Log ID 5, Quest ID 163), a line-for-line port of scripts/quests/outlands/Divine_Might.lua (quest rework
// B3b Outlands, group 4). The Shrine of Ru'Avitau: blank_divine_might !pos -40 -1.6 -148.2 (no script; Rhapsodies of
// Zilart 14 Ark Angels' 53, a progress cutscene at ARK_ANGELS status 0, beats this quest's 56 there; the default
// NOTHING_OUT_OF_ORDINARY). Ro'Maeve: QuHau_Spring !pos 0.2 -27.4 67.8 (no script; Windurst 8-1 / 9-2 answer its trigger
// only; the default MYSTERIOUS_ANCIENT_MONUMENT). La'Loff Amphitheater: the Divine Might battlefield
// (scripts/battlefields/LaLoff_Amphitheater/divine_might.lua: entry by the Ark Pentasphere traded to qm1_1, a char with
// this quest or the repeat accepted or completed) wins with 32001; at ARK_ANGELS status 1 the mission's own 32001 handler
// runs on the same finish (the five shards, the mission completed).
// Available (the incomplete arm): on Zilart ARK_ANGELS at status 1, 54 (parchment, illuminink, Ark Pentasphere); on any
// later Zilart mission (or ARK_ANGELS at another status) holding one of the five shards (Apathy .. Rage), 56 (same args);
// else nil. 54 / 56 finish: begun. Accepted: Prog 1 plays 55 (the five earrings), else the incomplete arm. 55's update
// with option 2 sends the earrings; its finish: options 1-5 pick Suppanomimi, Knight's, Abyssal, Beastly Earring or
// Bushinomimi; when given, quest:complete (Pentacide Perpetrator) and the char var DM_Earring = the earring. Qu'Hau Spring:
// on the full moon from 18:00 to 05:59 the illuminink and the parchment alone play 7 (parchment, illuminink); 7's finish
// gives the Ark Pentasphere and on true confirms the trade. 32001 with battlefieldWin == DIVINE_MIGHT (293): Prog 1.
// Completed: no section (Divine Might (Repeat) takes over).
// Kept as the Lua has it: the incomplete arm's ARK_ANGELS test reads the status 1 only, so ARK_ANGELS at status 2 or more
// falls to the shard test, and a char with no Zilart mission (getCurrentMission reads 65535) holding a shard gets 56; 55's
// escape (option 0, 6+) does nothing; the spring's trade does not check Prog (a second pentasphere while one is held:
// giveItem's rare check fails, nothing taken); DM_Earring is a forever char var; Ark Angels' DivineMight char var (set
// at its 32001 while this quest is accepted) is read by nothing.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Outlands
{
    public sealed class DivineMight : QuestScript
    {
        // xi.moonCycle.FULL_MOON (scripts/enum/moon_cycle.lua)
        const int FullMoon = 6;

        // the Lua's earringRewards, [1] .. [5]
        static readonly int[] EarringRewards =
        {
            Item.Suppanomimi,
            Item.KnightsEarring,
            Item.AbyssalEarring,
            Item.BeastlyEarring,
            Item.Bushinomimi,
        };

        public DivineMight() : base(QuestLog.Outlands, "DIVINE_MIGHT") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/outlands/Divine_Might";

        static QuestReply OnTriggerIncomplete(IQuestContext q)
        {
            int currentMission = q.Player.CurrentMission(MissionLog.Zilart);

            if (currentMission == Mission.Zilart.ArkAngels &&
                // Player.MissionStatus(int missionLog) = player:getMissionStatus(xi.mission.log_id.ZILART)
                q.Player.MissionStatus(MissionLog.Zilart) == 1)
            {
                return QuestReply.Event(54, Item.SheetOfParchment, Item.BottleOfIlluminink, Item.ArkPentasphere);
            }
            else if (currentMission >= Mission.Zilart.ArkAngels)
            {
                // NOTE: In order for a player to have the appropriate KI for an event to be displayed, they will be at least
                // at missionStatus 1, so no need to verify greater than.

                for (int keyItemId = KeyItem.ShardOfApathy; keyItemId <= KeyItem.ShardOfRage; keyItemId++)
                {
                    if (q.Player.HasKeyItem(keyItemId))
                    {
                        return QuestReply.Event(56, Item.SheetOfParchment, Item.BottleOfIlluminink, Item.ArkPentasphere);
                    }
                }
            }
            return null;
        }

        // earringRewards[option]: nil outside 1 .. 5
        static int? SelectedReward(int option) =>
            option >= 1 && option <= EarringRewards.Length ? EarringRewards[option - 1] : null;

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward { Title = Title.PentacidePerpetrator });
            b.Steps(1);

            b.State(QuestStatus.Available)
                .Npc(Zone.TheShrineOfRuavitau, Npc.TheShrineOfRuavitau.blankdivinemight).OnTrigger(OnTriggerIncomplete)
                .OnEventFinish(Zone.TheShrineOfRuavitau, 54, q =>
                {
                    q.Quest.Begin();
                })
                .OnEventFinish(Zone.TheShrineOfRuavitau, 56, q =>
                {
                    q.Quest.Begin();
                });

            b.State(QuestStatus.Accepted)
                .Npc(Zone.TheShrineOfRuavitau, Npc.TheShrineOfRuavitau.blankdivinemight).OnTrigger(q =>
                {
                    if (q.Quest.Step == 1)
                    {
                        return QuestReply.ProgressEvent(55, Item.Suppanomimi, Item.KnightsEarring, Item.AbyssalEarring, Item.BeastlyEarring, Item.Bushinomimi);
                    }
                    else
                    {
                        return OnTriggerIncomplete(q);
                    }
                })
                .OnEventUpdate(Zone.TheShrineOfRuavitau, 55, q =>
                {
                    if (q.Option == 2)
                    {
                        q.Player.UpdateEvent(Item.Suppanomimi, Item.KnightsEarring, Item.AbyssalEarring, Item.BeastlyEarring, Item.Bushinomimi);
                    }
                })
                .OnEventFinish(Zone.TheShrineOfRuavitau, 55, q =>
                {
                    int? selectedReward = SelectedReward(q.Option);

                    if (selectedReward != null &&
                        q.Player.GiveItem(selectedReward.Value))
                    {
                        q.Quest.Complete();

                        // TODO: Find a way to prevent the need for using a forever charVar here.
                        q.Player.SetCharVar("DM_Earring", selectedReward.Value);
                    }
                })
                .Npc(Zone.Romaeve, Npc.Romaeve.QuHauSpring).OnTrade(q =>
                {
                    int vanaHour = q.Player.VanadielHour;

                    if (
                        // Player.VanadielMoonCycle (int) = getVanadielMoonCycle()
                        (q.Player.VanadielMoonCycle == FullMoon) &&
                        (vanaHour >= 18 || vanaHour < 6) &&
                        q.Trade.HasExactly(Item.BottleOfIlluminink, Item.SheetOfParchment))
                    {
                        return QuestReply.ProgressEvent(7, Item.SheetOfParchment, Item.BottleOfIlluminink);
                    }
                    return null;
                })
                .OnEventFinish(Zone.Romaeve, 7, q =>
                {
                    if (q.Player.GiveItem(Item.ArkPentasphere))
                    {
                        q.Trade.Confirm();
                    }
                })
                .OnEventFinish(Zone.LaloffAmphitheater, 32001, q =>
                {
                    if (q.Player.LocalVar("battlefieldWin") == Battlefield.DivineMight)
                    {
                        q.Quest.SetStep(1);
                    }
                });
        }
    }
}
