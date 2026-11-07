// XI.Verify.Quests -- quest rework B3b San d'Oria group 16 (call-only: Trial Size Trial by Ice,
// Over the Hills and Far Away, Trust: San d'Oria): each definition validates clean and is call-only; the replies'
// kinds, which no flow can show (the bridge renders an Event, a ProgressEvent and a PriorityEvent alike, as
// player:startEvent; NoAction as nothing at all). Each pin is the Lua's: player:startEvent(id, args...), nothing
// started, or no arm (null). The finishes' calls in the scripts' order: 733, 734, 737, 32001, Antreneau's 725, the
// ???'s 10 at the midpoint rates, Gondebaud's 3500 / 3504, Excenmille's 893 / 895 / 897. Player.MissionStatus(log, pos)
// reads the status word's 4-bit field at pos, as the engine does.
using System;
using System.Linq;
using XI.Quests;
using XI.Quests.Authoring;
using QC = XI.QuestContent;

internal static partial class Program
{
    static void RunSandoriaGroup16()
    {
        Console.WriteLine("== quest rework B3b San d'Oria group 16: Trial Size Trial by Ice, Over the Hills and Far " +
                          "Away, Trust: San d'Oria (call-only) ==");
        var ts = new QC.Sandoria.TrialSizeTrialByIce().Compile();
        var oh = new QC.Sandoria.OverTheHillsAndFarAway().Compile();
        var tr = new QC.Sandoria.TrustSandoria().Compile();
        var defs = new[]
        {
            ("Trial Size Trial by Ice", ts), ("Over the Hills and Far Away", oh), ("Trust: San d'Oria", tr),
        };
        foreach (var (name, def) in defs)
        {
            Check($"sandoria g16:{name} clean", "", string.Join(" | ", QuestScriptValidator.Validate(def)));
            Check($"sandoria g16:{name} is call-only", "True", def.CallOnly.ToString());
        }

        int cop = QC.MissionLog.Cop;
        var mf = new Fake();
        mf.MissionStatuses[cop] = 14 << 8 | 3;
        Check("sandoria g16:Player.MissionStatus(log, pos) is the status word's 4-bit field at pos (fake)", "3|0|14|0",
              $"{mf.MissionStatus(cop, 0)}|{mf.MissionStatus(cop, 1)}|" +
              $"{mf.MissionStatus(cop, 2)}|{mf.MissionStatus(cop, 8)}");

        var lines = new System.Collections.Generic.List<string>();
        void L(string key, string v) => lines.Add(key + "=" + v);
        ushort North = QC.Zone.NorthernSanDoria, South = QC.Zone.SouthernSanDoria, Port = QC.Zone.PortSanDoria;
        ushort Ule = QC.Zone.UleguerandRange;
        const QuestHandlerKind Tr = QuestHandlerKind.OnTrigger, Td = QuestHandlerKind.OnTrade;
        int smn = QC.Job.Smn, fork = QC.Item.MiniTuningForkOfIce, hotrok = QC.Item.MoblinHotrok;
        int sandoria = QC.FameArea.Sandoria;
        // the reply, then what the handler did on the way
        string R(QuestScriptDefinition def, QuestHandlerKind kind, ushort zone, string npc, Fake f)
        {
            var hs = AU3Up(def, f).Where(x => x.Kind == kind && x.Zone == zone && x.Entity == npc).ToList();
            string r = hs.Count == 0 ? "null" : AU1Describe(hs.Single(), f);
            return f.Calls.Count == 0 ? r : r + "+" + string.Join("+", f.Calls);
        }
        Fake Smn(QuestStatus st, int fame = 2)
        {
            var f = new Fake { Status = st, MainJob = smn, MainLevel = 20 };
            f.Fame[sandoria] = fame;
            return f;
        }
        Fake Give(Fake f, int item)
        {
            f.TradeItems[item] = 1;
            f.TradeSlots.Add(item);
            return f;
        }
        string Castilchat = QC.Npc.NorthernSanDoria.Castilchat;
        L("ts offer", R(ts, Tr, North, Castilchat, Smn(QuestStatus.Available)));
        L("ts fame 1", R(ts, Tr, North, Castilchat, Smn(QuestStatus.Available, 1)));
        var held = Smn(QuestStatus.Accepted);
        held.Items.Add(fork);
        L("ts held", R(ts, Tr, North, Castilchat, held));
        L("ts lost", R(ts, Tr, North, Castilchat, Smn(QuestStatus.Accepted)));
        L("ts completed", R(ts, Tr, North, Castilchat, Smn(QuestStatus.Completed)));
        L("ts trade", R(ts, Td, North, Castilchat, Give(Smn(QuestStatus.Accepted), fork)));

        Fake Ant(QuestStatus st)
        {
            var f = new Fake { Status = st };
            f.QuestStatuses["Sandoria/A_TASTE_FOR_MEAT"] = QuestStatus.Completed;
            f.QuestStatuses["Sandoria/THE_MEDICINE_WOMAN"] = QuestStatus.Completed;
            f.Fame[sandoria] = 8;
            f.CharVars["DiaryPage"] = 4;
            return f;
        }
        string Antreneau = QC.Npc.PortSanDoria.Antreneau, Qm = QC.Npc.UleguerandRange.qmmoblinhotrok;
        L("oh 725", R(oh, Tr, Port, Antreneau, Ant(QuestStatus.Available)));
        L("oh 726", R(oh, Tr, Port, Antreneau, Ant(QuestStatus.Accepted)));
        L("oh 727", R(oh, Tr, Port, Antreneau, Ant(QuestStatus.Completed)));
        Fake Hot(int nibble, int title)
        {
            var f = Give(new Fake { Status = QuestStatus.Accepted }, hotrok);
            f.MissionStatuses[cop] = nibble << 8;
            if (title != 0) f.Titles.Add(title);
            return f;
        }
        int comp = QC.Title.CompanionOfLouverance, trueComp = QC.Title.TrueCompanionOfLouverance;
        L("oh 14 comp", R(oh, Td, Ule, Qm, Hot(14, comp)));
        L("oh 14", R(oh, Td, Ule, Qm, Hot(14, 0)));
        L("oh 0 true", R(oh, Td, Ule, Qm, Hot(0, trueComp)));
        L("oh 0", R(oh, Td, Ule, Qm, Hot(0, 0)));
        L("oh qm accepted", R(oh, Tr, Ule, Qm, new Fake { Status = QuestStatus.Accepted }));

        Fake Trust(QuestStatus st, int level = 5)
        {
            var f = new Fake { Status = st, MainLevel = level };
            f.MainNumbers["ENABLE_TRUST_QUESTS"] = 1;
            return f;
        }
        string Gondebaud = QC.Npc.SouthernSanDoria.Gondebaud, Excenmille = QC.Npc.NorthernSanDoria.Excenmille;
        L("tr 3500", R(tr, Tr, South, Gondebaud, Trust(QuestStatus.Available)));
        var won = Trust(QuestStatus.Available);
        won.QuestStatuses["Windurst/TRUST_WINDURST"] = QuestStatus.Completed;
        L("tr 3504", R(tr, Tr, South, Gondebaud, won));
        var other = Trust(QuestStatus.Available);
        other.QuestStatuses["Bastok/TRUST_BASTOK"] = QuestStatus.Accepted;
        L("tr nothing", R(tr, Tr, South, Gondebaud, other));
        var card = Trust(QuestStatus.Accepted);
        card.KeyItems[QC.KeyItem.RedInstituteCard] = true;
        L("tr 3501", R(tr, Tr, South, Gondebaud, card));
        L("tr 3502", R(tr, Tr, South, Gondebaud, Trust(QuestStatus.Completed)));
        L("tr 893", R(tr, Tr, North, Excenmille, Trust(QuestStatus.Accepted)));
        var first1 = Trust(QuestStatus.Accepted);
        first1.CharVars["SandoriaFirstTrust"] = 1;
        L("tr 894", R(tr, Tr, North, Excenmille, first1));
        var first2 = Trust(QuestStatus.Accepted);
        first2.CharVars["SandoriaFirstTrust"] = 2;
        L("tr 895", R(tr, Tr, North, Excenmille, first2));
        var other2 = Trust(QuestStatus.Accepted);
        other2.QuestStatuses["Bastok/TRUST_BASTOK"] = QuestStatus.Completed;
        L("tr 897", R(tr, Tr, North, Excenmille, other2));
        L("tr 896", R(tr, Tr, North, Excenmille, Trust(QuestStatus.Completed)));
        Check("sandoria g16:the reply kinds no flow can show",
              "ts offer=Event(733,0,1545,4,20) ; ts fame 1=null ; ts held=Event(708) ; " +
              "ts lost=Event(737,0,1545,4,20) ; " +
              "ts completed=Event(736) ; ts trade=Event(734,0,1545,4,20) ; " +
              "oh 725=Event(725) ; oh 726=Event(726) ; oh 727=Event(727) ; " +
              "oh 14 comp=Event(10,0,1729,431,0,0,1,0) ; oh 14=Event(10,0,1729,431,1,0,1,0) ; " +
              "oh 0 true=Event(10,0,1729,431,0,1,0,0) ; oh 0=Event(10,0,1729,431,1,0,0,0) ; " +
              "oh qm accepted=NoAction()+Text.Special(5:SOMETHING_GLITTERING)+" +
              "Text.Special(5:WHAT_LIES_BENEATH,0,1729) ; " +
              "tr 3500=Event(3500) ; tr 3504=Event(3504) ; tr nothing=NoAction() ; tr 3501=Event(3501) ; " +
              "tr 3502=Event(3502) ; tr 893=Event(893,0,0,0,0,0,0,0,0) ; " +
              "tr 894=Event(894)+Player.SetLocalVar(ExcenmilleTrustChatFlag,1) ; tr 895=Event(895) ; " +
              "tr 897=Event(897,0,0,0,0,0,0,0,0) ; " +
              "tr 896=Event(896,0,0,0,0,0,0,0,0)+Player.SetLocalVar(ExcenmilleTrustChatFlag,1)",
              string.Join(" ; ", lines));

        // the finishes, each run on a fake at the csid (and option), in the scripts' order
        string Finish(QuestScriptDefinition def, ushort zone, int csid, int option = 0, Action<Fake> setup = null)
        {
            var h = def.States.SelectMany(st => st.Handlers)
                .Single(x => x.Kind == QuestHandlerKind.OnEventFinish && x.Zone == zone && x.Id == csid);
            var f = new Fake { Status = QuestStatus.Accepted, Csid = csid, Option = option, Definition = def };
            setup?.Invoke(f);
            h.Run(f);
            return string.Join(";", f.Calls);
        }
        Check("sandoria g16:733's finish: option 1 begun, the fork; no free slot: the text only; option 0: nothing",
              $"Quest.Begin();Player.AddItem({fork},1);Text.Special(231:ITEM_OBTAINED,{fork})|" +
              $"Text.Special(231:ITEM_CANNOT_BE_OBTAINED,{fork})|",
              Finish(ts, North, 733, 1) + "|" + Finish(ts, North, 733, 1, f => f.FreeSlots = 0) + "|" +
              Finish(ts, North, 733, 0));
        Check("sandoria g16:734's and 737's finishes: a fork on 734 option 0, 737 any option; 734 option 1 teleports",
              $"Player.AddItem({fork},1);Text.Special(231:ITEM_OBTAINED,{fork})|" +
              $"Player.AddItem({fork},1);Text.Special(231:ITEM_OBTAINED,{fork})|Player.TeleportTo(43)|",
              Finish(ts, North, 734, 0) + "|" + Finish(ts, North, 737, 5) + "|" + Finish(ts, North, 734, 1) + "|" +
              Finish(ts, North, 734, 2));
        ushort Frost = QC.Zone.CloisterOfFrost;
        Check("sandoria g16:32001's finish, in order: Shiva, the scroll, fame, the raw completion",
              "Player.AddSpell(302);Text.Special(203:SHIVA_UNLOCKED,0,0,4);" +
              $"Player.GiveItem({QC.Item.ScrollOfInstantWarp});Player.AddFame({sandoria},20);" +
              "Player.CompleteQuest(Sandoria,TRIAL_SIZE_TRIAL_BY_ICE)",
              Finish(ts, Frost, 32001));
        Check("sandoria g16:725's finish begins on any option", "Quest.Begin()|Quest.Begin()",
              Finish(oh, Port, 725, 0) + "|" + Finish(oh, Port, 725, 3));
        Check("sandoria g16:10's finish at GIL_RATE and EXP_RATE 1.03125 (2062.5: llround 2063), in order",
              $"Player.GiveKeyItem({QC.KeyItem.MapOfTheUleguerandRange});Player.AddFame({sandoria},80);" +
              "Player.AddGil(2063);Text.Special(5:GIL_OBTAINED,2063);Player.AddExp(2063);" +
              "Player.CompleteQuest(Sandoria,OVER_THE_HILLS_AND_FAR_AWAY);Trade.Confirm()",
              Finish(oh, Ule, 10, setup: f =>
              {
                  f.MainNumbers["GIL_RATE"] = 1.03125;
                  f.MainNumbers["EXP_RATE"] = 1.03125;
              }));
        int cardKi = QC.KeyItem.RedInstituteCard, permit = QC.KeyItem.SanDoriaTrustPermit;
        Check("sandoria g16:3500's / 3504's finishes: begun with the card on option 2 only",
              $"|Quest.Begin();Player.GiveKeyItem({cardKi})|Quest.Begin();Player.GiveKeyItem({cardKi})",
              Finish(tr, South, 3500, 0) + "|" + Finish(tr, South, 3500, 2) + "|" + Finish(tr, South, 3504, 2));
        Check("sandoria g16:893's, 895's and 897's finishes, in order",
              "Player.AddSpell(899,True);Text.Special(231:YOU_LEARNED_TRUST,0,899);" +
              "Player.SetCharVar(SandoriaFirstTrust,1,0)|" +
              $"Player.DelKeyItem({cardKi});Text.Special(231:KEYITEM_LOST,{cardKi});Player.GiveKeyItem({permit});" +
              $"Player.AddTitle({QC.Title.TheTrustworthy});Player.SetCharVar(SandoriaFirstTrust,0,0);" +
              "Player.CompleteQuest(Sandoria,TRUST_SANDORIA);Text.Special(231:CALL_MULTIPLE_ALTER_EGO)|" +
              "Player.AddSpell(899,True);Text.Special(231:YOU_LEARNED_TRUST,0,899);" +
              $"Player.DelKeyItem({cardKi});Text.Special(231:KEYITEM_LOST,{cardKi});Player.GiveKeyItem({permit});" +
              "Player.CompleteQuest(Sandoria,TRUST_SANDORIA)",
              Finish(tr, North, 893) + "|" + Finish(tr, North, 895) + "|" + Finish(tr, North, 897));
    }
}
