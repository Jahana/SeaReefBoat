-----------------------------------
-- Area: Western Adoulin
--  NPC: Jorin
-- Starts, Involved with, and Finishes Quest: 'The Old Man and the Harpoon'
-- !pos 92 32 152 256
-----------------------------------
---@type TNpcEntity
local entity = {}

entity.onTrigger = function(player, npc)
    xi.cs.quest(player, npc, 'adoulin.the_old_man_and_the_harpoon', 'onTrigger')
end

entity.onEventFinish = function(player, csid, option, npc)
    xi.cs.questEvent(player, npc, 'adoulin.the_old_man_and_the_harpoon', csid, option)
end

return entity
