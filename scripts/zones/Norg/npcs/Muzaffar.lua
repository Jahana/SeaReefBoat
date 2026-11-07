-----------------------------------
-- Area: Norg
--  NPC: Muzaffar
-- Quests: Black Market
-- !pos 16.678, -2.044, -14.600 252
-----------------------------------
---@type TNpcEntity
local entity = {}

entity.onTrade = function(player, npc, trade)
    xi.cs.quest(player, npc, 'outlands.black_market', 'onTrade', trade)
end

entity.onTrigger = function(player, npc)
    xi.cs.quest(player, npc, 'outlands.black_market', 'onTrigger')
end

entity.onEventFinish = function(player, csid, option, npc)
    xi.cs.questEvent(player, npc, 'outlands.black_market', csid, option)
end

return entity
