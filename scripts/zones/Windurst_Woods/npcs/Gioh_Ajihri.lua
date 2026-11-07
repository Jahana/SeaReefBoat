-----------------------------------
-- Area: Windurst Woods
--  NPC: Gioh Ajihri
-- Starts & Finishes Repeatable Quest: Twinstone Bonding
-----------------------------------
---@type TNpcEntity
local entity = {}

entity.onTrade = function(player, npc, trade)
    xi.cs.quest(player, npc, 'windurst.twinstone_bonding', 'onTrade', trade)
end

entity.onTrigger = function(player, npc)
    if not xi.cs.quest(player, npc, 'windurst.twinstone_bonding', 'onTrigger') then
        player:startEvent(424)
    end
end

entity.onEventFinish = function(player, csid, option, npc)
    xi.cs.questEvent(player, npc, 'windurst.twinstone_bonding', csid, option)
end

return entity
