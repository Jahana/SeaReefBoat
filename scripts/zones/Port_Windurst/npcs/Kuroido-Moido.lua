-----------------------------------
-- Area: Port Windurst (240)
--  NPC: Kuriodo-Moido
-- Involved In Quest: Making Amends, Wonder Wands,
-- Starts and Finishes: Making Amens!, Orastery Woes
-- !pos -112.5 -4.2 102.9 240
-----------------------------------
---@type TNpcEntity
local entity = {}

entity.onTrigger = function(player, npc)
    -- Making Amens! (and its Wonder Wands lines)
    if xi.cs.quest(player, npc, 'windurst.making_amens', 'onTrigger') then
        return
    else
        local rand = math.randomInt(1, 2)
        if rand == 1 then
            player:startEvent(225)   -- Standard Conversation
        else
            player:startEvent(226)   -- Standard Conversation
        end
    end
end

entity.onEventFinish = function(player, csid, option, npc)
    xi.cs.questEvent(player, npc, 'windurst.making_amens', csid, option)
end

return entity
