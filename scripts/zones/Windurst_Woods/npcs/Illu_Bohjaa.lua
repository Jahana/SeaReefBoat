-----------------------------------
-- Area: Windurst Woods
--  NPC: Illu Bohjaa
-- Involved in the repeatable quest "Creepy Crawlies"
-----------------------------------
---@type TNpcEntity
local entity = {}

entity.onTrade = function(player, npc, trade)
    xi.cs.quest(player, npc, 'windurst.creepy_crawlies', 'onTrade', trade)
end

entity.onTrigger = function(player, npc)
    xi.cs.quest(player, npc, 'windurst.creepy_crawlies', 'onTrigger')
end

entity.onEventFinish = function(player, csid, option, npc)
    xi.cs.questEvent(player, npc, 'windurst.creepy_crawlies', csid, option)
end

return entity
