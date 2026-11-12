// XI.Quests -- quest rework C2 (docs/superpowers/specs/2026-11-05-quest-rework-c2-mission-platform-design.md, section
// 3.2): q.Mission, what a mission's check or handler can read and do with its own mission. The bridge implements it
// over the Lua Mission container (scripts/globals/interaction/mission.lua); the fake over plain fields. A quest's or
// battlefield's context has none (q.Mission is null there).
namespace XI.Quests.Authoring
{
    // q.Mission in a check (IQuestCheck.Mission): reads only
    public interface IMissionProgressView
    {
        // the log's current mission is this one: the check's currentMission argument (a handler's
        // player:getCurrentMission(log)) equals the mission's id
        bool IsCurrent { get; }
        // the log's whole mission status: the check's missionStatus argument (a handler's player:getMissionStatus(log))
        int Status { get; }
        // player:getMissionStatus(log, pos): the 4-bit field at pos (0 to 7) of the status word
        int StatusAt(int pos);
        // mission:getVar(player, name): the char var Mission[<log>][<id>]<name>; in a check through the lookup's var
        // cache
        int Var(string name);
        // mission:getMustZone(player)
        bool MustZone { get; }
        // mission:getLocalVar(player, name)
        int LocalVar(string name);
        // Container:isVarBitsSet(player, name, bits...): every bit of the char var Mission[<log>][<id>]<name> set
        bool IsVarBitsSet(string name, params int[] bits);
    }

    // q.Mission in a handler (IQuestContext.Mission): the reads and the mission's effects, each the Lua call the
    // Mission container makes
    public interface IMissionProgress : IMissionProgressView
    {
        // mission:begin(player): player:addMission(log, id)
        void Begin();
        // mission:complete(player): npcUtil.completeMission with the mission's reward; when it completed, the status
        // set to 0 (except on CoP, log 6) and the Mission[log][id] vars cleared. Its answer is npcUtil's (false: no
        // room for the reward's item)
        bool Complete();
        // player:completeMission(log, id) only, for the arms whose Lua calls it directly (no reward, no status reset,
        // no cleanup); true
        bool CompleteRaw();
        // player:setMissionStatus(log, value)
        void SetStatus(int value);
        // player:setMissionStatus(log, value, pos): the 4-bit field at pos
        void SetStatusAt(int value, int pos);
        // mission:setVar(player, name, value, expiry): an expiry of 0 is no expiry (nil)
        void SetVar(string name, int value, uint expiry = 0);
        // mission:setMustZone(player)
        void SetMustZone();
        // mission:setLocalVar(player, name, value)
        void SetLocalVar(string name, int value);
        // Container:setVarBit(player, name, bit): writes only when the bit is clear
        void SetVarBit(string name, int bit);
        // Container:incrementVar(player, name, value): player:incrementCharVar(prefix .. name, value)
        void IncrementVar(string name, int value);
        // mission:cleanup(player): player:clearVarsWithPrefix('Mission[<log>][<id>]')
        void Cleanup();
        // (quest rework C3 C4, CoP 8-1's Rubious Crystals) for _, member in ipairs(player:getAlliance()) do
        // mission:setLocalVar(member, name, value) ... end: each var in order, on each alliance member in turn
        void SetAllianceLocalVars(params (string Name, int Value)[] vars);
    }
}
