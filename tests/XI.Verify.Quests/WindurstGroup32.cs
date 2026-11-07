// XI.Verify.Quests -- quest rework B3b Windurst group 32 (call-only: Trust: Windurst at Wetata and Kupipi). The golden
// flows take every arm a char reaches in play and the GM-set states (the other nations' trust quests accepted or
// completed, WindurstFirstTrust outside 0-2, the card or the permit held or not, the Nanaa Mihgo trust known), the
// level and the ENABLE_TRUST_QUESTS guard (0, 1.25, true) and the memories / rank arguments. These checks add only
// what no flow can see (fake-only): the replies' kinds (in an NPC call the bridge renders an Event, a ProgressEvent and
// a PriorityEvent alike, as player:startEvent: ZoneHost.QuestCalls.cs RenderCallReply; NoAction plays nothing in
// either), the trailing zero args of 435 / 439 (Event(435) sends the same packet) and the handlers' and finishes'
// calls in order. Two pins overlap the flows: Fin(863, 3) (the main flow's option 3 at 863) and the flag's name
// (#gm: a rezone before the GM's flag 2, so a misspelt name reads its own flag 0).
// W15Handlers is in WindurstGroup15.cs.
using System;
using System.Linq;
using XI.Quests;
using XI.Quests.Authoring;
using QC = XI.QuestContent;

