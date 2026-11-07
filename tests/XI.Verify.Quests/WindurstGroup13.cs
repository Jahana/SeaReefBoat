// XI.Verify.Quests -- quest rework B3b Windurst group 13 (call-only: Hat in Hand and A Feather in One's Cap at
// Baren-Moren; group 14 is in WindurstGroup14.cs). The golden flows take every arm. These checks pin what no flow can
// see: the replies' kinds. In an NPC call the bridge renders an Event, a ProgressEvent and a PriorityEvent alike, as
// player:startEvent (ZoneHost.QuestCalls.cs RenderCallReply: no action is built, so no priority), which is what the Lua
// scripts call; the fake keeps them apart.
using System;
using System.Linq;
using XI.Quests;
using XI.Quests.Authoring;
using QC = XI.QuestContent;

internal static partial class Program
{
    static void RunWindurstGroup13()
    {
        Console.WriteLine("== quest rework B3b Windurst group 13: the reply kinds (call-only) ==");
        var hat = new QC.Windurst.HatInHand().Compile();
        var feather = new QC.Windurst.AFeatherInOnesCap().Compile();

        Check("windurst g13:Hat in Hand and A Feather in One's Cap clean", "/",
              string.Join("/", new[] { hat, feather }.Select(d => string.Join(" | ", QuestScriptValidator.Validate(d)))));

        // each trigger reply in declaration order, as the scripts had them (player:startEvent). Hat in Hand's show-off
        // replies are functions: each runs on a fresh fake, and the hatRewardTier it sets is shown after it
        Check("windurst g13:the trigger replies are the Lua's startEvent ids, as Event (52 with its hatRewardTier)",
              "Baren-Moren:Event(48),Baren-Moren:Event(52,80)[tier 5],Baren-Moren:Event(52,50)[tier 4]," +
              "Baren-Moren:Event(52,30)[tier 3],Baren-Moren:Event(52,20)[tier 2],Baren-Moren:Event(52)[tier 1]" +
              "|Baren-Moren:Event(75,0,842),Baren-Moren:Event(78,0,842),Baren-Moren:Event(75,0,842)",
              W13Replies(hat, QuestHandlerKind.OnTrigger) + "|" + W13Replies(feather, QuestHandlerKind.OnTrigger));

        // Baren-Moren's trade (Baren-Moren.lua onTrade, npcUtil.tradeHas): three Giant Bird Feathers: 79 (1500); two: nil
        Check("windurst g13:Baren-Moren's trade: 3 Giant Bird Feathers, then 2",
              "Baren-Moren:Event(79,1500)|Baren-Moren:null",
              W13Replies(feather, QuestHandlerKind.OnTrade, feathers: 3) + "|" +
              W13Replies(feather, QuestHandlerKind.OnTrade, feathers: 2));

        // 75's finish, option 1: begun while available, the repeat var while completed, nothing while accepted. The last
        // is invisible in game (a player:addQuest on a quest already current changes nothing, mutant round 1-2 survivor
        // F100), so the fake pins the Lua's arms by the calls made
        var finish75 = feather.States.SelectMany(s => s.Handlers).Single(h => h.Kind == QuestHandlerKind.OnEventFinish && h.Id == 75);
        string Finish75(QuestStatus status)
        {
            var f = new Fake { Status = status, Option = 1 };
            finish75.Run(f);
            return string.Join(";", f.Calls);
        }
        Check("windurst g13:75's finish, option 1: available, accepted, completed",
              "Quest.Begin()||Player.SetCharVar(QuestFeatherInOnesCap_var,1,0)",
              string.Join("|", Finish75(QuestStatus.Available), Finish75(QuestStatus.Accepted), Finish75(QuestStatus.Completed)));
    }

    // the replies of a kind in declaration order, "<entity>:<reply>", each on a fresh fake (with that many Giant Bird
    // Feathers in its trade), a hatRewardTier the reply set shown as [tier n]
    static string W13Replies(QuestScriptDefinition def, QuestHandlerKind kind, int feathers = 0) =>
        string.Join(",", def.States.SelectMany(s => s.Handlers).Where(h => h.Kind == kind).Select(h =>
        {
            var f = new Fake();
            if (feathers > 0) f.TradeItems[QC.Item.GiantBirdFeather] = feathers;
            var r = h.Fixed ?? h.Reply(f);
            string tier = f.PlayerLocalVars.TryGetValue("hatRewardTier", out var t) ? $"[tier {t}]" : "";
            return $"{h.Entity}:{(r == null ? "null" : QuestContextFake.Describe(r))}{tier}";
        }));
}
