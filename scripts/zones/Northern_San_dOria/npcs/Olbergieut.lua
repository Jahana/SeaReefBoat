-----------------------------------
-- Area: Northern San d'Oria
--  NPC: Olbergieut
-- !pos 91 0 121 231
-- Starts and Finishes Quest: Gates of Paradise
-----------------------------------
---@type TNpcEntity
local entity = {}

entity.onTrigger = function(player, npc)
    if xi.cs.quest(player, npc, 'sandoria.gates_to_paradise', 'onTrigger') then
        return
    end

    player:startEvent(612)
end

entity.onEventFinish = function(player, csid, option, npc)
    xi.cs.questEvent(player, npc, 'sandoria.gates_to_paradise', csid, option)
end

return entity
