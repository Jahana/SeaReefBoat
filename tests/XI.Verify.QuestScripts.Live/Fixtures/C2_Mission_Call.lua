-----------------------------------
-- quest rework C2 Task 4 (tests/XI.Verify.QuestScripts.Live/C2.cs RunC2Calls): a test-only NPC script, never in
-- scripts/, whose mission branches are calls in place. onTrigger returns what xi.cs.mission answered (true when the
-- C# mission acted) and plays 22 when it did not; onEventFinish hands the finish to xi.cs.missionEvent and returns
-- its answer
-----------------------------------
local entity = {}

entity.onTrigger = function(player, npc)
    if xi.cs.mission(player, npc, 'mission.rov.the_endless_sky', 'onTrigger') then
        return true
    end

    player:startEvent(22)
    return false
end

entity.onEventFinish = function(player, csid, option, npc)
    return xi.cs.missionEvent(player, npc, 'mission.rov.the_endless_sky', csid, option)
end

return entity
