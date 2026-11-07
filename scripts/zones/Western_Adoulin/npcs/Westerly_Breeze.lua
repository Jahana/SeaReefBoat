-----------------------------------
-- Area: Western Adoulin
--  NPC: Westerly Breeze
-- !pos 62 32 123 256
-----------------------------------
---@type TNpcEntity
local entity = {}

entity.onTrade = function(player, npc, trade)
    if trade:getItemCount() == 1 and trade:getGil() == 0 then
        local item = trade:getItem(0)
        if not item then
            return
        end

        local itemId = item:getID()
        local ahCategory = item:getAHCat()

        if
            ahCategory >= xi.itemAHCategory.MEAT_EGGS and
            ahCategory <= xi.itemAHCategory.SWEETS
        then
            -- We traded him a food item
            -- All the Way to the Bank: a gruel at ATWTTB_Can_Trade_Gruel 1 (5068)
            xi.cs.quest(player, npc, 'adoulin.all_the_way_to_the_bank', 'onTrade', trade)
        elseif ahCategory == 58 then
            -- Always More Quoth the Ravenous: a drink while accepted (3013 / 3014)
            xi.cs.quest(player, npc, 'adoulin.always_more_quoth_the_ravenous', 'onTrade', trade)
        else
            -- Always More Quoth the Ravenous: the Cursed Beverage while accepted (3012)
            xi.cs.quest(player, npc, 'adoulin.always_more_quoth_the_ravenous', 'onTrade', trade)
        end
    end
end

entity.onTrigger = function(player, npc)
    xi.cs.quest(player, npc, 'adoulin.always_more_quoth_the_ravenous', 'onTrigger')
end

entity.onEventFinish = function(player, csid, option, npc)
    if csid == 5068 then
        -- Trading him gruel after Quest: 'All The Way To The Bank'
        xi.cs.questEvent(player, npc, 'adoulin.all_the_way_to_the_bank', csid, option)
    else
        xi.cs.questEvent(player, npc, 'adoulin.always_more_quoth_the_ravenous', csid, option)
    end
end

return entity
