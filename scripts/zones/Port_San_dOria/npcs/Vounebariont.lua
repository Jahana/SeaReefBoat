-----------------------------------
-- Area: Port San d'Oria
--  NPC: Vounebariont
-- Starts and Finishes Quest: Thick Shells
-----------------------------------
---@type TNpcEntity
local entity = {}

entity.onTrade = function(player, npc, trade)
    xi.cs.quest(player, npc, 'sandoria.thick_shells', 'onTrade', trade)
end

entity.onTrigger = function(player, npc)
    if xi.cs.quest(player, npc, 'sandoria.thick_shells', 'onTrigger') then
        return
    end

    player:startEvent(568)
end

entity.onEventFinish = function(player, csid, option, npc)
    xi.cs.questEvent(player, npc, 'sandoria.thick_shells', csid, option)
end

return entity
