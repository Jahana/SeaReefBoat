-----------------------------------
-- Area: Caedarva Mire
--  NPC: qm9
-- Involved in quest: The Wayward Automaton
-- !pos  129 1.396 -631 79
-----------------------------------
---@type TNpcEntity
local entity = {}

entity.onTrigger = function(player, npc)
    xi.cs.quest(player, npc, 'ahtUrhgan.the_wayward_automaton', 'onTrigger')
end

entity.onEventFinish = function(player, csid, option, npc)
    if csid == 14 then
        xi.cs.questEvent(player, npc, 'ahtUrhgan.the_wayward_automaton', csid, option)
    end
end

return entity
