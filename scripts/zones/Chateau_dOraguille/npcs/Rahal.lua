-----------------------------------
-- Area: Chateau d'Oraguille
--  NPC: Rahal
-- Involved in Quests: The Holy Crest, Lure of the Wildcat (San d'Oria)
-- !pos -28 0.1 -6 233
-----------------------------------
---@type TNpcEntity
local entity = {}

entity.onTrigger = function(player, npc)
    local crestProgress = player:getCharVar('TheHolyCrest_Event')
    local hasDragonCurseRemedy = player:hasKeyItem(xi.keyItem.DRAGON_CURSE_REMEDY)

    -- Need to speak with Rahal to get Dragon Curse Remedy
    if crestProgress == 5 and not hasDragonCurseRemedy then
        player:startEvent(60) -- Gives key item
    elseif crestProgress == 5 and hasDragonCurseRemedy then
        player:startEvent(122) -- Reminder to go to Gelsba

    -- Knight Stalker (DRG AF3)
    elseif xi.cs.quest(player, npc, 'sandoria.knight_stalker', 'onTrigger') then
        return

    elseif player:getCharVar('KnightStalker_Option2') == 1 then
        player:startEvent(118) -- Optional CS after Knight Stalker

    else
        player:startEvent(529)
    end
end

entity.onEventFinish = function(player, csid, option, npc)
    if csid == 60 then
        npcUtil.giveKeyItem(player, xi.keyItem.DRAGON_CURSE_REMEDY)
    elseif csid == 121 or csid == 120 or csid == 78 or csid == 110 then
        xi.cs.questEvent(player, npc, 'sandoria.knight_stalker', csid, option)
    elseif csid == 118 then
        player:setCharVar('KnightStalker_Option2', 0)
    end
end

return entity
