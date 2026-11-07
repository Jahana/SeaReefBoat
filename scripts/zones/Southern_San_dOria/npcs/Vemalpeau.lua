-----------------------------------
-- Area: Southern San d'Oria
--  NPC: Vemalpeau
-- Involved in Quests: Under Oath
-----------------------------------
---@type TNpcEntity
local entity = {}

entity.onTrigger = function(player, npc)
    if not xi.cs.quest(player, npc, 'sandoria.under_oath', 'onTrigger') then
        player:startEvent(1)
    end
end

entity.onEventFinish = function(player, csid, option, npc)
    xi.cs.questEvent(player, npc, 'sandoria.under_oath', csid, option)
end

return entity
