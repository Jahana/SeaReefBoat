// Face of the Future (Log ID 7, Quest ID 61), a line-for-line port of
// scripts/quests/crystalWar/WOTG_SAN_12_Face_of_the_Future.lua (quest rework B3b Crystal War, draft). Jugner Forest:
// Metallic Hodgepodge !pos -285.493 -7.819 -163.707 (default NOTHING_OUT_OF_ORDINARY). Ghelsba Outpost: Clandestine
// Marking !pos 55.474 -2.046 36.147 (default `{ ID.text.NOTHING_OUT_OF_ORDINARY }`, no key). Yughott Grotto: Scrape
// Mark !pos -7.817 0.132 -62.194 (no default). Batallia Downs: Cavernous Maw !pos -48 0.1 435 (its script:
// xi.maws.onTrigger). Everbloom Hollow: the instance (not implemented). Accepted only (Chasing Shadows' 117 adds it):
// the Jugner Forest zone-in from Batallia Downs at Prog 0: 44 (-> Prog 1). Metallic Hodgepodge: Prog 1: 45 (-> 2). The
// Ghelsba Outpost zone-in at Prog 2 (any zone): 57 (-> 3). Clandestine Marking: no Orcish Infiltration Kit: the kit (a
// key item reply). Scrape Mark: Prog 3: 1 (-> 4). Everbloom Hollow's 10000 finish: Prog 5, setPos into Yughott Grotto.
// The Yughott Grotto zone-in at Prog 5 (any zone): 2, whose finish sets Prog 6 and the char down in Ghelsba Outpost.
// Cavernous Maw: the Batallia Downs maw unlocked and Prog 6: 504, whose finish sets Prog 7 and the char down in
// Batallia Downs. The Batallia Downs zone-in: Prog 7: 505 (-> 8, setPos into Xarcabard [S]); Prog 9: 508 (-> 10, setPos
// into Batallia Downs); Prog 10: 507, whose finish completes it (a Griffon Ring, Fangmonger Forestaller) and sets the
// char down in Batallia Downs [S]. The Xarcabard [S] zone-in at Prog 8: 40 (-> 9, setPos into Batallia Downs). Kept as
// the Lua has it: the kit is given at any Prog and never taken; Prog 4 waits on the instance (the Lua's NOTE: Event
// 103, params 0, 0, 61 registers it); every zone-in but Jugner Forest's plays from any zone; the setPos spots the Lua's
// TODOs call assumed (505, 508); no available or completed section.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.CrystalWar
{
    public sealed class FaceOfTheFuture : QuestScript
    {
        public FaceOfTheFuture() : base(QuestLog.CrystalWar, "FACE_OF_THE_FUTURE") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/crystalWar/WOTG_SAN_12_Face_of_the_Future";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Steps(1, 2, 3, 4, 5, 6, 7, 8, 9, 10);

            b.Reward(new QuestReward
            {
                Item = Item.GriffonRing,
                Title = Title.FangmongerForestaller,
            });

            b.State(QuestStatus.Accepted)
                .Npc(Zone.JugnerForest, Npc.JugnerForest.MetallicHodgepodge).OnTrigger(q =>
                {
                    if (q.Quest.Step == 1)
                    {
                        return QuestReply.ProgressEvent(45);
                    }
                    return null;
                })
                .OnZoneIn(Zone.JugnerForest, q =>
                {
                    if (q.Player.PreviousZone == Zone.BatalliaDowns &&
                        q.Quest.Step == 0)
                    {
                        return QuestReply.Event(44);
                    }
                    return null;
                })
                .OnEventFinish(Zone.JugnerForest, 44, q =>
                {
                    q.Quest.SetStep(1);
                })
                .OnEventFinish(Zone.JugnerForest, 45, q =>
                {
                    q.Quest.SetStep(2);
                })
                .Npc(Zone.GhelsbaOutpost, Npc.GhelsbaOutpost.ClandestineMarking).OnTrigger(q =>
                {
                    if (!q.Player.HasKeyItem(KeyItem.OrcishInfiltrationKit))
                    {
                        return QuestReply.KeyItem(KeyItem.OrcishInfiltrationKit);
                    }
                    return null;
                })
                .OnZoneIn(Zone.GhelsbaOutpost, q =>
                {
                    if (q.Quest.Step == 2)
                    {
                        return QuestReply.Event(57);
                    }
                    return null;
                })
                .OnEventFinish(Zone.GhelsbaOutpost, 57, q =>
                {
                    q.Quest.SetStep(3);
                })
                .Npc(Zone.YughottGrotto, Npc.YughottGrotto.ScrapeMark).OnTrigger(q =>
                {
                    // the Lua's NOTE: Prog 4 is the instance's entry (Event 103, params 0, 0, 61), not here
                    if (q.Quest.Step == 3)
                    {
                        return QuestReply.ProgressEvent(1);
                    }
                    return null;
                })
                .OnZoneIn(Zone.YughottGrotto, q =>
                {
                    if (q.Quest.Step == 5)
                    {
                        return QuestReply.Event(2);
                    }
                    return null;
                })
                .OnEventFinish(Zone.YughottGrotto, 1, q =>
                {
                    q.Quest.SetStep(4);
                })
                .OnEventFinish(Zone.YughottGrotto, 2, q =>
                {
                    q.Quest.SetStep(6);
                    q.Player.SetPos(13.29, -0.104, -59.417, 253, Zone.GhelsbaOutpost);
                })
                .OnEventFinish(Zone.EverbloomHollow, 10000, q =>
                {
                    q.Quest.SetStep(5);
                    q.Player.SetPos(-184.678, -0.266, -112.093, 101, Zone.YughottGrotto);
                })
                .Npc(Zone.BatalliaDowns, Npc.BatalliaDowns.CavernousMaw).OnTrigger(q =>
                {
                    // Lua: xi.maws.hasUnlockedMaw(player, BATALLIA_DOWNS)
                    if (q.Player.HasUnlockedMaw(Zone.BatalliaDowns) &&
                        q.Quest.Step == 6)
                    {
                        return QuestReply.ProgressEvent(504);
                    }
                    return null;
                })
                .OnZoneIn(Zone.BatalliaDowns, q =>
                {
                    int questProgress = q.Quest.Step;

                    if (questProgress == 7)
                    {
                        return QuestReply.Event(505);
                    }
                    else if (questProgress == 9)
                    {
                        return QuestReply.Event(508);
                    }
                    else if (questProgress == 10)
                    {
                        return QuestReply.Event(507);
                    }
                    return null;
                })
                .OnEventFinish(Zone.BatalliaDowns, 504, q =>
                {
                    q.Quest.SetStep(7);
                    q.Player.SetPos(-47.108, 0.153, 437.1, 65, Zone.BatalliaDowns);
                })
                .OnEventFinish(Zone.BatalliaDowns, 505, q =>
                {
                    q.Quest.SetStep(8);

                    // the Lua's TODO: the spot is assumed from earlier captures
                    q.Player.SetPos(-105.798, -25.522, -53.499, 176, Zone.XarcabardS);
                })
                .OnEventFinish(Zone.BatalliaDowns, 507, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Player.SetPos(-49.368, 0.268, 436.691, 252, Zone.BatalliaDownsS);
                    }
                })
                .OnEventFinish(Zone.BatalliaDowns, 508, q =>
                {
                    // the Lua's TODO: the spot is assumed from earlier captures
                    q.Quest.SetStep(10);
                    q.Player.SetPos(-47.108, 0.154, 437.1, 65, Zone.BatalliaDowns);
                })
                .OnZoneIn(Zone.XarcabardS, q =>
                {
                    if (q.Quest.Step == 8)
                    {
                        return QuestReply.Event(40);
                    }
                    return null;
                })
                .OnEventFinish(Zone.XarcabardS, 40, q =>
                {
                    q.Quest.SetStep(9);
                    q.Player.SetPos(-47.108, 0.154, 437.1, 65, Zone.BatalliaDowns);
                });
        }
    }
}
