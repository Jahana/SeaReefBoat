-----------------------------------
-- Holy Crest
-- Ghelsba Outpost DRG quest battlefield
-- !pos -162 -11 78 140
-----------------------------------
local ghelsbaID = zones[xi.zone.GHELSBA_OUTPOST]
-----------------------------------

local content = BattlefieldQuest:new({
    zoneId        = xi.zone.GHELSBA_OUTPOST,
    battlefieldId = xi.battlefield.id.HOLY_CREST,
    maxPlayers    = 6,
    timeLimit     = utils.minutes(15),
    index         = 1,
    area          = 1,
    entryNpc      = 'Hut_Door',
    questArea     = xi.questLog.SANDORIA,
    quest         = xi.quest.id.sandoria.THE_HOLY_CREST,
})

-- Players must be on the quest with the key item or have completed the quest to enter the battlefield.
function content:entryRequirement(player, npc, isRegistrant, trade)
    local questStatus = player:getQuestStatus(xi.questLog.SANDORIA, xi.quest.id.sandoria.THE_HOLY_CREST)

    if questStatus == xi.questStatus.QUEST_COMPLETED then
        return true
    end

    return player:hasKeyItem(xi.keyItem.DRAGON_CURSE_REMEDY)
end

function content:onEventFinishWin(player, csid, option, npc)
    xi.cs.questEvent(player, npc, 'sandoria.the_holy_crest', csid, option)
end

content.groups =
{
    {
        mobIds =
        {
            ghelsbaID.mob.CYRANUCE_M_CUTAULEON,
        },

        allDeath = function(battlefield, mob)
            battlefield:setStatus(xi.battlefield.status.WON)
        end,
    },
}

return content:register()
