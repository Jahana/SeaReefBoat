-----------------------------------
-- quest rework C3 Prep (tests/XI.Verify.QuestScripts.Live/C2.cs): a test-only Lua mission on a constant no Lua file
-- names, so no lane's port moves it. QuestScripts.Live lists it as scripts/missions/cop/QSL_Mission_Fixture
-----------------------------------
local mission = Mission:new(xi.mission.log_id.COP, xi.mission.id.cop.DESCENDANTS_OF_A_LINE_LOST)

mission.reward =
{
    nextMission = { xi.mission.log_id.COP, xi.mission.id.cop.THE_MOTHERCRYSTALS },
}

mission.sections =
{
    {
        check = function(player, currentMission, missionStatus, vars)
            return currentMission == mission.missionId
        end,

        [xi.zone.METALWORKS] =
        {
            ['Naji'] =
            {
                onTrigger = function(player, npc)
                    return mission:progressEvent(1000)
                end,
            },
        },
    },
}

return mission
