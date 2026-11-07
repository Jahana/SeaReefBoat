-----------------------------------
-- Area: Cloister of Frost
-- BCNM: Trial-size Trial by Ice
-----------------------------------

local content = BattlefieldQuest:new({
    zoneId           = xi.zone.CLOISTER_OF_FROST,
    battlefieldId    = xi.battlefield.id.TRIAL_SIZE_TRIAL_BY_ICE,
    canLoseExp       = false,
    maxPlayers       = 1,
    levelCap         = 20,
    timeLimit        = utils.minutes(15),
    index            = 2,
    entryNpc         = 'IP_Entrance',
    exitNpc          = 'Ice_Protocrystal',
    requiredItems    = { xi.item.MINI_TUNING_FORK_OF_ICE },

    questArea = xi.questLog.SANDORIA,
    quest     = xi.quest.id.sandoria.TRIAL_SIZE_TRIAL_BY_ICE,
})

function content:entryRequirement(player, npc, isRegistrant, trade)
    return player:getMainJob() == xi.job.SMN and
        player:getMainLvl() >= 20
end

function content:onEventFinishWin(player, csid, option, npc)
    xi.cs.questEvent(player, npc, 'sandoria.trial_size_trial_by_ice', csid, option)
end

content.groups =
{
    {
        mobs = { 'Shiva_Prime_TSTBI' },
        allDeath = function(battlefield, mob)
            battlefield:setStatus(xi.battlefield.status.WON)
        end,
    },
}

return content:register()
