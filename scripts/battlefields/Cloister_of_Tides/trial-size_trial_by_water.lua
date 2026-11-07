-----------------------------------
-- Area: Cloister of Tides
-- BCNM: Trial-size Trial by Water
-----------------------------------

local content = BattlefieldQuest:new({
    zoneId           = xi.zone.CLOISTER_OF_TIDES,
    battlefieldId    = xi.battlefield.id.TRIAL_SIZE_TRIAL_BY_WATER,
    canLoseExp       = false,
    maxPlayers       = 1,
    levelCap         = 20,
    timeLimit        = utils.minutes(15),
    index            = 1,
    entryNpc         = 'WP_Entrance',
    exitNpc          = 'Water_Protocrystal',
    requiredItems    = { xi.item.MINI_TUNING_FORK_OF_WATER },

    questArea = xi.questLog.OUTLANDS,
    quest     = xi.quest.id.outlands.TRIAL_SIZE_TRIAL_BY_WATER,
})

function content:entryRequirement(player, npc, isRegistrant, trade)
    return player:getMainJob() == xi.job.SMN and
        player:getMainLvl() >= 20
end

function content:onEventFinishWin(player, csid, option, npc)
    xi.cs.questEvent(player, npc, 'outlands.trial_size_trial_by_water', csid, option)
end

content.groups =
{
    {
        mobs = { 'Leviathan_Prime_TSTBW' },
        allDeath = function(battlefield, mob)
            battlefield:setStatus(xi.battlefield.status.WON)
        end,
    },
}

return content:register()
