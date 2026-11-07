// Go! Go! Gobmuffin! (Log ID 4, Quest ID 69), a line-for-line port of scripts/quests/otherAreas/Go_Go_Gobmuffin.lua
// (quest rework B3b, Other Areas group 7). Tavnazian Safehold: Epinolle !pos 81.728 -33.970 66.351 (no script; default
// 230); Riverne - Site #B01: the Spatial Displacement of event 17 (DISPLACEMENT_OFFSET + 15; its script's 17 finish
// does nothing) and the three NMs Spell Spitter Spilospok (ID.mob.SPELL_SPITTER_SPILUSPOK), Book Browser Bokabraq and
// Chemical Cook Chemachiq (the two ids after it).
// Available once CoP's Sheltering Doubt is completed: Epinolle 232, whose finish begins the quest. Accepted: Epinolle
// at Prog 1: 234, whose finish runs quest:complete (2000 exp, 2000 gil, the Map of Cape Riverne); else 233 (event).
// Riverne #B01: the 17 finish at Prog 0 with none of the three up: the three spawned and claimed. Each NM's death at
// Prog 0: its bit (its id less the first's) set in the char's local GobmuffinNM; all three bits: Prog 1, else the mask
// kept (a zone change clears it). Completed: Epinolle 235 (event, replaceDefault).
// Kept as the Lua has it: at seven bits the local var is not written (it stays at the two-bit value); a death of a mob
// of those names whose id is not one of the three would hand utils.mask.setBit a nil bit (a Lua error): the C# returns
// there (not reachable: Riverne - Site #B01 places one mob of each name, the three ids; XI.Verify.Quests `oa g7` checks
// it); 234's quest:complete is not read (no item: it cannot fail).
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.OtherAreas
{
    public sealed class GoGoGobmuffin : QuestScript
    {
        public GoGoGobmuffin() : base(QuestLog.OtherAreas, "GO_GO_GOBMUFFIN") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/otherAreas/Go_Go_Gobmuffin";

        // nmStatus: the dead NM's bit in the char's local GobmuffinNM; all three: Prog 1
        static void NmStatus(IQuestContext q)
        {
            int nmBitmask = q.Player.LocalVar("GobmuffinNM");
            int bitNumber = q.Mob.Id - q.Player.MobIdOf(Mob.RiverneSiteB01.SPELL_SPITTER_SPILUSPOK);
            if (bitNumber < 0 || bitNumber > 2)
            {
                return; // nmBitTable[mob:getID()] is nil: utils.mask.setBit errors in the Lua
            }
            nmBitmask = nmBitmask | 1 << bitNumber;

            if (nmBitmask == 7) // All 3 bits are true.
            {
                q.Quest.SetStep(1);
            }
            else
            {
                q.Player.SetLocalVar("GobmuffinNM", nmBitmask); // Resets upon zoning causing the player to need to refight the NMs
            }
        }

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward
            {
                Exp = 2000,
                Gil = 2000,
                KeyItem = KeyItem.MapOfCapeRiverne,
            });
            b.Steps(1);

            b.State(QuestStatus.Available, when: q => q.Player.HasCompletedMission(MissionLog.Cop, Mission.Cop.ShelteringDoubt))
                .Npc(Zone.TavnazianSafehold, Npc.TavnazianSafehold.Epinolle).OnTrigger(QuestReply.ProgressEvent(232))
                .OnEventFinish(Zone.TavnazianSafehold, 232, q =>
                {
                    q.Quest.Begin();
                });

            b.State(QuestStatus.Accepted)
                .Npc(Zone.TavnazianSafehold, Npc.TavnazianSafehold.Epinolle).OnTrigger(q =>
                {
                    if (q.Quest.Step == 1)
                    {
                        return QuestReply.ProgressEvent(234);
                    }
                    else
                    {
                        return QuestReply.Event(233);
                    }
                })
                .OnEventFinish(Zone.TavnazianSafehold, 234, q =>
                {
                    q.Quest.Complete();
                })
                .Npc(Zone.RiverneSiteB01, Npc.RiverneSiteB01.BookBrowserBokabraq).OnMobDeath(q =>
                {
                    if (q.Quest.Step == 0)
                    {
                        NmStatus(q);
                    }
                })
                .Npc(Zone.RiverneSiteB01, Npc.RiverneSiteB01.ChemicalCookChemachiq).OnMobDeath(q =>
                {
                    if (q.Quest.Step == 0)
                    {
                        NmStatus(q);
                    }
                })
                .Npc(Zone.RiverneSiteB01, Npc.RiverneSiteB01.SpellSpitterSpilospok).OnMobDeath(q =>
                {
                    if (q.Quest.Step == 0)
                    {
                        NmStatus(q);
                    }
                })
                .OnEventFinish(Zone.RiverneSiteB01, 17, q =>
                {
                    if (q.Quest.Step == 0 &&
                        !q.Player.MobIsSpawned(Mob.RiverneSiteB01.SPELL_SPITTER_SPILUSPOK) &&
                        !q.Player.MobIsSpawned(Mob.RiverneSiteB01.SPELL_SPITTER_SPILUSPOK, 1) &&
                        !q.Player.MobIsSpawned(Mob.RiverneSiteB01.SPELL_SPITTER_SPILUSPOK, 2))
                    {
                        for (int i = 0; i <= 2; i++)
                        {
                            q.Player.SpawnMob(Mob.RiverneSiteB01.SPELL_SPITTER_SPILUSPOK, i, claim: true);
                        }
                    }
                });

            b.State(QuestStatus.Completed)
                .Npc(Zone.TavnazianSafehold, Npc.TavnazianSafehold.Epinolle).OnTrigger(QuestReply.Event(235).ReplaceDefault());
        }
    }
}
