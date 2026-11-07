-----------------------------------
-- Area: Western Adoulin
--  NPC: Mastan
--  Involved with Quests: 'Order Up'
--                        'The Curious Case of Melvien'
-- !pos -9 0 67 256
-----------------------------------
---@type TNpcEntity
local entity = {}

entity.onTrigger = function(player, npc)
    -- Order Up first (70), then The Curious Case of Melvien (184)
    if not xi.cs.quest(player, npc, 'adoulin.order_up', 'onTrigger') then
        xi.cs.quest(player, npc, 'adoulin.the_curious_case_of_melvien', 'onTrigger')
    end
end

entity.onEventFinish = function(player, csid, option, npc)
    if csid == 70 then
        xi.cs.questEvent(player, npc, 'adoulin.order_up', csid, option)
    elseif csid == 184 then
        xi.cs.questEvent(player, npc, 'adoulin.the_curious_case_of_melvien', csid, option)
    end
end

return entity
