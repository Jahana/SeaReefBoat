-----------------------------------
-- Area: Western Adoulin
--  NPC: Defliaa
-- Involved with Quest: 'All the Way to the Bank'
-- !pos 43 2 -113 256
-----------------------------------
local ID = zones[xi.zone.WESTERN_ADOULIN]
-----------------------------------
---@type TNpcEntity
local entity = {}

entity.onTrade = function(player, npc, trade)
    -- ALL THE WAY TO THE BANK: 5069 for 19440 gil with the invoice, bit 0 clear
    xi.cs.quest(player, npc, 'adoulin.all_the_way_to_the_bank', 'onTrade', trade)
end

entity.onTrigger = function(player, npc)
    player:showText(npc, ID.text.DEFLIAA_SHOP_TEXT)
    local stock =
    {
        { 5166,  3400, }, -- Coeurl Sub
        { 4421,  1560, }, -- Melon Pie
        { 5889, 19440, }, -- Stuffed Pitaru
        { 5885, 18900, }, -- Saltena
        { 4396,   280, }, -- Sausage Roll
        { 4356,   200, }, -- White Bread
        { 5686,   800, }, -- Cheese Sandwich
    }
    xi.shop.general(player, stock)
end

entity.onEventFinish = function(player, csid, option, npc)
    -- ALL THE WAY TO THE BANK
    if csid == 5069 then
        xi.cs.questEvent(player, npc, 'adoulin.all_the_way_to_the_bank', csid, option)
    end
end

return entity
