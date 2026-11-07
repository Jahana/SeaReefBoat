-----------------------------------
-- Area: Heaven's Tower
--  NPC: Kupipi
-- Involved in Mission 2-3
-- Involved in Quest: Riding on the Clouds
-- !pos 2 0.1 30 242
-----------------------------------
---@type TNpcEntity
local entity = {}

entity.onTrigger = function(player, npc)
    if xi.cs.quest(player, npc, 'windurst.trust_windurst', 'onTrigger') then
        return
    elseif player:getNation() == xi.nation.WINDURST then
        if player:getRank(player:getNation()) == 10 then
            player:startEvent(408) -- After achieving Windurst Rank 10, Kupipi has more to say
        else
            player:startEvent(251)
        end
    else
        player:startEvent(251)
    end
end

entity.onEventFinish = function(player, csid, option, npc)
    --TRUST
    if csid == 435 or csid == 437 or csid == 439 then
        xi.cs.questEvent(player, npc, 'windurst.trust_windurst', csid, option)
    end
end

return entity
