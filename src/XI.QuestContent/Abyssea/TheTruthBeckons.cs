// The Truth Beckons (Log ID 8, Quest ID 161), a line-for-line port of scripts/quests/abyssea/The_Truth_Beckons.lua
// (quest rework B3b Abyssea). Port Jeuno: Joachim !pos -52.844 0 -9.978 246 (his script: the traverser stone menu).
// Added by A Journey Begins' 325 (no available section). Accepted: a zone-in to any of the nine Abyssea areas
// (Konschtat, La Theine, Tahrongi, Attohwa, Misareaux, Vunkerl, Altepa, Uleguerand, Grauberg) sets Prog 1 and plays
// nothing. Joachim at Prog 1: 327 (0, 0, the traverser stone cap), whose finish completes it and adds Dawn of Death
// (accepted); at any other Prog: 326. Kept as the Lua has it: the zone-ins set Prog 1 at any Prog and return nothing
// (the zone's own onZoneIn plays); Empyreal Paradox is not among them; no completed section.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Abyssea
{
    public sealed class TheTruthBeckons : QuestScript
    {
        public TheTruthBeckons() : base(QuestLog.Abyssea, "THE_TRUTH_BECKONS") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/abyssea/The_Truth_Beckons";

        // the Lua's handleOnZoneIn: Prog 1, nothing returned
        static QuestReply HandleOnZoneIn(IQuestContext q)
        {
            q.Quest.SetStep(1);
            return null;
        }

        protected override void Build(QuestScriptBuilder b)
        {
            b.Steps(1);

            b.Reward(new QuestReward());

            b.State(QuestStatus.Accepted)
                .Npc(Zone.PortJeuno, Npc.PortJeuno.Joachim).OnTrigger(q =>
                {
                    if (q.Quest.Step == 1)
                    {
                        return QuestReply.ProgressEvent(327, 0, 0, q.Player.TraverserCap);
                    }
                    else
                    {
                        return QuestReply.ProgressEvent(326);
                    }
                })
                .OnEventFinish(Zone.PortJeuno, 327, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Player.AddQuest(QuestLog.Abyssea, "DAWN_OF_DEATH");
                    }
                })
                .OnZoneIn(Zone.AbysseaKonschtat, HandleOnZoneIn)
                .OnZoneIn(Zone.AbysseaLaTheine, HandleOnZoneIn)
                .OnZoneIn(Zone.AbysseaTahrongi, HandleOnZoneIn)
                .OnZoneIn(Zone.AbysseaAttohwa, HandleOnZoneIn)
                .OnZoneIn(Zone.AbysseaMisareaux, HandleOnZoneIn)
                .OnZoneIn(Zone.AbysseaVunkerl, HandleOnZoneIn)
                .OnZoneIn(Zone.AbysseaAltepa, HandleOnZoneIn)
                .OnZoneIn(Zone.AbysseaUleguerand, HandleOnZoneIn)
                .OnZoneIn(Zone.AbysseaGrauberg, HandleOnZoneIn);
        }
    }
}
