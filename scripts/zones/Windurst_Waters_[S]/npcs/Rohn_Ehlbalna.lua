-----------------------------------
-- Area: Windurst Waters (S)
--  NPC: Rohn Ehlbalna
-- !pos -43.473 -4.5 46.496 94
-----------------------------------
---@type TNpcEntity
local entity = {}

entity.onTrigger = function(player, npc)
    if xi.cs.quest(player, npc, 'crystalWar.redeeming_rocks', 'onTrigger') then
        return
    end

    player:startEvent(440)
end

entity.onEventFinish = function(player, csid, option, npc)
    xi.cs.questEvent(player, npc, 'crystalWar.redeeming_rocks', csid, option)
end

return entity
