-----------------------------------
-- Area: Upper Jeuno
--  NPC: Monberaux
-- Starts and Finishes Quest: The Lost Cardian (finish), The kind cardian (start)
-- Involved in Quests: Save the Clock Tower
-- Involved in handling his how his alter ego reacts in combat.
-- !pos -43 0 -1 244
-----------------------------------
---@type TNpcEntity
local entity = {}

entity.onTrade = function(player, npc, trade)
    local minimumGil = player:getGil() * 0.1
    local elixirTotal = 2

    local finalElixir = player:getCharVar('finalElixir')
    local monbAoe = player:getCharVar('monbAoe')
    local gilAmount = trade:getGil()
    local elixirType = trade:getItemId(0)

    if minimumGil > 100000 then
        minimumGil = 100000
    elseif minimumGil < 10000 then
        minimumGil = 10000
    end

    -- Trust Interaction
    if
        xi.settings.main.ENABLE_SOA == 1 and
        player:hasSpell(xi.magic.spell.MONBERAUX)
    then
        -- Check trade for elixir/hi-elixir
        -- TODO: add logic to trade more than 1 at a time, or more than 1 type at a time.
        if
            finalElixir < elixirTotal and
            (trade:hasItemQty(xi.item.ELIXIR, 1) or trade:hasItemQty(xi.item.HI_ELIXIR, 1))
        then
            player:startEvent(10243, elixirType, 1, 2, 0, 59615134, 7271819, 4095, 128)
        elseif
            (trade:hasItemQty(xi.item.ELIXIR, 1) or
            trade:hasItemQty(xi.item.HI_ELIXIR, 1)) and
            finalElixir >= elixirTotal
        then
            player:startEvent(10246, elixirType)
        end

        if
            gilAmount >= minimumGil and
            monbAoe == 0
        then
            player:startEvent(10238)
        elseif
            gilAmount >= minimumGil and
            monbAoe == 1
        then
            player:startEvent(10240)
        end
    end
end

entity.onTrigger = function(player, npc)
    local elixirTotal = 2
    local minimumGil = player:getGil() * 0.1

    if minimumGil > 100000 then
        minimumGil = 100000
    elseif minimumGil < 10000 then
        minimumGil = 10000
    end

    if not player:hasCompletedUniqueEvent(xi.uniqueEvent.MONBERAUX_INTRODUCTION) then
        player:startEvent(109)
    end

    -- The Lost Cardian, then The Kind Cardian (one if-chain in the Lua)
    if not xi.cs.quest(player, npc, 'jeuno.the_lost_cardian', 'onTrigger') then
        xi.cs.quest(player, npc, 'jeuno.the_kind_cardian', 'onTrigger')
    end

    -- Trust Interaction
    if
        xi.settings.main.ENABLE_SOA == 1 and
        player:hasSpell(xi.magic.spell.MONBERAUX)
    then
        if player:getCharVar('monbAoe') == 0 then
            player:startEvent(10237, minimumGil, 1, 2964, 3300, 58195966, 3881063, 4480, 128)
        else
            player:startEvent(10239)
        end

        if player:getCharVar('finalElixir') <= elixirTotal then
            player:startEvent(10241)
        else
            player:startEvent(10244)
        end
    end

    -- Intentionally split up to not lock the player out of the above interactions while still forcing your introduction to Monberaux to happen.
    player:startEvent(28)
end

entity.onEventFinish = function(player, csid, option, npc)
    local finalElixir = player:getCharVar('finalElixir')

    if csid == 33 or csid == 34 then
        xi.cs.questEvent(player, npc, 'jeuno.the_lost_cardian', csid, option)
    end

    if csid == 10238 then
        player:setCharVar('monbAoe', 1, NextJstWeek())
        player:tradeComplete()
    end

    if csid == 10243 then
        player:setCharVar('finalElixir', finalElixir + 1)
        player:tradeComplete()
    end

    if csid == 109 then
        player:setUniqueEvent(xi.uniqueEvent.MONBERAUX_INTRODUCTION)
    end
end

return entity
