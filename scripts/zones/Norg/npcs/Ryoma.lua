-----------------------------------
-- Area: Norg
--  NPC: Ryoma
-- Start and Finish Quest: 20 in Pirate Years, True Will
-- !pos -23 0 -9 252
-----------------------------------
---@type TNpcEntity
local entity = {}

entity.onTrigger = function(player, npc)
    if xi.cs.quest(player, npc, 'outlands.twenty_in_pirate_years', 'onTrigger') then
        return
    end

    xi.cs.quest(player, npc, 'outlands.true_will', 'onTrigger')
end

entity.onEventFinish = function(player, csid, option, npc)
    if csid == 133 or csid == 134 then
        xi.cs.questEvent(player, npc, 'outlands.twenty_in_pirate_years', csid, option)
    elseif csid == 136 or csid == 137 then
        xi.cs.questEvent(player, npc, 'outlands.true_will', csid, option)
    end
end

return entity
