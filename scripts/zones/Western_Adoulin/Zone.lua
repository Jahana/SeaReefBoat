-----------------------------------
-- Zone: Western Adoulin
-----------------------------------
---@type TZone
local zoneObject = {}

zoneObject.onInitialize = function(zone)
    zone:registerCuboidTriggerArea(1, 13, 0, 123.518, 28, 0, 173) -- Approaching Airship Docks
end

zoneObject.onZoneIn = function(player, prevZone)
    local heartwingsAndTheKindhearted = player:getCurrentMission(xi.mission.log_id.SOA) == xi.mission.id.soa.HEARTWINGS_AND_THE_KINDHEARTED

    -- Raptor Rapture: 5056 at Raptor_Rapture_Status 2
    local raptorRaptureCs = xi.cs.questZoneIn(player, 'adoulin.raptor_rapture', prevZone)
    if raptorRaptureCs then
        return raptorRaptureCs
    end

    if heartwingsAndTheKindhearted then
        return 2
    end

    return xi.moghouse.onMoghouseZoneEvent(player, prevZone)
end

zoneObject.onTriggerAreaEnter = function(player, triggerArea)
end

zoneObject.onEventUpdate = function(player, csid, option, npc)
end

zoneObject.onEventFinish = function(player, csid, option, npc)
    if csid == 5056 then
        -- Raptor Rapture: the introduction's end (status 3; option 1 begins it, status 4)
        xi.cs.questEvent(player, nil, 'adoulin.raptor_rapture', csid, option)
    elseif csid == 2 then
        player:completeMission(xi.mission.log_id.SOA, xi.mission.id.soa.HEARTWINGS_AND_THE_KINDHEARTED)
        player:addMission(xi.mission.log_id.SOA, xi.mission.id.soa.PIONEER_REGISTRATION)
    end
end

return zoneObject
