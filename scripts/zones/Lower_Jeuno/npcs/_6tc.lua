-----------------------------------
-- Area: Lower Jeuno
--  NPC: Door: "Neptune's Spire"
-- Starts and Finishes Quest: Beat Around the Bushin
-- ZM 17 cutscene
-- !pos 35 0 -15 245
-----------------------------------
local ID = zones[xi.zone.LOWER_JEUNO]
-----------------------------------
---@type TNpcEntity
local entity = {}

entity.onTrade = function(player, npc, trade)
    xi.cs.quest(player, npc, 'jeuno.beat_around_the_bushin', 'onTrade', trade)
end

entity.onTrigger = function(player, npc)
    if xi.cs.quest(player, npc, 'jeuno.beat_around_the_bushin', 'onTrigger') then
        return

    elseif player:hasKeyItem(xi.keyItem.TENSHODO_MEMBERS_CARD) then
        player:startEvent(105) -- Open the door

    else
        player:messageSpecial(ID.text.ITS_LOCKED)
    end
end

entity.onEventFinish = function(player, csid, option, npc)
    if csid >= 155 and csid <= 159 then
        xi.cs.questEvent(player, npc, 'jeuno.beat_around_the_bushin', csid, option)
    end
end

return entity
