-----------------------------------
-- Area: Windurst Waters
--  NPC: Mashuu-Ajuu
-- Starts and Finished Quest: Reap What You Sow
-- Involved in Quest: Making the Grade
-- !pos 129 -6 167 238
-----------------------------------
---@type TNpcEntity
local entity = {}

entity.onTrade = function(player, npc, trade)
    xi.cs.quest(player, npc, 'windurst.reap_what_you_sow', 'onTrade', trade)
end

entity.onTrigger = function(player, npc)
    xi.cs.quest(player, npc, 'windurst.reap_what_you_sow', 'onTrigger')
end

entity.onEventFinish = function(player, csid, option, npc)
    xi.cs.questEvent(player, npc, 'windurst.reap_what_you_sow', csid, option)
end

return entity
