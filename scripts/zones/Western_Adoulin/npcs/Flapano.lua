-----------------------------------
-- Area: Western Adoulin
--  NPC: Flapno
-- !pos 70 0 -13 256
-----------------------------------
local ID = zones[xi.zone.WESTERN_ADOULIN]
-----------------------------------
---@type TNpcEntity
local entity = {}

entity.onTrade = function(player, npc, trade)
    -- ALL THE WAY TO THE BANK: 5600 gil with the invoice: 5071 with bit 2 clear, else nothing
    if xi.cs.quest(player, npc, 'adoulin.all_the_way_to_the_bank', 'onTrade', trade) then
        return

    -- EXOTIC DELICACIES: 2861 (the ingredients) / 2862 (a paella) while accepted
    else
        xi.cs.quest(player, npc, 'adoulin.exotic_delicacies', 'onTrade', trade)
    end
end

entity.onTrigger = function(player, npc)
    -- THE WEATHERSPOON WAR: 191 while accepted at Weatherspoon_War_Status 6
    if xi.cs.quest(player, npc, 'adoulin.the_weatherspoon_war', 'onTrigger') then
        return
    end

    -- EXOTIC DELICACIES: the offer every other talk (2860 / 2863, Flapano_Odd_Even 1); on the shop's turn it sets the
    -- var 0 and plays nothing
    -- SHOP
    if not xi.cs.quest(player, npc, 'adoulin.exotic_delicacies', 'onTrigger') then
        player:showText(npc, ID.text.FLAPANO_SHOP_TEXT)
        local stock =
        {
            { 5943,   125, }, -- Smoked Mackerel
            { 4415,   124, }, -- Roasted Corn
            { 4434,  5000, }, -- Mushroom Risotto
            { 5145,  5600, }, -- Fish and Chips
            { 4423,   300, }, -- Apple Juice
            { 4405,   160, }, -- Rice Ball
            { 5676, 76475, }, -- Mushroom Saute
        }
        xi.shop.general(player, stock)
    end
end

entity.onEventFinish = function(player, csid, option, npc)
    -- ALL THE WAY TO THE BANK
    if csid == 5071 then
        xi.cs.questEvent(player, npc, 'adoulin.all_the_way_to_the_bank', csid, option)

    -- EXOTIC DELICACIES
    else
        xi.cs.questEvent(player, npc, 'adoulin.exotic_delicacies', csid, option)
    end
end

return entity
