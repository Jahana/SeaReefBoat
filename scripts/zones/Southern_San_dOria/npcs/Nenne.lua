-----------------------------------
-- Area: Southern San d'Oria
--  NPC: Nenne
-- Starts and Finishes Quest: To Cure a Cough
-- !pos -114 -6 102 230
-----------------------------------
---@type TNpcEntity
local entity = {}

entity.onTrigger = function(player, npc)
    if xi.cs.quest(player, npc, 'sandoria.to_cure_a_cough', 'onTrigger') then
        return
    end

    player:startEvent(584)
end

entity.onEventFinish = function(player, csid, option, npc)
    xi.cs.questEvent(player, npc, 'sandoria.to_cure_a_cough', csid, option)
end

return entity
