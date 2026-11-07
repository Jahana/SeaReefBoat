-----------------------------------
-- Area: Windurst Woods
--  NPC: Umumu
--  Involved In Quest: Making Headlines
-- !pos 32.575 -5.250 141.372 241
-----------------------------------
---@type TNpcEntity
local entity = {}

entity.onTrigger = function(player, npc)
    if not xi.cs.quest(player, npc, 'windurst.making_headlines', 'onTrigger') then
        player:startEvent(414) -- Standard Conversation
    end
end

entity.onEventFinish = function(player, csid, option, npc)
    xi.cs.questEvent(player, npc, 'windurst.making_headlines', csid, option)
end

return entity
