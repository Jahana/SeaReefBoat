// The Pickpocket (Log ID 0, Quest ID 3), a line-for-line port of scripts/quests/sandoria/The_Pickpocket.lua (quest rework
// B3b, San d'Oria group 7). Miene : !pos 0 -4 -81 232; Altiret : !pos 21 -4 -65 232; Esca : !pos -624 -51 278 100.
// Available at Prog 0: Miene's 502 (Prog 1). Prog 1 (still available), or accepted without the Gilt Glasses: 31
// bystanders in Port and Northern San d'Oria and West Ronfaure each say their PICKPOCKET_* line (Comittie's and
// Meinemelle's once per zone, Aurege's and Rodaillece's important once, Maurinne's a two-line sequence that turns her
// to 82 with the second line, then back to 115). Available at Prog 1: Miene's 554, Altiret's 547 (begun, Prog 0).
// Accepted: Altiret's 547; a trade of just one Gilt Glasses to him plays 550 (completed: Light Axe, fromTrade; fame +20;
// Pickpocket Pincher; then the glasses taken), anything else 551. Miene at Stage 0: 549 with a free slot (the Eagle
// Button, Stage 1), else 552; at Stage 1: 611 with a free slot and neither the button nor the glasses (the button
// again), else nothing. Esca: 123 with the glasses, else 120; a trade of just the button plays 121 (the Gilt Glasses,
// fromTrade; then the button taken), anything else 122. Completed: Altiret's 580, Esca's 123. No outside reference.
//
// quest:message(id) builds Message:new(id, nil), whose type defaults to Text: the same action quest:messageText(id)
// builds, so the bystanders' lines are QuestReply.MessageText(ZoneText). Maurinne's second line is
// PICKPOCKET_MAURINNE + 1 (no text name has that id): Message(Text.Id(...) + 1).WithFace(82), one sequence entry
// { text = id, face = 82 } as the Lua's.
// Kept as the Lua has it: 611's finish ignores giveItem's result; Miene at Stage 1 with the button or glasses held (or no
// free slot) returns nothing; 549, 121 and 550 change nothing when the give fails.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Sandoria
{
    public sealed class ThePickpocket : QuestScript
    {
        public ThePickpocket() : base(QuestLog.Sandoria, "THE_PICKPOCKET") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/sandoria/The_Pickpocket";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward
            {
                Fame = 20,
                FameArea = FameArea.Sandoria,
                Item = Item.LightAxe,
                ItemParams = new QuestItemParams { FromTrade = true },
                Title = Title.PickpocketPincher,
            });

            // Speaking with the little elvaan girl, Miene, will activate a cutscene. You will see a burglar steal
            // something from Altiret, one of the guards.
            b.State(QuestStatus.Available, when: q => q.Quest.Step == 0)
                .Npc(Zone.PortSanDoria, Npc.PortSanDoria.Miene).OnTrigger(QuestReply.ProgressEvent(502))
                .OnEventFinish(Zone.PortSanDoria, 502, q => q.Quest.SetStep(1));

            // Informational: You can talk to nearby guards and by-standers, some will report having seen the thief
            // allowing you to pick up the trail.
            b.State(QuestStatus.Available, when: q => q.Quest.Step == 1)
                .Or(QuestStatus.Accepted, when: q => !q.Player.HasItem(Item.GiltGlasses))
                .Npc(Zone.PortSanDoria, Npc.PortSanDoria.Answald).OnTrigger(QuestReply.MessageText(Text.PortSanDoria.PICKPOCKET_ANSWALD))
                .Npc(Zone.PortSanDoria, Npc.PortSanDoria.Artinien).OnTrigger(QuestReply.MessageText(Text.PortSanDoria.PICKPOCKET_ARTINIEN))
                .Npc(Zone.PortSanDoria, Npc.PortSanDoria.Avandale).OnTrigger(QuestReply.MessageText(Text.PortSanDoria.PICKPOCKET_AVANDALE))
                .Npc(Zone.PortSanDoria, Npc.PortSanDoria.Brifalien).OnTrigger(QuestReply.MessageText(Text.PortSanDoria.PICKPOCKET_BRIFALIEN))
                .Npc(Zone.PortSanDoria, Npc.PortSanDoria.Comittie).OnTrigger(QuestReply.MessageText(Text.PortSanDoria.PICKPOCKET_COMITTIE).OncePerZone())
                .Npc(Zone.PortSanDoria, Npc.PortSanDoria.Coribalgeant).OnTrigger(QuestReply.MessageText(Text.PortSanDoria.PICKPOCKET_CORIBALGEANT))
                .Npc(Zone.PortSanDoria, Npc.PortSanDoria.Crilde).OnTrigger(QuestReply.MessageText(Text.PortSanDoria.PICKPOCKET_CRILDE))
                .Npc(Zone.PortSanDoria, Npc.PortSanDoria.Eaugouint).OnTrigger(QuestReply.MessageText(Text.PortSanDoria.PICKPOCKET_EAUGOUINT))
                .Npc(Zone.PortSanDoria, Npc.PortSanDoria.Laucimercen).OnTrigger(QuestReply.MessageText(Text.PortSanDoria.PICKPOCKET_LAUCIMERCEN))
                .Npc(Zone.PortSanDoria, Npc.PortSanDoria.Marquie).OnTrigger(QuestReply.MessageText(Text.PortSanDoria.PICKPOCKET_MARQUIE))
                .Npc(Zone.PortSanDoria, Npc.PortSanDoria.Maunadolace).OnTrigger(QuestReply.MessageText(Text.PortSanDoria.PICKPOCKET_MAUNADOLACE))
                .Npc(Zone.PortSanDoria, Npc.PortSanDoria.Meinemelle).OnTrigger(QuestReply.MessageText(Text.PortSanDoria.PICKPOCKET_MEINEMELLE).OncePerZone())
                .Npc(Zone.PortSanDoria, Npc.PortSanDoria.Meuxtajean).OnTrigger(QuestReply.MessageText(Text.PortSanDoria.PICKPOCKET_MEUXTAJEAN))
                .Npc(Zone.PortSanDoria, Npc.PortSanDoria.Noquerelle).OnTrigger(QuestReply.MessageText(Text.PortSanDoria.PICKPOCKET_NOQUERELLE))
                .Npc(Zone.PortSanDoria, Npc.PortSanDoria.Parcarin).OnTrigger(QuestReply.MessageText(Text.PortSanDoria.PICKPOCKET_PARCARIN))
                .Npc(Zone.PortSanDoria, Npc.PortSanDoria.Rielle).OnTrigger(QuestReply.MessageText(Text.PortSanDoria.PICKPOCKET_RIELLE))
                .Npc(Zone.PortSanDoria, Npc.PortSanDoria.Sheridan).OnTrigger(QuestReply.MessageText(Text.PortSanDoria.PICKPOCKET_SHERIDAN))
                .Npc(Zone.PortSanDoria, Npc.PortSanDoria.Solgierte).OnTrigger(QuestReply.MessageText(Text.PortSanDoria.PICKPOCKET_SOLGIERTE))
                .Npc(Zone.NorthernSanDoria, Npc.NorthernSanDoria.Aurege).OnTrigger(QuestReply.MessageText(Text.NorthernSanDoria.PICKPOCKET_AUREGE).ImportantOnce())
                .Npc(Zone.NorthernSanDoria, Npc.NorthernSanDoria.Gilipese).OnTrigger(QuestReply.MessageText(Text.NorthernSanDoria.PICKPOCKET_GILIPESE))
                .Npc(Zone.NorthernSanDoria, Npc.NorthernSanDoria.Guilberdrier).OnTrigger(QuestReply.MessageText(Text.NorthernSanDoria.PICKPOCKET_GUILBERDRIER))
                // { text = PICKPOCKET_MAURINNE, wait = 1000 }, { text = PICKPOCKET_MAURINNE + 1, face = 82, wait = 2000 }, { face = 115 }
                .Npc(Zone.NorthernSanDoria, Npc.NorthernSanDoria.Maurinne).OnTrigger(q => QuestReply.Sequence(
                    QuestReply.MessageText(Text.NorthernSanDoria.PICKPOCKET_MAURINNE),
                    QuestReply.Wait(1000),
                    QuestReply.Message(q.Text.Id(Text.NorthernSanDoria.PICKPOCKET_MAURINNE) + 1).WithFace(82),
                    QuestReply.Wait(2000),
                    QuestReply.Face(115)))
                .Npc(Zone.NorthernSanDoria, Npc.NorthernSanDoria.Pepigort).OnTrigger(QuestReply.MessageText(Text.NorthernSanDoria.PICKPOCKET_PEPIGORT))
                .Npc(Zone.NorthernSanDoria, Npc.NorthernSanDoria.Rodaillece).OnTrigger(QuestReply.MessageText(Text.NorthernSanDoria.PICKPOCKET_RODAILLECE).ImportantOnce())
                .Npc(Zone.WestRonfaure, Npc.WestRonfaure.Aaveleon).OnTrigger(QuestReply.MessageText(Text.WestRonfaure.PICKPOCKET_AAVELEON))
                .Npc(Zone.WestRonfaure, Npc.WestRonfaure.Adalefont).OnTrigger(QuestReply.MessageText(Text.WestRonfaure.PICKPOCKET_ADALEFONT))
                .Npc(Zone.WestRonfaure, Npc.WestRonfaure.Colmaie).OnTrigger(QuestReply.MessageText(Text.WestRonfaure.PICKPOCKET_COLMAIE))
                .Npc(Zone.WestRonfaure, Npc.WestRonfaure.Gachemage).OnTrigger(QuestReply.MessageText(Text.WestRonfaure.PICKPOCKET_GACHEMAGE))
                .Npc(Zone.WestRonfaure, Npc.WestRonfaure.Laillera).OnTrigger(QuestReply.MessageText(Text.WestRonfaure.PICKPOCKET_LAILLERA))
                .Npc(Zone.WestRonfaure, Npc.WestRonfaure.Palcomondau).OnTrigger(QuestReply.MessageText(Text.WestRonfaure.PICKPOCKET_PALCOMONDAU))
                .Npc(Zone.WestRonfaure, Npc.WestRonfaure.Zovriace).OnTrigger(QuestReply.MessageText(Text.WestRonfaure.PICKPOCKET_ZOVRIACE));

            // After the cutscene ends, speak with Altiret. He asks you to retrieve his wife's glasses for him, since he
            // cannot leave his post.
            b.State(QuestStatus.Available, when: q => q.Quest.Step == 1)
                .Npc(Zone.PortSanDoria, Npc.PortSanDoria.Miene).OnTrigger(QuestReply.Event(554))
                .Npc(Zone.PortSanDoria, Npc.PortSanDoria.Altiret).OnTrigger(QuestReply.ProgressEvent(547))
                .OnEventFinish(Zone.PortSanDoria, 547, q =>
                {
                    q.Quest.Begin();
                    q.Quest.SetStep(0);
                });

            // Speak to Miene again to get a quest item Eagle Button. Travel to (F-6) in West Ronfaure. You will find
            // Esca in the tower there. Speak with her, and she will deny any accusation, demanding proof. Trade Esca the
            // Eagle Button and she will admit her guilt. She then gives you the Gilt Glasses. Return to Port San d'Oria
            // and trade the Gilt Glasses to Altiret for your reward.
            b.State(QuestStatus.Accepted)
                .Npc(Zone.PortSanDoria, Npc.PortSanDoria.Altiret)
                    .OnTrigger(QuestReply.Event(547))
                    .OnTrade(q =>
                    {
                        if (q.Trade.Matches((Item.GiltGlasses, 1)))
                        {
                            return QuestReply.ProgressEvent(550);
                        }
                        else
                        {
                            return QuestReply.Event(551);
                        }
                    })
                .Npc(Zone.PortSanDoria, Npc.PortSanDoria.Miene).OnTrigger(q =>
                {
                    int stage = q.Quest.Var("Stage");

                    if (stage == 0)
                    {
                        if (q.Player.FreeSlotCount > 0)
                        {
                            return QuestReply.ProgressEvent(549);
                        }
                        else
                        {
                            return QuestReply.Event(552);
                        }
                    }
                    else
                    {
                        if (q.Player.FreeSlotCount > 0 &&
                            !q.Player.HasItem(Item.EagleButton) &&
                            !q.Player.HasItem(Item.GiltGlasses))
                        {
                            // Reaquire the button
                            return QuestReply.ProgressEvent(611);
                        }
                    }
                    return null;
                })
                .OnEventFinish(Zone.PortSanDoria, 549, q =>
                {
                    if (q.Player.GiveItem(Item.EagleButton))
                    {
                        q.Quest.SetVar("Stage", 1);
                    }
                })
                .OnEventFinish(Zone.PortSanDoria, 611, q => q.Player.GiveItem(Item.EagleButton))
                .OnEventFinish(Zone.PortSanDoria, 550, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Trade.Complete();
                    }
                })
                .Npc(Zone.WestRonfaure, Npc.WestRonfaure.Esca)
                    .OnTrigger(q =>
                    {
                        if (q.Player.HasItem(Item.GiltGlasses))
                        {
                            return QuestReply.Event(123);
                        }
                        else
                        {
                            return QuestReply.Event(120);
                        }
                    })
                    .OnTrade(q =>
                    {
                        if (q.Trade.Matches((Item.EagleButton, 1)))
                        {
                            return QuestReply.ProgressEvent(121);
                        }

                        return QuestReply.Event(122);
                    })
                .OnEventFinish(Zone.WestRonfaure, 121, q =>
                {
                    // npcUtil.giveItem(player, xi.item.GILT_GLASSES, { fromTrade = true })
                    if (q.Player.GiveItem(Item.GiltGlasses, silent: false, fromTrade: true))
                    {
                        q.Trade.Complete();
                    }
                });

            // Section: Completed quest
            b.State(QuestStatus.Completed)
                .Npc(Zone.PortSanDoria, Npc.PortSanDoria.Altiret).OnTrigger(QuestReply.Event(580))
                .Npc(Zone.WestRonfaure, Npc.WestRonfaure.Esca).OnTrigger(QuestReply.Event(123));
        }
    }
}
