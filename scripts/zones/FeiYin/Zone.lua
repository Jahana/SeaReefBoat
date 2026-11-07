-----------------------------------
-- Zone: FeiYin (204)
-----------------------------------
---@type TZone
local zoneObject = {}

zoneObject.onInitialize = function(zone)
    xi.treasure.initZone(zone)
end

zoneObject.onZoneIn = function(player, prevZone)
    local cs = -1

    if
        player:getXPos() == 0 and
        player:getYPos() == 0 and
        player:getZPos() == 0
    then
        player:setPos(-180, -24, -187, 198)
    end

    xi.cs.questZoneIn(player, 'sandoria.peace_for_the_spirit', prevZone) -- RDM AF: Miser Murphy, no cutscene

    if player:getCurrentMission(xi.mission.log_id.ACP) == xi.mission.id.acp.THOSE_WHO_LURK_IN_SHADOWS_I then
        cs = 29
    else
        cs = xi.cs.questZoneIn(player, 'sandoria.pieujes_decision', prevZone) or cs -- WHM AF
    end

    return cs
end

zoneObject.onConquestUpdate = function(zone, updatetype, influence, owner, ranking, isConquestAlliance)
    xi.conquest.onConquestUpdate(zone, updatetype, influence, owner, ranking, isConquestAlliance)
end

zoneObject.onTriggerAreaEnter = function(player, triggerArea)
end

zoneObject.onEventUpdate = function(player, csid, option, npc)
end

zoneObject.onEventFinish = function(player, csid, option, npc)
    if csid == 19 then
        xi.cs.questEvent(player, npc, 'sandoria.pieujes_decision', csid, option)
    elseif csid == 29 then
        player:completeMission(xi.mission.log_id.ACP, xi.mission.id.acp.THOSE_WHO_LURK_IN_SHADOWS_I)
        player:addMission(xi.mission.log_id.ACP, xi.mission.id.acp.THOSE_WHO_LURK_IN_SHADOWS_II)
    end
end

return zoneObject
