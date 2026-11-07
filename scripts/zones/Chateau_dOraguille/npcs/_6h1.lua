-----------------------------------
-- Area: Chateau d'Oraguille
-- Door: Prince Regent's Rm
-- Starts and Finishes Quest: Prelude of Black and White (Start), Pieuje's Decision (Start)
-- !pos -37 -3 31 233
-----------------------------------
---@type TNpcEntity
local entity = {}

entity.onTrigger = function(player, npc)
    -- WHM AF quests
    if
        player:getMainJob() == xi.job.WHM and
        player:getMainLvl() >= xi.settings.main.AF2_QUEST_LEVEL
    then
        xi.cs.quest(player, npc, 'sandoria.pieujes_decision', 'onTrigger')

    -- San d'Oria Rank 10 (new default)
    elseif
        player:getNation() == xi.nation.SANDORIA and
        player:getRank(player:getNation()) == 10
    then
        player:startEvent(73)

    -- Default dialogue
    else
        player:startEvent(523)
    end
end

entity.onEventFinish = function(player, csid, option, npc)
    xi.cs.questEvent(player, npc, 'sandoria.pieujes_decision', csid, option)
end

return entity
