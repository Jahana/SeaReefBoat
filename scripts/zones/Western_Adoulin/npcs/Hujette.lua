-----------------------------------
-- Area: Western Adoulin
--  NPC: Hujette
-- Involved with Quest: 'All the Way to the Bank'
-- !pos 35 0 -56 256
-----------------------------------
local ID = zones[xi.zone.WESTERN_ADOULIN]
-----------------------------------
---@type TNpcEntity
local entity = {}

entity.onTrade = function(player, npc, trade)
    -- ALL THE WAY TO THE BANK: 5070 for 3000 gil with the invoice, bit 1 clear
    xi.cs.quest(player, npc, 'adoulin.all_the_way_to_the_bank', 'onTrade', trade)
end

entity.onTrigger = function(player, npc)
    player:showText(npc, ID.text.HUJETTE_SHOP_TEXT)
    local stock =
    {
        { 5941,   20, }, -- Campfire Choco
        { 5940,    8, }, -- Trail Cookie
        { 5942,   20, }, -- Cascade Candy
        { 5775,  544, }, -- Chocolate Crepe
        { 5147, 3000, }, -- Snoll Gelato
    }
    xi.shop.general(player, stock)
end

entity.onEventFinish = function(player, csid, option, npc)
    -- ALL THE WAY TO THE BANK
    if csid == 5070 then
        xi.cs.questEvent(player, npc, 'adoulin.all_the_way_to_the_bank', csid, option)
    end
end

return entity
