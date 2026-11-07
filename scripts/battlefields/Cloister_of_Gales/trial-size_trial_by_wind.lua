-----------------------------------
-- Area: Cloister of Gales
-- BCNM: Trial-size Trial by Wind
-----------------------------------

local content = BattlefieldQuest:new({
    zoneId           = xi.zone.CLOISTER_OF_GALES,
    battlefieldId    = xi.battlefield.id.TRIAL_SIZE_TRIAL_BY_WIND,
    canLoseExp       = false,
    maxPlayers       = 1,
    levelCap         = 20,
    timeLimit        = utils.minutes(15),
    index            = 2,
    entryNpc         = 'WP_Entrance',
    exitNpc          = 'Wind_Protocrystal',
    requiredItems    = { xi.item.MINI_TUNING_FORK_OF_WIND },

    questArea = xi.questLog.OUTLANDS,
    quest     = xi.quest.id.outlands.TRIAL_SIZE_TRIAL_BY_WIND,
})

function content:entryRequirement(player, npc, isRegistrant, trade)
    return player:getMainJob() == xi.job.SMN and
        player:getMainLvl() >= 20
end

function content:onEventFinishWin(player, csid, option, npc)
    xi.cs.questEvent(player, npc, 'outlands.trial_size_trial_by_wind', csid, option)
end

content.groups =
{
    {
        mobs = { 'Garuda_Prime_TSTBW' },
        allDeath = function(battlefield, mob)
            battlefield:setStatus(xi.battlefield.status.WON)
        end,
    },
}

return content:register()
