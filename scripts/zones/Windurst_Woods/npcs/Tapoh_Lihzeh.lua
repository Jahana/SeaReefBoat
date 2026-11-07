-----------------------------------
-- Area: Windurst Woods
--  NPC: Tapoh Lihzeh
-- Starts & Finishes Repeatable Quest: Paying Lip Service
-- !pos 51.011 -3.749 54.402 241
-----------------------------------
---@type TNpcEntity
local entity = {}

entity.onTrade = function(player, npc, trade)
    -- CHOCOBILIOUS
    if xi.cs.quest(player, npc, 'windurst.chocobilious', 'onTrade', trade) then
        return

    -- PAYING LIP SERVICE
    else
        xi.cs.quest(player, npc, 'windurst.paying_lip_service', 'onTrade', trade)
    end
end

entity.onTrigger = function(player, npc)
    -- CHOCOBILIOUS
    if xi.cs.quest(player, npc, 'windurst.chocobilious', 'onTrigger') then
        return

    -- PAYING LIP SERVICE
    elseif xi.cs.quest(player, npc, 'windurst.paying_lip_service', 'onTrigger') then
        return

    -- STANDARD DIALOG
    else
        player:startEvent(437)
    end
end

entity.onEventFinish = function(player, csid, option, npc)
    -- CHOCOBILIOUS
    if csid == 227 or csid == 229 then
        xi.cs.questEvent(player, npc, 'windurst.chocobilious', csid, option)

    -- PAYING LIP SERVICE
    elseif csid == 477 or csid == 479 then
        xi.cs.questEvent(player, npc, 'windurst.paying_lip_service', csid, option)
    end
end

return entity
