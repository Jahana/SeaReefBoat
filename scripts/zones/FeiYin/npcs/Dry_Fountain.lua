-----------------------------------
-- Area: FeiYin
--  NPC: Dry Fountain
-- Involved In Quest: Peace for the Spirit
-- !pos -17 -16 71 204
-----------------------------------
local ID = zones[xi.zone.FEIYIN]
-----------------------------------
---@type TNpcEntity
local entity = {}

entity.onTrade = function(player, npc, trade)
    xi.cs.quest(player, npc, 'sandoria.peace_for_the_spirit', 'onTrade', trade)
end

entity.onTrigger = function(player, npc)
    player:messageSpecial(ID.text.DRIED_UP_FOUNTAIN)
end

entity.onEventFinish = function(player, csid, option, npc)
    xi.cs.questEvent(player, npc, 'sandoria.peace_for_the_spirit', csid, option)
end

return entity
