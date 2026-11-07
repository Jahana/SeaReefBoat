// XI.Quests -- quest rework C2 (docs/superpowers/specs/2026-11-05-quest-rework-c2-mission-platform-design.md, section
// 3.2): a mission is a C# class, the second kind on the shared container core. Its states are the Lua Mission's
// sections, a state's matches are that section's check (on the log's current mission, its status and the vars), and
// its handlers are the quest ones. Compile() runs Build once and freezes the result; the bridge turns a definition into
// a Lua Mission container (Mission:new). Nothing here touches the engine.
using System;
using System.Collections.Generic;

namespace XI.Quests.Authoring
{
    public abstract class MissionScript : ContainerScript
    {
        protected MissionScript(MissionLog log, string constName)
        {
            Log = log;
            ConstName = constName;
        }

        // the mission log (xi.mission.log_id)
        public MissionLog Log { get; }
        // the xi.mission.id constant, e.g. "THE_ZERUHN_REPORT"
        public string ConstName { get; }
        public override ContainerKind Kind => ContainerKind.Mission;
        // mission.<log in lower case>.<constant in lower case>: the enum's names in lower case are xi.mission.area's
        public override string Key => ContainerKeys.Mission(Log.ToString(), ConstName);

        protected abstract void Build(MissionScriptBuilder b);

        // runs Build on a fresh builder
        public MissionScriptDefinition Compile()
        {
            var b = new MissionScriptBuilder();
            Build(b);
            return b.ToDefinition(Log, ConstName);
        }
    }

    // A state matches as a Lua Mission section check: on the log's current mission, its status and the vars.
    public sealed class MissionMatch
    {
        internal MissionMatch(bool? isCurrent, int? status, Func<IQuestCheck, bool> when)
        {
            IsCurrent = isCurrent;
            Status = status;
            When = when;
        }

        // null: any; true: the log's current mission is this one; false: it is another
        public bool? IsCurrent { get; }
        // the log's whole mission status (getMissionStatus(log)); null: any
        public int? Status { get; }
        // a guard over the check's context; null: pass
        public Func<IQuestCheck, bool> When { get; }
    }

    // mission.reward: npcUtil.completeMission's params, in its order. A field left at 0 (NextLog at null) is not a key
    // of the Lua table, as a Lua mission leaves it out
    public sealed class MissionReward
    {
        public int Item; public int KeyItem; public int Gil; public int Bayld; public int Exp; public int Title;
        public int RankPoints; public int Rank; public MissionLog? NextLog; public int NextMission;

        internal MissionReward Copy() => (MissionReward)MemberwiseClone();
    }

    // the builder behind MissionScript.Build: the quest builder's states and handlers, entered by a mission match
    public sealed class MissionScriptBuilder
    {
        readonly QuestScriptBuilder core_ = new QuestScriptBuilder();
        MissionReward reward_;

        // a copy: the mission can't change its reward after declaring it
        public void Reward(MissionReward reward)
        {
            if (reward == null) throw new ArgumentNullException(nameof(reward));
            reward_ = reward.Copy();
        }

        // a state matching the log's current mission (current: true, the default: this one; false: another one; null:
        // any), its whole status (null: any) and a guard; its NPC and zone handlers are the quest builder's
        public QuestStateBuilder State(bool? current = true, int? status = null, Func<IQuestCheck, bool> when = null) =>
            core_.NewMissionState(new MissionMatch(current, status, when));

        internal MissionScriptDefinition ToDefinition(MissionLog log, string constName) =>
            new MissionScriptDefinition(log, constName, reward_, core_.ToDefinition(QuestScript.HiddenLog, constName));
    }

    public sealed class MissionScriptDefinition
    {
        internal MissionScriptDefinition(MissionLog log, string constName, MissionReward reward,
                                         QuestScriptDefinition core)
        {
            Log = log;
            ConstName = constName;
            Reward = reward;
            Core = core;
        }

        public MissionLog Log { get; }
        public string ConstName { get; }
        // null: no reward table (Lua's empty Mission.reward)
        public MissionReward Reward { get; }
        // the states and handlers on the shared container core, which the bridge builds the sections from (its Log,
        // reward and steps are unused: a mission has its own)
        public QuestScriptDefinition Core { get; }
        // declaration order == Lua section order
        public IReadOnlyList<QuestStateDecl> States => Core.States;
    }
}
