// Emissaries of God (Log ID 8, Quest ID 182), a line-for-line port of scripts/quests/abyssea/Emissaries_of_God.lua
// (quest rework B3b Abyssea). Port Jeuno: Joachim !pos -52.844 0 -9.978 246 (his script: the traverser stone menu).
// Hall of the Gods: its zone-in. Added by Death and Rebirth's 7 (no available section). Accepted, with the six caturae
// titles (Iratham Capturer, Kutharei Unhorser, Sippoy Capturer, Yaanei Crasher, Rani Decrowner, Raja Regicide):
// Joachim: 344, whose finish gives the Abyssite of Discernment (npcUtil.giveKeyItem) and sets Prog 1. Accepted at
// Prog 1: the Hall of the Gods zone-in from 18:00 to 4:59: 8, whose finish completes it and adds Beneath a Blood Red
// Sky (accepted). Kept as the Lua has it: 344 answers at any Prog while the titles are held (its finish gives the held
// key item again: nothing, no message); 8 is returned as a number (an Event); no reward (the key item comes with 344);
// no completed section.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Abyssea
{
    public sealed class EmissariesOfGod : QuestScript
    {
        public EmissariesOfGod() : base(QuestLog.Abyssea, "EMISSARIES_OF_GOD") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/abyssea/Emissaries_of_God";

        // the Lua's caturaeTitles, in its order
        static readonly int[] CaturaeTitles =
        {
            Title.IrathamCapturer,
            Title.KuthareiUnhorser,
            Title.SippoyCapturer,
            Title.YaaneiCrasher,
            Title.RaniDecrowner,
            Title.RajaRegicide,
        };

        // the Lua's hasAllCaturaeTitles
        static bool HasAllCaturaeTitles(IQuestPlayerView player)
        {
            foreach (var titleId in CaturaeTitles)
            {
                if (!player.HasTitle(titleId))
                {
                    return false;
                }
            }

            return true;
        }

        protected override void Build(QuestScriptBuilder b)
        {
            b.Steps(1);

            // Key item given in first cutscene, not on quest complete
            b.Reward(new QuestReward());

            b.State(QuestStatus.Accepted, when: q => HasAllCaturaeTitles(q.Player))
                .Npc(Zone.PortJeuno, Npc.PortJeuno.Joachim).OnTrigger(QuestReply.ProgressEvent(344))
                .OnEventFinish(Zone.PortJeuno, 344, q =>
                {
                    q.Player.GiveKeyItem(KeyItem.AbyssiteOfDiscernment);
                    q.Quest.SetStep(1);
                });

            b.State(QuestStatus.Accepted, step: 1)
                .OnZoneIn(Zone.HallOfTheGods, q =>
                {
                    int hour = q.Player.VanadielHour;
                    if (hour >= 18 || hour < 5)
                    {
                        return QuestReply.Event(8);
                    }
                    return null;
                })
                .OnEventFinish(Zone.HallOfTheGods, 8, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Player.AddQuest(QuestLog.Abyssea, "BENEATH_A_BLOOD_RED_SKY");
                    }
                });
        }
    }
}
