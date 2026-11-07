-----------------------------------
-- Area: Windurst Walls
--  Location: X:-81  Y:-9  Z:103
--  NPC: Raamimi
--  Involved in Quest: To Bee or Not to Bee?
-----------------------------------
---@type TNpcEntity
local entity = {}

entity.onTrigger = function(player, npc)
    if not xi.cs.quest(player, npc, 'windurst.to_bee_or_not_to_bee', 'onTrigger') then
        player:startEvent(296)
    end
end

-- Event ID List for NPC
--  player:startEvent(296) -- Standard Conversation
--  player:startEvent(67) -- Quest is kicked off already, he gives you honey
-- player:startEvent(68) -- After honey is given to player...... before given to Zayhi????
--  player:startEvent(80) -- Quest Finish - Gives Mulsum
--  player:startEvent(79) -- After Quest but before zoning: "it's certainly gotten quiet around here..."

entity.onEventFinish = function(player, csid, option, npc)
    xi.cs.questEvent(player, npc, 'windurst.to_bee_or_not_to_bee', csid, option)
end

return entity
