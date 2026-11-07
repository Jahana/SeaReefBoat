// Chasing Dreams (Log ID 5, Quest ID 199), a line-for-line port of scripts/quests/outlands/Chasing_Dreams.lua (quest
// rework B3b Outlands, group 3). Rabao: Rudolfo !pos 119.6 8 51 (no script; default 125), Zoriboh !pos -44.5 8 81.4 (no
// script; default 127). Norg: Sohyon !pos 47.3 -6.3 13.9 (no script; default 212), Washu !pos 51 -6.3 15.5 (no script;
// default 80), Gimb !pos -5 -1 -44 (his script: always 69, SahaginKeyProgress 1). Korroloka Tunnel: the four clams _4t0 ..
// _4t3 (no scripts, no defaults). Port Bastok: Kagetora !pos -94.6 -2.3 30.5 (his script: Twenty in Pirate Years' 261;
// default 23), Patient_Wheel !pos -108 4.9 52.6 (no script; default 325). Selbina: Abelard !pos -52 -11.3 -13 (no
// script; default 1130). Lufaise Meadows: the zone-in.
// Available (no other guard): Rudolfo 117 begins it. Accepted up to Prog 3: Zoriboh 119 at Prog 0 (Prog 1), else 120;
// Sohyon 209 at Prog 1 (Prog 2), else 213; Washu 221 at Prog 2 (Washu's Flask, Prog 3); each clam with the flask and its
// Option bit (1 .. 4) clear sets the bit and fills the flask (FILL_FLASK; with all four bits, Option 30: the flask taken,
// FLASK_FULL, the Flask of Clam Water, Prog 4; else STILL_LIGHT), no action; else CLAM_EMPTY. From Prog 4: Sohyon 210 with
// the clam water and no Storeroom Key (the water taken, the key); Gimb 211 with the key (0, Pinch of Prism Powder: the key
// taken, Prog 5); Kagetora 322 at Prog 5 (0, Eastern Gem: Prog 6), else 324; Patient_Wheel: five Eastern Gems alone at
// Prog 6: 323 (confirmed, Prog 7); his talk 326 (0, Eastern Gem) at Prog 6, 327 above it; Abelard 1108 at Prog 7 (Prog 8);
// Lufaise Meadows' zone-in at Prog 8: 4 (Prog 9); Zoriboh 121 at Prog 9: quest:complete (the Venerer Ring, 4000 gil), then
// San d'Oria and Bastok fame 10.
// Kept as the Lua has it: the clams answer CLAM_EMPTY at any Prog up to 3 without the flask or with the bit set; from
// Prog 4 they have no section (no default either); the flask is filled whichever clam comes last; Zoriboh is nil between
// Prog 4 and 8.
// Shared NPCs (#kinds): Washu (Stop Your Whining, Yomi Okuri), Kagetora (Ayame and Kaede, Faded Promises; his script),
// Patient_Wheel (Lure of the Wildcat), Abelard (An Explorer's Footsteps, Signed in Blood, RoV 1-04), Gimb (his script).
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Outlands
{
    public sealed class ChasingDreams : QuestScript
    {
        public ChasingDreams() : base(QuestLog.Outlands, "CHASING_DREAMS") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/outlands/Chasing_Dreams";

        // handleFlask
        static void HandleFlask(IQuestContext q)
        {
            q.Text.Special(Text.KorrolokaTunnel.FILL_FLASK, KeyItem.WashusFlask);

            if (q.Quest.Var("Option") == 30)
            {
                q.Player.DelKeyItem(KeyItem.WashusFlask);
                q.Text.Special(Text.KorrolokaTunnel.FLASK_FULL, KeyItem.WashusFlask);
                q.Player.GiveKeyItem(KeyItem.FlaskOfClamWater);
                q.Quest.SetStep(4);
            }
            else
            {
                q.Text.Special(Text.KorrolokaTunnel.STILL_LIGHT);
            }
        }

        // a clam (_4t0 .. _4t3): its Option bit
        static QuestReply Clam(IQuestContext q, int bit)
        {
            if (!q.Quest.IsVarBitsSet("Option", bit) &&
                q.Player.HasKeyItem(KeyItem.WashusFlask))
            {
                q.Quest.SetVarBit("Option", bit);
                HandleFlask(q);
                return QuestReply.NoAction();
            }
            else
            {
                return QuestReply.MessageSpecial(Text.KorrolokaTunnel.CLAM_EMPTY, KeyItem.WashusFlask);
            }
        }

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward { Item = Item.VenererRing, Gil = 4000 });
            b.Steps(1, 2, 3, 4, 5, 6, 7, 8, 9);

            // TODO (Lua): There is a pre-req to this quest as confirmed in captures, however the exact pre-req is unknown.
            b.State(QuestStatus.Available)
                .Npc(Zone.Rabao, Npc.Rabao.Rudolfo).OnTrigger(QuestReply.ProgressEvent(117))
                .OnEventFinish(Zone.Rabao, 117, q =>
                {
                    q.Quest.Begin();
                });

            // Quest stage preceding and including getting clam water
            b.State(QuestStatus.Accepted, when: q => q.Quest.Step <= 3)
                .Npc(Zone.Rabao, Npc.Rabao.Zoriboh).OnTrigger(q =>
                {
                    if (q.Quest.Step == 0)
                    {
                        return QuestReply.ProgressEvent(119);
                    }
                    else
                    {
                        return QuestReply.Event(120);
                    }
                })
                .OnEventFinish(Zone.Rabao, 119, q =>
                {
                    q.Quest.SetStep(1);
                })
                .Npc(Zone.Norg, Npc.Norg.Sohyon).OnTrigger(q =>
                {
                    if (q.Quest.Step == 1)
                    {
                        return QuestReply.ProgressEvent(209);
                    }
                    else
                    {
                        // Additional Dialogue
                        return QuestReply.Event(213);
                    }
                })
                .Npc(Zone.Norg, Npc.Norg.Washu).OnTrigger(q =>
                {
                    if (q.Quest.Step == 2)
                    {
                        return QuestReply.ProgressEvent(221);
                    }
                    return null;
                })
                .OnEventFinish(Zone.Norg, 209, q =>
                {
                    q.Quest.SetStep(2);
                })
                .OnEventFinish(Zone.Norg, 221, q =>
                {
                    q.Player.GiveKeyItem(KeyItem.WashusFlask); // WASHU'S FLASK Key Item = 623
                    q.Quest.SetStep(3);
                })
                .Npc(Zone.KorrolokaTunnel, Npc.KorrolokaTunnel._4t0).OnTrigger(q => Clam(q, 1))
                .Npc(Zone.KorrolokaTunnel, Npc.KorrolokaTunnel._4t1).OnTrigger(q => Clam(q, 2))
                .Npc(Zone.KorrolokaTunnel, Npc.KorrolokaTunnel._4t2).OnTrigger(q => Clam(q, 3))
                .Npc(Zone.KorrolokaTunnel, Npc.KorrolokaTunnel._4t3).OnTrigger(q => Clam(q, 4));

            // Quest section suceeding having gathered the clam water
            b.State(QuestStatus.Accepted, when: q => q.Quest.Step >= 4)
                .Npc(Zone.Norg, Npc.Norg.Sohyon).OnTrigger(q =>
                {
                    if (q.Player.HasKeyItem(KeyItem.FlaskOfClamWater) &&
                        !q.Player.HasKeyItem(KeyItem.StoreroomKey))
                    {
                        return QuestReply.ProgressEvent(210);
                    }
                    return null;
                })
                .Npc(Zone.Norg, Npc.Norg.Gimb).OnTrigger(q =>
                {
                    if (q.Player.HasKeyItem(KeyItem.StoreroomKey))
                    {
                        return QuestReply.ProgressEvent(211, 0, Item.PinchOfPrismPowder);
                    }
                    return null;
                })
                .OnEventFinish(Zone.Norg, 210, q =>
                {
                    q.Player.DelKeyItem(KeyItem.FlaskOfClamWater);
                    q.Player.GiveKeyItem(KeyItem.StoreroomKey);
                })
                .OnEventFinish(Zone.Norg, 211, q =>
                {
                    q.Player.DelKeyItem(KeyItem.StoreroomKey);
                    q.Quest.SetStep(5);
                })
                .Npc(Zone.PortBastok, Npc.PortBastok.Kagetora).OnTrigger(q =>
                {
                    if (q.Quest.Step == 5)
                    {
                        return QuestReply.ProgressEvent(322, 0, Item.EasternGem);
                    }
                    else
                    {
                        // Additional Dialogue
                        return QuestReply.Event(324);
                    }
                })
                .Npc(Zone.PortBastok, Npc.PortBastok.PatientWheel)
                    .OnTrade(q =>
                    {
                        if (q.Quest.Step == 6 &&
                            q.Trade.HasExactly((Item.EasternGem, 5)))
                        {
                            return QuestReply.ProgressEvent(323);
                        }
                        return null;
                    })
                    .OnTrigger(q =>
                    {
                        if (q.Quest.Step == 6)
                        {
                            // Additional Dialogue
                            return QuestReply.Event(326, 0, Item.EasternGem);
                        }
                        else if (q.Quest.Step > 6)
                        {
                            // Additional Dialogue
                            return QuestReply.Event(327);
                        }
                        return null;
                    })
                .OnEventFinish(Zone.PortBastok, 322, q =>
                {
                    q.Quest.SetStep(6);
                })
                .OnEventFinish(Zone.PortBastok, 323, q =>
                {
                    q.Trade.Confirm();
                    q.Quest.SetStep(7);
                })
                .Npc(Zone.Selbina, Npc.Selbina.Abelard).OnTrigger(q =>
                {
                    if (q.Quest.Step == 7)
                    {
                        return QuestReply.ProgressEvent(1108);
                    }
                    return null;
                })
                .OnEventFinish(Zone.Selbina, 1108, q =>
                {
                    q.Quest.SetStep(8);
                })
                .OnZoneIn(Zone.LufaiseMeadows, q =>
                {
                    if (q.Quest.Step == 8)
                    {
                        return QuestReply.Event(4);
                    }
                    return null;
                })
                .OnEventFinish(Zone.LufaiseMeadows, 4, q =>
                {
                    q.Quest.SetStep(9);
                })
                .Npc(Zone.Rabao, Npc.Rabao.Zoriboh).OnTrigger(q =>
                {
                    if (q.Quest.Step == 9)
                    {
                        return QuestReply.ProgressEvent(121);
                    }
                    return null;
                })
                .OnEventFinish(Zone.Rabao, 121, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Player.AddFame(FameArea.Sandoria, 10);
                        q.Player.AddFame(FameArea.Bastok, 10);
                    }
                });
        }
    }
}
