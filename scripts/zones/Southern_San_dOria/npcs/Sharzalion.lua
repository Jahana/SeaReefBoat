-----------------------------------
-- Area: Southern San d'Oria
--  NPC: Sharzalion
-- !pos 95 0 111 230
-----------------------------------
---@type TNpcEntity
local entity = {}

entity.onTrigger = function(player, npc)
    if xi.cs.quest(player, npc, 'sandoria.peace_for_the_spirit', 'onTrigger') then
        return
    end

    player:startEvent(15)
end

entity.onEventFinish = function(player, csid, option, npc)
    xi.cs.questEvent(player, npc, 'sandoria.peace_for_the_spirit', csid, option)
end

return entity