internal static partial class Program
{
    static void RunWindurstGroup32()
    {
        Console.WriteLine("== quest rework B3b Windurst group 32: the reply kinds and the finishes (call-only) ==");
        var tw = new QC.Windurst.TrustWindurst().Compile();
        const string Sandoria = "Sandoria/TRUST_SANDORIA", Bastok = "Bastok/TRUST_BASTOK";
        const string FirstTrust = "WindurstFirstTrust", ChatFlag = "KupipiTrustChatFlag";
        const QuestStatus Av = QuestStatus.Available, Ac = QuestStatus.Accepted, Co = QuestStatus.Completed;
        const int Card = QC.KeyItem.GreenInstituteCard;

        Check("windurst g32:Trust: Windurst clean", "", string.Join(" | ", QuestScriptValidator.Validate(tw)));

        string Say(QuestReply r) => r == null ? "null" : QuestContextFake.Describe(r);
        // the reply, then the calls other than the reads
        string Run(string npc, Fake f)
        {
            string reply = Say(W15Handlers(tw, QuestHandlerKind.OnTrigger).Single(h => h.Entity == npc).Reply(f));
            string calls = string.Join(";", f.Calls.Where(c => !c.StartsWith("Player.Has")));
            return calls.Length == 0 ? reply : reply + " " + calls;
        }

        // Wetata: a level 5 char, ENABLE_TRUST_QUESTS 1, the other two trust quests as given
        Fake W(QuestStatus s, QuestStatus bastok = Av, QuestStatus sandoria = Av, int lvl = 5, bool card = false)
        {
            var f = new Fake { Status = s, MainLevel = lvl };
            f.MainSettings["ENABLE_TRUST_QUESTS"] = 1;
            f.QuestStatuses[Bastok] = bastok;
            f.QuestStatuses[Sandoria] = sandoria;
            if (card) f.KeyItems[Card] = true;
            return f;
        }
        string Wet(Fake f) => Run("Wetata", f);
        Fake Setting(Fake f, Action<Fake> set)
        {
            f.MainSettings.Remove("ENABLE_TRUST_QUESTS");
            set(f);
            return f;
        }
        Check("windurst g32:Wetata's 863 / 867 (Event), NoAction with another trust quest accepted, 864, 861, null",
              "Event(863)|Event(867)|Event(867)|Event(867)|NoAction()|NoAction()|NoAction()|Event(863)|" +
              "Event(864)|null|Event(864)|Event(861)|Event(864)|null|null",
              string.Join("|", Wet(W(Av)), Wet(W(Av, Co)), Wet(W(Av, Av, Co)), Wet(W(Av, Ac, Co)), Wet(W(Av, Ac)),
                  Wet(W(Av, Av, Ac)), Wet(W(Av, Ac, Ac)), Wet(W(Av, card: true)), Wet(W(Av, lvl: 4, card: true)),
                  Wet(W(Av, lvl: 4)), Wet(W(Ac, card: true)), Wet(W(Co)), Wet(W(Co, card: true)), Wet(W(Ac)),
                  Wet(W(Ac, Co))));
        Check("windurst g32:Wetata's guard reads ENABLE_TRUST_QUESTS as the Lua value (0, 1.25, true, unset: not 1)",
              "null|null|null|null|Event(863)|Event(863)",
              string.Join("|", Wet(Setting(W(Av), f => f.MainSettings["ENABLE_TRUST_QUESTS"] = 0)),
                  Wet(Setting(W(Av), f => f.MainNumbers["ENABLE_TRUST_QUESTS"] = 1.25)),
                  Wet(Setting(W(Av), f => f.MainBools["ENABLE_TRUST_QUESTS"] = true)),
                  Wet(Setting(W(Av), f => { })),
                  Wet(Setting(W(Av), f => f.MainNumbers["ENABLE_TRUST_QUESTS"] = 1.0)),
                  Wet(W(Av, lvl: 99))));

        // Kupipi: a Bastok char (nation 1) of rank 1, the other two trust quests available, the vars as given
        Fake K(QuestStatus s, int var = 0, int flag = 0, QuestStatus bastok = Av, QuestStatus sandoria = Av)
        {
            var f = new Fake { Status = s, NationId = 1 };
            f.QuestStatuses[Bastok] = bastok;
            f.QuestStatuses[Sandoria] = sandoria;
            f.CharVars[FirstTrust] = var;
            f.PlayerLocalVars[ChatFlag] = flag;
            return f;
        }
        string Kup(Fake f) => Run("Kupipi", f);
        Check("windurst g32:Kupipi's 439 / 435 / 436 / 437 / 438 (Event), the flag set after 436 / 438, null",
              "Event(439,0,0,0,0,0,0,0,0)|Event(439,0,0,0,0,0,0,0,0)|Event(439,0,0,0,0,0,0,0,0)|" +
              "Event(439,0,0,0,0,0,0,0,0)|Event(435,0,0,0,0,0,0,0,0)|Event(435,0,0,0,0,0,0,0,0)|" +
              "Event(436) Player.SetLocalVar(KupipiTrustChatFlag,1)|null|Event(437)|Event(437)|null|null|null|" +
              "Event(438) Player.SetLocalVar(KupipiTrustChatFlag,1)|" +
              "Event(438) Player.SetLocalVar(KupipiTrustChatFlag,1)|" +
              "Event(438) Player.SetLocalVar(KupipiTrustChatFlag,1)|" +
              "Event(438) Player.SetLocalVar(KupipiTrustChatFlag,1)|" +
              "null|null|null|null|null",
              string.Join("|", Kup(K(Ac, 0, 0, Co)), Kup(K(Ac, 1, 0, Av, Co)), Kup(K(Ac, 2, 1, Co, Co)),
                  Kup(K(Ac, 3, 0, Av, Co)), Kup(K(Ac, 0)), Kup(K(Ac, 0, 1, Ac, Ac)), Kup(K(Ac, 1)), Kup(K(Ac, 1, 1)),
                  Kup(K(Ac, 2)), Kup(K(Ac, 2, 1)), Kup(K(Ac, 3)), Kup(K(Ac, -1)), Kup(K(Ac, 1, 2)),
                  Kup(K(Co)), Kup(K(Co, 1)), Kup(K(Co, 2, 0, Co)), Kup(K(Co, 0, 0, Av, Co)), Kup(K(Co, 0, 1)),
                  Kup(K(Co, 0, 2)), Kup(K(Av)), Kup(K(Av, 1)), Kup(K(Av, 2, 0, Co, Co))));
        Fake Learned(Fake f, params int[] spells)
        {
            foreach (int s in spells) f.Spells.Add(s);
            return f;
        }
        Fake KnowsNanaa(Fake f) => Learned(f, 901);
        Check("windurst g32:Kupipi's 438 only without the Nanaa Mihgo trust (the other trusts not read)",
              "null|Event(438) Player.SetLocalVar(KupipiTrustChatFlag,1)|Event(435,0,0,0,0,0,0,0,0)",
              string.Join("|", Kup(KnowsNanaa(K(Co))), Kup(Learned(K(Co), 898, 899, 902, 903, 904, 905)),
                  Kup(KnowsNanaa(K(Ac)))));
        // the memories (The Three Kingdoms 2, Moon Reading 8) and rank3 (the char's own nation's rank >= 3)
        Fake M(Fake f, int nation, int rank, params int[] missions)
        {
            f.NationId = nation;
            f.Ranks[nation] = rank;
            foreach (int m in missions) f.CompletedMissions.Add($"{QC.MissionLog.Windurst}/{m}");
            return f;
        }
        // a rank in another nation than the char's own (rank3 reads the char's own nation's rank)
        Fake RankElsewhere(Fake f, int nation, int rank)
        {
            f.Ranks[nation] = rank;
            return f;
        }
        const int Ttk = QC.Mission.Windurst.TheThreeKingdoms, Moon = QC.Mission.Windurst.MoonReading;
        Check("windurst g32:Kupipi's 435 / 439 memories and rank3",
              "Event(435,0,0,0,0,0,0,0,0)|Event(435,0,0,0,2,0,0,0,1)|Event(435,0,0,0,8,0,0,0,0)|" +
              "Event(435,0,0,0,10,0,0,0,1)|Event(435,0,0,0,0,0,0,0,1)|Event(435,0,0,0,0,0,0,0,0)|" +
              "Event(439,0,0,0,10,0,0,0,1)|Event(439,0,0,0,2,0,0,0,0)",
              string.Join("|", Kup(M(K(Ac), 2, 2)), Kup(M(K(Ac), 2, 3, Ttk)), Kup(M(K(Ac), 2, 2, Moon)),
                  Kup(M(K(Ac), 2, 10, Ttk, Moon)), Kup(M(K(Ac), 1, 3)), Kup(RankElsewhere(M(K(Ac), 0, 2), 1, 5)),
                  Kup(M(K(Ac, 0, 0, Av, Co), 2, 10, Ttk, Moon)), Kup(M(K(Ac, 2, 0, Co), 1, 2, Ttk))));

        // the finishes' calls, in order, on that option
        string Fin(int csid, int option = 0)
        {
            var h = tw.States.SelectMany(s => s.Handlers)
                      .Single(x => x.Kind == QuestHandlerKind.OnEventFinish && x.Id == csid);
            var f = new Fake { Status = QuestStatus.Accepted, Csid = csid, Option = option };
            h.Run(f);
            return string.Join(";", f.Calls);
        }
        Check("windurst g32:Wetata's 863 / 867 finishes: begun with the card on option 2 only",
              "|||Quest.Begin();Player.GiveKeyItem(2496)|||Quest.Begin();Player.GiveKeyItem(2496)",
              string.Join("|", Fin(863), Fin(863, 1), Fin(863, 3), Fin(863, 2), Fin(867), Fin(867, 1), Fin(867, 2)));
        Check("windurst g32:Kupipi's 435 / 437 / 439 finishes (any option), npcUtil.completeQuest step for step",
              "Player.AddSpell(898,True);Text.Special(242:YOU_LEARNED_TRUST,0,898);" +
              "Player.SetCharVar(WindurstFirstTrust,1,0)|" +
              "Player.AddSpell(898,True);Text.Special(242:YOU_LEARNED_TRUST,0,898);" +
              "Player.SetCharVar(WindurstFirstTrust,1,0)|" +
              "Player.DelKeyItem(2496);Text.Special(242:KEYITEM_LOST,2496);Player.GiveKeyItem(2497);" +
              "Player.AddTitle(894);Player.SetCharVar(WindurstFirstTrust,0,0);" +
              "Player.CompleteQuest(Windurst,TRUST_WINDURST);" +
              "Text.Special(242:CALL_MULTIPLE_ALTER_EGO)|" +
              "Player.DelKeyItem(2496);Text.Special(242:KEYITEM_LOST,2496);Player.GiveKeyItem(2497);" +
              "Player.AddTitle(894);Player.SetCharVar(WindurstFirstTrust,0,0);" +
              "Player.CompleteQuest(Windurst,TRUST_WINDURST);" +
              "Text.Special(242:CALL_MULTIPLE_ALTER_EGO)|" +
              "Player.AddSpell(898,True);Text.Special(242:YOU_LEARNED_TRUST,0,898);Player.DelKeyItem(2496);" +
              "Text.Special(242:KEYITEM_LOST,2496);Player.GiveKeyItem(2497);" +
              "Player.CompleteQuest(Windurst,TRUST_WINDURST)|" +
              "Player.AddSpell(898,True);Text.Special(242:YOU_LEARNED_TRUST,0,898);Player.DelKeyItem(2496);" +
              "Text.Special(242:KEYITEM_LOST,2496);Player.GiveKeyItem(2497);" +
              "Player.CompleteQuest(Windurst,TRUST_WINDURST)",
              string.Join("|", Fin(435), Fin(435, 1), Fin(437), Fin(437, 2), Fin(439), Fin(439, 1)));
    }
}
