-----------------------------------
-- Area: Xarcabard
--  NPC: qm1 (???)
-- Involved in Quests: The Three Magi
-- !pos -331 -29 -49 112
-----------------------------------
---@type TNpcEntity
local entity = {}

entity.onTrade = function(player, npc, trade)
    xi.cs.quest(player, npc, 'windurst.the_three_magi', 'onTrade', trade)
end

entity.onTrigger = function(player, npc)
    xi.cs.quest(player, npc, 'windurst.the_three_magi', 'onTrigger')
end

return entity
