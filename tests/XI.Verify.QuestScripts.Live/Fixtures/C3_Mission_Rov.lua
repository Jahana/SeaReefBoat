-----------------------------------
-- quest rework C3 Prep (tests/XI.Verify.QuestScripts.Live/C2.cs): a test-only Lua mission on a constant no Lua file
-- names, so no lane's port moves it. QuestScripts.Live lists it as scripts/missions/rov/QSL_Mission_Fixture
-----------------------------------
local mission = Mission:new(xi.mission.log_id.ROV, xi.mission.id.rov.THE_ENDLESS_SKY)

mission.reward =
{
    rankPoints = 400,
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
