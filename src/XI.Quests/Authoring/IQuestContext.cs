// XI.Quests -- what a quest check or handler can read and do (quest rework B2, split into facets in B3a). The bridge
// implements them over the engine. Grown per quest: only what a ported quest uses exists, and anything missing is a
// compile error.
namespace XI.Quests.Authoring
{
    // a state's check (QuestMatch.When): reads only, vars through the dispatch's cache; no Npc, as a Lua quest's check
    // (player, status, vars) gets none
    public interface IQuestCheck
    {
        IQuestPlayerView Player { get; }
        IQuestProgressView Quest { get; }
        ushort Zone { get; }
        IQuestSettings Settings { get; }
        // the check's mission (quest rework C2, MissionScript): its current-ness, status and vars; null for a quest's
        // or a battlefield's check
        IMissionProgressView Mission { get; }
    }

    // a handler's context
    public interface IQuestContext
    {
        IQuestPlayer Player { get; }
        IQuestProgress Quest { get; }
        IQuestTrade Trade { get; }
        IQuestText Text { get; }
        // the handler's NPC (onTrigger, onTrade, an event with one), else null
        IQuestNpc Npc { get; }
        IQuestSettings Settings { get; }
        ushort Zone { get; }
        int Csid { get; }
        int Option { get; }
        // the event option when it is a Lua string (onEventUpdate after a typed input, GP_CLI_COMMAND_PASSWARDS: Lua's
        // type(option) == 'string'), else null; Option is then 0, as it always was for a non-number option
        string OptionText { get; }
        // triggerArea:getTriggerAreaID() (OnTriggerAreaEnter, OnTriggerAreaLeave)
        int TriggerAreaId { get; }
        // the dying mob of an OnMobDeath (mob:getID(), mob:getLocalVar(name)), else null
        IQuestNpc Mob { get; }
        // an OnMobDeath's optParams (quest rework P7): the handler's third argument, the table luautils::OnMobDeath passes
        // for each alliance member in the killer's zone (optParams.isKiller is true for the killer only). Null outside an
        // OnMobDeath, and when the call passed no table (a Lua handler reading optParams.isKiller fails on nil, a C# one on
        // null). One read-only view for the one argument, as Mob is for the mob: Csid / Option / TriggerAreaId are each one
        // argument's single value, optParams holds five
        IQuestMobDeath MobDeath { get; }
        // the handler's mission (quest rework C2, MissionScript): the Lua Mission container's calls; null for a quest's
        // or a battlefield's handler
        IMissionProgress Mission { get; }
    }
}
