-----------------------------------
-- Area: Western Adoulin
--  NPC: Dewalt
-- Involved with Quests: 'Flavors of our Lives', 'Dont Ever Leaf Me'
--  !pos -23 0 28 256
-----------------------------------
---@type TNpcEntity
local entity = {}

entity.onTrigger = function(player, npc)
    xi.cs.quest(player, npc, 'adoulin.dont_ever_leaf_me', 'onTrigger')
end

entity.onEventFinish = function(player, csid, option, npc)
    xi.cs.questEvent(player, npc, 'adoulin.dont_ever_leaf_me', csid, option)
end

return entity
