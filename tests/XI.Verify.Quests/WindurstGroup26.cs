// XI.Verify.Quests -- quest rework B3b Windurst group 26 (call-only: Making Headlines at Naiko-Paneiko, Kyume-Romeh,
// Yujuju, Hiwon-Biwon, Umumu and the Mahogany Door _5ca). The golden flows take every arm a char reaches in play, the
// GM-set states the door and Naiko-Paneiko read (#gm: three scoops, bit 4 set, available / completed at the var 15),
// the scoop NPCs once completed (#completed), the rival containers (#wildcat, #wildcat_kyume, #wildcat_umumu, Curses,
// Foiled Again!? (2)'s #headlines, Scooped's flow, the missions' #mission and #cop) and every roll's both arms
// (#rolls). These checks add only what no flow can see:
//  - the replies' kinds (fake-only): in an NPC call the bridge renders an Event, a ProgressEvent and a PriorityEvent
//    alike, as player:startEvent (ZoneHost.QuestCalls.cs RenderCallReply), which is what the Lua scripts call; and the
//    door's NoAction and a null alike (the door's script ignores the call's answer); the fake keeps them apart;
//  - the roll bounds (fake-only): math.randomInt(1, 100) <= 50 at Naiko-Paneiko's reminder and finish and Hiwon-Biwon's
//    sick scoop. The flows' draws are seeded and show both arms, but no flow can steer a draw to exactly 50 or 51.
// W15Handlers is in WindurstGroup15.cs.
using System;
using System.Linq;
using XI.Quests;
using XI.Quests.Authoring;
using QC = XI.QuestContent;

