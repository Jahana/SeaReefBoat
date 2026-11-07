-----------------------------------
-- Area: Windurst Walls
--  Location: X:-92  Y:-9  Z:107
--  NPC: Rutango-Botango
--  Involved in Quest: To Bee or Not to Bee?
-----------------------------------
---@type TNpcEntity
local entity = {}

entity.onTrigger = function(player, npc)
    if not xi.cs.quest(player, npc, 'windurst.to_bee_or_not_to_bee', 'onTrigger') then
        player:startEvent(297) -- Standard Conversation
    end
end

-- CS/Event ID List for NPC
-- *Rutango-Botango    CS 443 - player:startEvent(443) -- Long Star Sybil CS
-- Rutango-Botango    CS 297 - player:startEvent(297) -- Standard Conversation
-- *Rutango-Botango    CS 64 - player:startEvent(64) -- Zayhi Coughing
-- Rutango-Botango    CS 65 - player:startEvent(65) -- During Too Bee quest before honey given to Zayhi:  "Oh Crumb...lost his voice"
-- Rutango-Botango    CS 71 - player:startEvent(71) -- During Too Bee quest after some honey was given to Zayhi: "lap up more honey"
-- *Rutango-Botango    CS 75 - player:startEvent(75) -- Combo CS: During Too Bee quest, kicked off from Zayhi
-- Rutango-Botango    CS 76 - player:startEvent(76) -- After Too Bee quest but before zone: "master let me speak for you"

return entity
