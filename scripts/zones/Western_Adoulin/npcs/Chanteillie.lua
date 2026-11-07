-----------------------------------
-- Area: Western Adoulin
--  NPC: Chanteillie
-- Involved with Quests: 'Do Not Go Into the Light'
--                       'Vegetable Vegetable Crisis'
-- !pos 89 0 -75 256
-----------------------------------
---@type TNpcEntity
local entity = {}

entity.onTrade = function(player, npc, trade)
    -- DO NOT GO INTO THE LIGHT (5076), then VEGETABLE VEGETABLE CRISIS (5089)
    if not xi.cs.quest(player, npc, 'adoulin.do_not_go_into_the_light', 'onTrade', trade) then
        xi.cs.quest(player, npc, 'adoulin.vegetable_vegetable_crisis', 'onTrade', trade)
    end
end

entity.onTrigger = function(player, npc)
    -- DO NOT GO INTO THE LIGHT (5077), then VEGETABLE VEGETABLE CRISIS (5088)
    if not xi.cs.quest(player, npc, 'adoulin.do_not_go_into_the_light', 'onTrigger') then
        xi.cs.quest(player, npc, 'adoulin.vegetable_vegetable_crisis', 'onTrigger')
    end
end

entity.onEventFinish = function(player, csid, option, npc)
    if csid == 5076 then
        xi.cs.questEvent(player, npc, 'adoulin.do_not_go_into_the_light', csid, option)
    elseif csid == 5089 then
        xi.cs.questEvent(player, npc, 'adoulin.vegetable_vegetable_crisis', csid, option)
    end
end

return entity
