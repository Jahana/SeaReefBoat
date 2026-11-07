-----------------------------------
-- Area: Aht Urhgan Whitegate
--  NPC: Iruki-Waraki
--  Involved in quest: No Strings Attached
-- !pos 101.329 -6.999 -29.042 50
-----------------------------------
---@type TNpcEntity
local entity = {}

entity.onTrade = function(player, npc, trade)
    xi.cs.quest(player, npc, 'ahtUrhgan.operation_teatime', 'onTrade', trade)
end

entity.onTrigger = function(player, npc)
    local noStringsAttached = player:getQuestStatus(xi.questLog.AHT_URHGAN, xi.quest.id.ahtUrhgan.NO_STRINGS_ATTACHED)
    local playerJob = player:getMainJob()

    --Quest: The Wayward Automaton
    if xi.cs.quest(player, npc, 'ahtUrhgan.the_wayward_automaton', 'onTrigger') then
        return
    elseif
        playerJob ~= xi.job.PUP and
        noStringsAttached == xi.questStatus.QUEST_COMPLETED
    then
        player:startEvent(267) -- asking you how are you doing with your automaton

    --Quest: Operation teatime
    elseif xi.cs.quest(player, npc, 'ahtUrhgan.operation_teatime', 'onTrigger') then
        return
    end
end

entity.onEventFinish = function(player, csid, option, npc)
    if csid == 774 or csid == 776 then
        xi.cs.questEvent(player, npc, 'ahtUrhgan.the_wayward_automaton', csid, option)
    elseif csid == 778 or csid == 780 then
        xi.cs.questEvent(player, npc, 'ahtUrhgan.operation_teatime', csid, option)
    end
end

return entity
