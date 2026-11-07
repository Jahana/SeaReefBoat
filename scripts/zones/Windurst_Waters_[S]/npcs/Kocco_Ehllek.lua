-----------------------------------
-- Area: Windurst Waters (S)
--  NPC: Kocco Ehllek
-- Type: Quest Giver
-- !pos -41.465 -2.125 -163.018 94
-----------------------------------
---@type TNpcEntity
local entity = {}

entity.onTrigger = function(player, npc)
    if xi.cs.quest(player, npc, 'crystalWar.redeeming_rocks', 'onTrigger') then
        return
    end

    player:startEvent(140) -- Standard text
end

entity.onEventFinish = function(player, csid, option, npc)
    xi.cs.questEvent(player, npc, 'crystalWar.redeeming_rocks', csid, option)
end

return entity
