-----------------------------------
-- Area: Windurst Walls
--  Location: X:-91  Y:-9  Z:109
--  NPC: Zayhi-Bauhi
--  Starts and Finishes Quest: To Bee or Not to Bee?
-----------------------------------
---@type TNpcEntity
local entity = {}

entity.onTrade = function(player, npc, trade)
    xi.cs.quest(player, npc, 'windurst.to_bee_or_not_to_bee', 'onTrade', trade)
end

entity.onTrigger = function(player, npc)
    if not xi.cs.quest(player, npc, 'windurst.to_bee_or_not_to_bee', 'onTrigger') then
        player:startEvent(299) -- Normal speech
    end
end

--        Event ID List for NPC
--      player:startEvent(299) -- Normal speach
--      player:startEvent(61) -- Normal speach
--      player:startEvent(64) -- Start quest "Too Bee or Not Too Be" (Speech given with lots of coughing)
--      player:startEvent(69) -- After Honey#1: Clearing throat
--      player:startEvent(70) -- After Honey#2: Tries to speak again... coughs
--      player:startEvent(73) -- After Honey#3: Tries to speak again... coughs..asked for more Honey
--      player:startEvent(74) -- After Honey#4: Feels like its getting a lot better but there is still iritaion
--      player:startEvent(75) -- After Honey#5: ToBee quest Finish (tooth hurts from all the Honey)
--      player:startEvent(78) -- ToBee After Quest Finish but before zone (tooth still hurts)

entity.onEventFinish = function(player, csid, option, npc)
    xi.cs.questEvent(player, npc, 'windurst.to_bee_or_not_to_bee', csid, option)
end

return entity
