-----------------------------------
-- Area: Nashmau
--  NPC: Dnegan
-- Involved in quest: Wayward Automaton
-- !pos 29.89 -6 55.83 53
-----------------------------------
---@type TNpcEntity
local entity = {}

entity.onTrigger = function(player, npc)
    -- Quest: The WayWard Automaton
    if xi.cs.quest(player, npc, 'ahtUrhgan.the_wayward_automaton', 'onTrigger') then
        return

    -- Quest: Operation Teatime
    elseif xi.cs.quest(player, npc, 'ahtUrhgan.operation_teatime', 'onTrigger') then
        return
    else
        player:startEvent(288)
    end
end

entity.onEventFinish = function(player, csid, option, npc)
    if csid == 289 then
        xi.cs.questEvent(player, npc, 'ahtUrhgan.the_wayward_automaton', csid, option)
    elseif csid == 290 then
        xi.cs.questEvent(player, npc, 'ahtUrhgan.operation_teatime', csid, option)
    end
end

return entity
