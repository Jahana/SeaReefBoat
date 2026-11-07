-----------------------------------
-- Area: Windurst Woods
--  NPC: Kopuro-Popuro
-- !pos -0.037 -4.749 -22.589 241
-- Starts Quests: The All-New C-2000, Legendary Plan B, The All-New C-3000
-- Involved in quests: Lost Chick
-----------------------------------
---@type TNpcEntity
local entity = {}

entity.onTrade = function(player, npc, trade)
    -- THE ALL NEW C-2000
    if xi.cs.quest(player, npc, 'windurst.the_all_new_c_2000', 'onTrade', trade) then
        return

    -- LEGENDARY PLAN B
    elseif xi.cs.quest(player, npc, 'windurst.legendary_plan_b', 'onTrade', trade) then
        return

    -- THE ALL NEW C-3000
    else
        xi.cs.quest(player, npc, 'windurst.the_all_new_c_3000', 'onTrade', trade)
    end
end

entity.onTrigger = function(player, npc)
    -- THE ALL NEW C-3000
    if xi.cs.quest(player, npc, 'windurst.the_all_new_c_3000', 'onTrigger') then
        return

    -- A GREETING CARDIAN (301) and LEGENDARY PLAN B
    elseif xi.cs.quest(player, npc, 'windurst.legendary_plan_b', 'onTrigger') then
        return

    -- THE ALL NEW C-2000
    elseif xi.cs.quest(player, npc, 'windurst.the_all_new_c_2000', 'onTrigger') then
        return

    -- STANDARD DIALOG
    else
        player:startEvent(276)
    end
end

entity.onEventFinish = function(player, csid, option, npc)
    -- THE ALL NEW C-2000
    if csid == 285 or csid == 292 then
        xi.cs.questEvent(player, npc, 'windurst.the_all_new_c_2000', csid, option)

    -- LEGENDARY PLAN B
    elseif csid == 308 or csid == 314 then
        xi.cs.questEvent(player, npc, 'windurst.legendary_plan_b', csid, option)

    -- THE ALL NEW C-3000
    elseif csid == 655 or csid == 657 then
        xi.cs.questEvent(player, npc, 'windurst.the_all_new_c_3000', csid, option)
    end
end

return entity
