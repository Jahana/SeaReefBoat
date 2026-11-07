-----------------------------------
-- Area: Southern San d'Oria
--  NPC: Daggao
-- Involved in Quest: Peace for the Spirit, Lure of the Wildcat (San d'Oria)
-- !pos 89 0 119 230
-----------------------------------
---@type TNpcEntity
local entity = {}

entity.onTrigger = function(player, npc)
    if xi.cs.quest(player, npc, 'sandoria.peace_for_the_spirit', 'onTrigger') then
        return
    end

    player:startEvent(60)
end

entity.onEventFinish = function(player, csid, option, npc)
    xi.cs.questEvent(player, npc, 'sandoria.peace_for_the_spirit', csid, option)
end

return entity