internal static partial class Program
{
    static void RunWindurstGroup26()
    {
        Console.WriteLine("== quest rework B3b Windurst group 26: the reply kinds and roll bounds (call-only) ==");
        var mh = new QC.Windurst.MakingHeadlines().Compile();
        const string Var = "QuestMakingHeadlines_var";

        Check("windurst g26:Making Headlines clean", "", string.Join(" | ", QuestScriptValidator.Validate(mh)));

        // a trigger's reply on a fresh fake (that status, the var, the draw given, Curses, Foiled Again! (1)'s status)
        string Reply(string npc, QuestStatus status, int var, int roll = 1,
                     QuestStatus curses1 = QuestStatus.Available)
        {
            var f = new Fake { Status = status };
            f.CharVars[Var] = var;
            f.Rolls.Enqueue(roll);
            f.QuestStatuses[$"{QuestLog.Windurst}/CURSES_FOILED_AGAIN_1"] = curses1;
            var r = W15Handlers(mh, QuestHandlerKind.OnTrigger).Single(h => h.Entity == npc).Reply(f);
            return r == null ? "null" : QuestContextFake.Describe(r);
        }

        string Naiko(QuestStatus s, int v, int roll = 1) => Reply("Naiko-Paneiko", s, v, roll);
        Check("windurst g26:Naiko-Paneiko's replies by state, var and roll (50 / 51), as Event",
              "Event(665)|Event(666)|Event(671)|Event(673)|Event(674)|Event(670)|null",
              string.Join("|", Naiko(QuestStatus.Available, 0), Naiko(QuestStatus.Accepted, 0, 50),
                  Naiko(QuestStatus.Accepted, 0, 51), Naiko(QuestStatus.Accepted, 15),
                  Naiko(QuestStatus.Accepted, 31, 50), Naiko(QuestStatus.Accepted, 31, 51),
                  Naiko(QuestStatus.Completed, 0)));
        string Kyume(QuestStatus s, int v) => Reply("Kyume-Romeh", s, v);
        string Yujuju(QuestStatus s, int v) => Reply("Yujuju", s, v);
        Check("windurst g26:Kyume-Romeh's and Yujuju's replies, as Event",
              "Event(668)|Event(669)|null|null|Event(314)|Event(315)|null|null",
              string.Join("|", Kyume(QuestStatus.Accepted, 0), Kyume(QuestStatus.Accepted, 1),
                  Kyume(QuestStatus.Available, 0), Kyume(QuestStatus.Completed, 0), Yujuju(QuestStatus.Accepted, 0),
                  Yujuju(QuestStatus.Accepted, 2), Yujuju(QuestStatus.Available, 0), Yujuju(QuestStatus.Completed, 0)));
        Check("windurst g26:Hiwon-Biwon's replies (the sick roll 50 / 51), as Event",
              "Event(283)|Event(284)|Event(281)|Event(282)|null|null|Event(281)",
              string.Join("|", Reply("Hiwon-Biwon", QuestStatus.Accepted, 0, 50, QuestStatus.Accepted),
                  Reply("Hiwon-Biwon", QuestStatus.Accepted, 0, 51, QuestStatus.Accepted),
                  Reply("Hiwon-Biwon", QuestStatus.Accepted, 0), Reply("Hiwon-Biwon", QuestStatus.Accepted, 4),
                  Reply("Hiwon-Biwon", QuestStatus.Available, 0), Reply("Hiwon-Biwon", QuestStatus.Completed, 0),
                  Reply("Hiwon-Biwon", QuestStatus.Accepted, 0, 50, QuestStatus.Completed)));
        // the three rolls' bounds as the fake sees them (RandomInt(lower, upper)): Naiko-Paneiko's reminder and finish and
        // Hiwon-Biwon's sick scoop; Umumu's completed draw is (1, 3)
        string RollCalls(string npc, QuestStatus status, int var, QuestStatus curses1 = QuestStatus.Available)
        {
            var f = new Fake { Status = status };
            f.CharVars[Var] = var;
            f.Rolls.Enqueue(1);
            f.QuestStatuses[$"{QuestLog.Windurst}/CURSES_FOILED_AGAIN_1"] = curses1;
            W15Handlers(mh, QuestHandlerKind.OnTrigger).Single(h => h.Entity == npc).Reply(f);
            return string.Join(";", f.Calls.Where(c => c.Contains("RandomInt")));
        }
        Check("windurst g26:the roll calls are RandomInt(1,100) (reminder, finish, sick scoop) and (1,3) (Umumu)",
              "Player.RandomInt(1,100)|Player.RandomInt(1,100)|Player.RandomInt(1,100)|Player.RandomInt(1,3)",
              string.Join("|", RollCalls("Naiko-Paneiko", QuestStatus.Accepted, 0),
                  RollCalls("Naiko-Paneiko", QuestStatus.Accepted, 31),
                  RollCalls("Hiwon-Biwon", QuestStatus.Accepted, 0, QuestStatus.Accepted),
                  RollCalls("Umumu", QuestStatus.Completed, 0)));
        Check("windurst g26:Umumu's replies (the completed draw 1 / 2 / 3), as Event",
              "Event(383)|Event(381)|Event(382)|Event(385)|Event(386)|Event(414)|null",
              string.Join("|", Reply("Umumu", QuestStatus.Accepted, 16), Reply("Umumu", QuestStatus.Accepted, 0),
                  Reply("Umumu", QuestStatus.Accepted, 8), Reply("Umumu", QuestStatus.Completed, 0, 1),
                  Reply("Umumu", QuestStatus.Completed, 0, 2), Reply("Umumu", QuestStatus.Completed, 0, 3),
                  Reply("Umumu", QuestStatus.Available, 0)));
        Check("windurst g26:the Mahogany Door: NoAction after the message and bit 4, else null",
              "NoAction()|null|null|null",
              string.Join("|", Reply("_5ca", QuestStatus.Accepted, 15), Reply("_5ca", QuestStatus.Accepted, 31),
                  Reply("_5ca", QuestStatus.Accepted, 7), Reply("_5ca", QuestStatus.Available, 15)));
    }
}
