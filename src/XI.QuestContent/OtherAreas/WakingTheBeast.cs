// Waking the Beast (Log ID 4, Quest ID 32), a line-for-line port of scripts/quests/otherAreas/Waking_the_Beast.lua
// (quest rework B3b Other Areas group 9). La Theine Plateau: qm3 !pos -179 8 254 (no script; DefaultActions; shared
// with Windurst's I Can Hear a Rainbow and CoP 5-3); the six Cloisters' and the Full Moon Fountain's Waking the Beast
// battlefields (BattlefieldQuest: entry with the Rainbow Resonator, or the six eyes and the resonator at the Fountain).
// Section 1: available with Ifrit, Garuda, Shiva, Ramuh, Leviathan and Titan learnt, or completed with neither the
// Faded Ruby nor the Rainbow Resonator and completedThisWeek 0: qm3 207, whose finish gives the resonator and begins
// the quest when it is available. Section 2: accepted or completed, the Faded Ruby held, completedThisWeek 0: qm3 208,
// whose finish runs quest:complete (Carbuncle's Pole) and on true sets completedThisWeek 1 (expiring at
// NextConquestTally()), takes the ruby and adds the title by the Option as read before quest:complete (0:
// Disturber of Slumber, else Interrupter of Dreams). Section 3: accepted or completed, the six eyes and the resonator
// held: the Fountain's 32001 finish with the char's local battlefieldWin 226 (WAKING_THE_BEAST_FULLMOON): the seven
// key items taken, the Faded Ruby, Option 1 on option 0 else 0. Section 4:
// accepted or completed, the resonator held: each Cloister's 32001 finish with battlefieldWin its Waking the Beast id
// (Flames 546, Frost 483, Gales 419, Storms 451, Tides 610, Tremors 579): its eye.
// Kept as the Lua has it: sections 3 and 4 both hold at the Fountain's and the Cloisters' 32001 with every key item
// held (each zone has its own handler, so they never meet); the eye is given again by a second win (giveKeyItem of a
// held key item: its message only); 208's quest:complete false (no room for the pole) keeps the ruby and the week open;
// the Option's sense is inverted (option 0 sets 1).
// A deliberate change from the Lua (user ruling 2026-11-04, kept Lua bugs): the Lua's 208 reads Option after
// quest:complete, whose cleanup (Container:cleanup, clearVarsWithPrefix) has already cleared it, so its read was always
// 0 and the title always Disturber of Slumber; the C# reads Option first, so Option 1 gives Interrupter of Dreams.
// A deliberate change from the Lua (user ruling 2026-11-05, kept bugs): upstream's BattlefieldQuest:onBattlefieldWin
// (scripts/globals/battlefield.lua) sets battlefieldWin only while the quest is accepted, so a completed char's
// Cloister and Fountain wins gave no eye and no ruby and 208 never repeated. The seven Waking the Beast battlefields
// now set BattlefieldQuest's repeatable flag, so a completed char's win sets battlefieldWin too and the completed
// alternatives of sections 3 and 4 give the eyes and the ruby: the weekly repeat works (no other battlefield sets it).
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.OtherAreas
{
    public sealed class WakingTheBeast : QuestScript
    {
        public WakingTheBeast() : base(QuestLog.OtherAreas, "WAKING_THE_BEAST") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/otherAreas/Waking_the_Beast";

        // xi.battlefield.id (scripts/globals/battlefield.lua)
        const int WakingTheBeastFullmoon = 226;
        const int WakingTheBeastCloisterOfGales = 419;
        const int WakingTheBeastCloisterOfStorms = 451;
        const int WakingTheBeastCloisterOfFrost = 483;
        const int WakingTheBeastCloisterOfFlames = 546;
        const int WakingTheBeastCloisterOfTremors = 579;
        const int WakingTheBeastCloisterOfTides = 610;

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward
            {
                Item = Item.CarbunclesPole,
            });

            // check to start the quest the first time
            b.State(QuestStatus.Available, when: q =>
                    // condition for the starting the quest the first time
                    q.Player.HasSpell(Spell.Ifrit) &&
                    q.Player.HasSpell(Spell.Garuda) &&
                    q.Player.HasSpell(Spell.Shiva) &&
                    q.Player.HasSpell(Spell.Ramuh) &&
                    q.Player.HasSpell(Spell.Leviathan) &&
                    q.Player.HasSpell(Spell.Titan))
                // condition for the starting the quest (after completing previously)
                .Or(QuestStatus.Completed, when: q =>
                    !q.Player.HasKeyItem(KeyItem.FadedRuby) &&
                    !q.Player.HasKeyItem(KeyItem.RainbowResonator) &&
                    q.Quest.Var("completedThisWeek") == 0)
                .Npc(Zone.LaTheinePlateau, Npc.LaTheinePlateau.qm3).OnTrigger(QuestReply.ProgressEvent(207))
                .OnEventFinish(Zone.LaTheinePlateau, 207, q =>
                {
                    q.Player.GiveKeyItem(KeyItem.RainbowResonator);
                    // if starting for the first time then begin the quest
                    if (q.Quest.Status == QuestStatus.Available)
                    {
                        q.Quest.Begin();
                    }
                });

            // check for completing the quest the first time or subsequent times
            b.State(QuestStatus.Accepted, when: FadedRubyThisWeek)
                .Or(QuestStatus.Completed, when: FadedRubyThisWeek)
                .Npc(Zone.LaTheinePlateau, Npc.LaTheinePlateau.qm3).OnTrigger(QuestReply.ProgressEvent(208))
                .OnEventFinish(Zone.LaTheinePlateau, 208, q =>
                {
                    // read before quest:complete, whose cleanup clears it (the Lua read it after: always 0; fixed,
                    // user ruling 2026-11-04)
                    int optionVar = q.Quest.Var("Option");

                    if (q.Quest.Complete())
                    {
                        q.Quest.SetVar("completedThisWeek", 1, (uint)q.Player.NextConquestTally);
                        q.Player.DelKeyItem(KeyItem.FadedRuby);

                        if (optionVar == 0)
                        {
                            q.Player.AddTitle(Title.DisturberOfSlumber);
                        }
                        else
                        {
                            q.Player.AddTitle(Title.InterrupterOfDreams);
                        }
                    }
                });

            // the completed alternative: the weekly repeat (BattlefieldQuest's repeatable flag sets battlefieldWin
            // for a completed char's win too: user ruling 2026-11-05, kept bugs)
            b.State(QuestStatus.Accepted, when: AllEyes)
                .Or(QuestStatus.Completed, when: AllEyes)
                .OnEventFinish(Zone.FullMoonFountain, 32001, q =>
                {
                    if (q.Player.LocalVar("battlefieldWin") == WakingTheBeastFullmoon)
                    {
                        q.Player.DelKeyItem(KeyItem.EyeOfFlames);
                        q.Player.DelKeyItem(KeyItem.EyeOfFrost);
                        q.Player.DelKeyItem(KeyItem.EyeOfGales);
                        q.Player.DelKeyItem(KeyItem.EyeOfStorms);
                        q.Player.DelKeyItem(KeyItem.EyeOfTides);
                        q.Player.DelKeyItem(KeyItem.EyeOfTremors);
                        q.Player.DelKeyItem(KeyItem.RainbowResonator);

                        q.Player.GiveKeyItem(KeyItem.FadedRuby);

                        if (q.Option == 0)
                        {
                            q.Quest.SetVar("Option", 1);
                        }
                        else
                        {
                            q.Quest.SetVar("Option", 0);
                        }
                    }
                });

            // the completed alternative: the weekly repeat (BattlefieldQuest's repeatable flag sets battlefieldWin
            // for a completed char's win too: user ruling 2026-11-05, kept bugs)
            b.State(QuestStatus.Accepted, when: q => q.Player.HasKeyItem(KeyItem.RainbowResonator))
                .Or(QuestStatus.Completed, when: q => q.Player.HasKeyItem(KeyItem.RainbowResonator))
                .OnEventFinish(Zone.CloisterOfFlames, 32001,
                    q => CloisterWin(q, WakingTheBeastCloisterOfFlames, KeyItem.EyeOfFlames))
                .OnEventFinish(Zone.CloisterOfFrost, 32001,
                    q => CloisterWin(q, WakingTheBeastCloisterOfFrost, KeyItem.EyeOfFrost))
                .OnEventFinish(Zone.CloisterOfGales, 32001,
                    q => CloisterWin(q, WakingTheBeastCloisterOfGales, KeyItem.EyeOfGales))
                .OnEventFinish(Zone.CloisterOfStorms, 32001,
                    q => CloisterWin(q, WakingTheBeastCloisterOfStorms, KeyItem.EyeOfStorms))
                .OnEventFinish(Zone.CloisterOfTides, 32001,
                    q => CloisterWin(q, WakingTheBeastCloisterOfTides, KeyItem.EyeOfTides))
                .OnEventFinish(Zone.CloisterOfTremors, 32001,
                    q => CloisterWin(q, WakingTheBeastCloisterOfTremors, KeyItem.EyeOfTremors));
        }

        static bool FadedRubyThisWeek(IQuestCheck q) =>
            q.Player.HasKeyItem(KeyItem.FadedRuby) &&
            q.Quest.Var("completedThisWeek") == 0;

        static bool AllEyes(IQuestCheck q) =>
            q.Player.HasKeyItem(KeyItem.EyeOfFlames) &&
            q.Player.HasKeyItem(KeyItem.EyeOfFrost) &&
            q.Player.HasKeyItem(KeyItem.EyeOfGales) &&
            q.Player.HasKeyItem(KeyItem.EyeOfStorms) &&
            q.Player.HasKeyItem(KeyItem.EyeOfTides) &&
            q.Player.HasKeyItem(KeyItem.EyeOfTremors) &&
            q.Player.HasKeyItem(KeyItem.RainbowResonator);

        static void CloisterWin(IQuestContext q, int battlefieldId, int eye)
        {
            if (q.Player.LocalVar("battlefieldWin") == battlefieldId)
            {
                q.Player.GiveKeyItem(eye);
            }
        }
    }
}
