-----------------------------------
-- Area: Bastok Markets (S)
--  NPC: Raginmund
-- Involved in Quest: Too Many Chefs
-- Location L-10
-----------------------------------
---@type TNpcEntity
local entity = {}

entity.onTrigger = function(player, npc)
    if xi.cs.quest(player, npc, 'bastok.too_many_chefs', 'onTrigger') then
        return
    end

    player:startEvent(111) -- standard
end

entity.onEventFinish = function(player, csid, option, npc)
    xi.cs.questEvent(player, npc, 'bastok.too_many_chefs', csid, option)
end

return entity
