-----------------------------------
-- Area: Cloister of Storms
-- BCNM: Trial by Lightning
-----------------------------------

local content = BattlefieldQuest:new({
    zoneId           = xi.zone.CLOISTER_OF_STORMS,
    battlefieldId    = xi.battlefield.id.TRIAL_BY_LIGHTNING,
    canLoseExp       = false,
    maxPlayers       = 6,
    timeLimit        = utils.minutes(30),
    index            = 0,
    entryNpc         = 'LP_Entrance',
    exitNpc          = 'Lightning_Protocrystal',
    requiredKeyItems = { xi.keyItem.TUNING_FORK_OF_LIGHTNING },

    questArea = xi.questLog.OTHER_AREAS,
    quest     = xi.quest.id.otherAreas.TRIAL_BY_LIGHTNING,
})

function content:onEventFinishWin(player, csid, option, npc)
    xi.cs.questEvent(player, npc, 'otherAreas.trial_by_lightning', csid, option)
end

content.groups =
{
    {
        mobs = { 'Ramuh_Prime_TBL' },
        allDeath = function(battlefield, mob)
            battlefield:setStatus(xi.battlefield.status.WON)
        end,
    },
}

return content:register()
