// Chocobo Whistle (hidden quest ChocoboWhistle), ported from scripts/quests/hiddenQuests/Chocobo_Whistle.lua (quest
// rework B3b hidden quests). Hantileon : !pos -2.675 -0.100 -105.287 230. Starts when a raised chocobo grows up (the
// chocobo raising library, which stays Lua, writes the Prog var HQuest[ChocoboWhistle]Prog:
// xi.chocoboRaising.whistleQuestVar, model.lua:19; prog = xi.chocoboRaising.whistleProg: NOT_STARTED 0, SEE_HANTILEON
// 1, SEARCH 2, FOUND 3, DONE 4). Step 1 (ENABLE_CHOCOBO_RAISING and Prog 1): Hantileon starts the string event 829 (the
// chocobo's full, first and last name, '', VanadielTime(), 0, 0, 2) himself and answers noAction; 829's update 244
// (draw the chocobo): updateEvent(0, 0, 1, 0, 4, 1); its finish: Prog 2 (the search runs on the chocobo's walks). Step
// 2 (ENABLE_CHOCOBO_RAISING and Prog 3): Hantileon's string event 830 (the names, VanadielTime(), 2 with the
// Handkerchief key item, else 1); its finish on option 222 (RECEIVE_WHISTLE):
// xi.chocoboRaising.setWhistleProgress(DONE) (the user flag, the var cleared), both handkerchiefs taken,
// xi.chocoboRaising.whistle.giveQuestWhistle (the whistle, or the pending var with no room); any other option: nothing.
// Kept as the Lua has it: the text 3 (Hantileon's own handkerchief) is never sent (the Lua's TODO); the setting reads
// the Lua's truthiness (Settings.MainTruthy: only false and nil are off, so a 0 is on; true by default:
// settings/default/main.lua:301).
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.HiddenQuests
{
    public sealed class ChocoboWhistle : HiddenQuestScript
    {
        public ChocoboWhistle() : base("ChocoboWhistle") { }

        public override string LegacyPath => "scripts/quests/hiddenQuests/Chocobo_Whistle";

        // xi.chocoboRaising.whistleProg (scripts/globals/hobbies/chocobo_raising/model.lua:43-50)
        private const int ProgSeeHantileon = 1;
        private const int ProgSearch = 2;
        private const int ProgFound = 3;
        private const int ProgDone = 4;

        // xi.chocoboRaising.whistle.option.RECEIVE_WHISTLE (whistle.lua:45)
        private const int OptionReceiveWhistle = 222;

        // Event 830 text. TODO: Nothing sends 3, the text for Hantileon's own handkerchief.
        private const int HandkerchiefTextDirty = 1;
        private const int HandkerchiefTextFromWorker = 2;

        // xi.settings.main.ENABLE_CHOCOBO_RAISING: Lua truthiness (Settings.MainTruthy), so a 0 is on
        private static bool RaisingEnabled(IQuestCheck q) => q.Settings.MainTruthy("ENABLE_CHOCOBO_RAISING");

        // startStringEvent(player, csid, ...): the chocobo's names ('' each with no raised chocobo), then the numbers;
        // the event is started here and the action is noAction
        private static QuestReply StartStringEvent(IQuestContext q, int csid, params int[] args)
        {
            // local chocoState = player:getChocoboRaisingInfo(); if chocoState then fullName, firstName, lastName =
            // xi.chocoboRaising.nameStrings(chocoState) end ('', '', '' without one)
            var (fullName, firstName, lastName) = q.Player.ChocoboNameStrings;

            // player:startEventString(csid, fullName, firstName, lastName, '', ...)
            q.Player.StartEventString(csid, fullName, firstName, lastName, "", args);

            return QuestReply.NoAction();
        }

        protected override void Build(QuestScriptBuilder b)
        {
            b.Steps(ProgSeeHantileon, ProgSearch, ProgFound);

            // Step 1: Hantileon's scene starts the search, which runs on the chocobo's walks.
            b.AnyState(when: q =>
                    RaisingEnabled(q) &&
                    q.Quest.Step == ProgSeeHantileon)
                .Npc(Zone.SouthernSanDoria, Npc.SouthernSanDoria.Hantileon).OnTrigger(q =>
                    // VanadielTime()
                    StartStringEvent(q, 829, q.Player.VanadielTime, 0, 0, 2))
                .OnEventUpdate(Zone.SouthernSanDoria, 829, q =>
                {
                    // 244 is the event asking to draw the chocobo.
                    if (q.Option == 244)
                    {
                        q.Player.UpdateEvent(0, 0, 1, 0, 4, 1);
                    }
                })
                .OnEventFinish(Zone.SouthernSanDoria, 829, q =>
                {
                    q.Quest.SetStep(ProgSearch);
                });

            // Step 2: Bring the handkerchief back to Hantileon (Reward: Chocobo Whistle).
            b.AnyState(when: q =>
                    RaisingEnabled(q) &&
                    q.Quest.Step == ProgFound)
                .Npc(Zone.SouthernSanDoria, Npc.SouthernSanDoria.Hantileon).OnTrigger(q =>
                {
                    int variant = q.Player.HasKeyItem(KeyItem.Handkerchief)
                        ? HandkerchiefTextFromWorker
                        : HandkerchiefTextDirty;

                    return StartStringEvent(q, 830, q.Player.VanadielTime, variant);
                })
                .OnEventFinish(Zone.SouthernSanDoria, 830, q =>
                {
                    if (q.Option != OptionReceiveWhistle)
                    {
                        return;
                    }

                    // xi.chocoboRaising.setWhistleProgress(player, prog.DONE) (the Lua global: DONE sets the user flag
                    // WHISTLE_QUEST_DONE and clears the var)
                    q.Player.SetChocoboWhistleProgress(ProgDone);
                    q.Player.DelKeyItem(KeyItem.DirtyHandkerchief);
                    q.Player.DelKeyItem(KeyItem.Handkerchief);
                    // xi.chocoboRaising.whistle.giveQuestWhistle(player) (the Lua global: npcUtil.giveItem of the
                    // whistle, else the pending var 1)
                    q.Player.GiveChocoboQuestWhistle();
                });
        }
    }
}
