-----------------------------------
-- Area: Cloister of Storms
-- BCNM: Trial-size Trial by Lightning
-----------------------------------

local content = BattlefieldQuest:new({
    zoneId           = xi.zone.CLOISTER_OF_STORMS,
    battlefieldId    = xi.battlefield.id.TRIAL_SIZE_TRIAL_BY_LIGHTNING,
    canLoseExp       = false,
    maxPlayers       = 1,
    levelCap         = 20,
    timeLimit        = utils.minutes(15),
    index            = 2,
    entryNpc         = 'LP_Entrance',
    exitNpc          = 'Lightning_Protocrystal',
    requiredItems    = { xi.item.MINI_TUNING_FORK_OF_LIGHTNING },

    questArea = xi.questLog.OTHER_AREAS,
    quest     = xi.quest.id.otherAreas.TRIAL_SIZE_TRIAL_BY_LIGHTNING,
})

function content:entryRequirement(player, npc, isRegistrant, trade)
    return player:getMainJob() == xi.job.SMN and
        player:getMainLvl() >= 20
end

function content:onEventFinishWin(player, csid, option, npc)
    xi.cs.questEvent(player, npc, 'otherAreas.trial_size_trial_by_lightning', csid, option)
end

content.groups =
{
    {
        mobs = { 'Ramuh_Prime_TSTBL' },
        allDeath = function(battlefield, mob)
            battlefield:setStatus(xi.battlefield.status.WON)
        end,
    },
}

return content:register()
