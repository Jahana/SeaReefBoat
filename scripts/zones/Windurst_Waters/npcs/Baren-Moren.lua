-----------------------------------
-- Area: Windurst Waters
--  NPC: Baren-Moren
-- Starts and Finishes Quest: Hat in Hand
-- !pos -66 -3 -148 238
-----------------------------------
---@type TNpcEntity
local entity = {}

entity.onTrade = function(player, npc, trade)
    -- A Feather in One's Cap
    xi.cs.quest(player, npc, 'windurst.a_feather_in_ones_cap', 'onTrade', trade)
end

entity.onTrigger = function(player, npc)
    -- Hat in Hand, A Feather in One's Cap (one if-chain in the Lua)
    if
        xi.cs.quest(player, npc, 'windurst.hat_in_hand', 'onTrigger') or
        xi.cs.quest(player, npc, 'windurst.a_feather_in_ones_cap', 'onTrigger')
    then
        return

    -- default dialog
    else
        local rand = math.randomInt(1, 6)

        if rand == 1 then
            player:startEvent(42) -- Standard Conversation 1
        elseif rand == 2 then
            player:startEvent(44) -- Standard Conversation 2
        elseif rand == 3 then
            player:startEvent(45) -- Standard Conversation 3
        elseif rand == 4 then
            player:startEvent(46) -- Standard Conversation 4
        elseif rand == 5 then
            player:startEvent(47) -- Standard Conversation 5
        elseif rand == 6 then
            player:startEvent(1022) -- Standard Conversation 6
        end
    end
end

entity.onEventFinish = function(player, csid, option, npc)
    if csid == 48 or csid == 52 then
        xi.cs.questEvent(player, npc, 'windurst.hat_in_hand', csid, option)
    elseif csid == 75 or csid == 79 then
        xi.cs.questEvent(player, npc, 'windurst.a_feather_in_ones_cap', csid, option)
    end
end

return entity
