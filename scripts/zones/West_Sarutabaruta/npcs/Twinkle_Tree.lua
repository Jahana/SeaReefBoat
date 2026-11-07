-----------------------------------
-- Area: West Sarutabaruta
--  NPC: Twinkle Tree
-- Involved in Quest: To Catch a Falling Star
--  Note: EventID for Twinkle Tree is unknown. Quest funtions but the full event is not played.
-- !pos 156.003 -40.753 333.742 115
-----------------------------------
local ID = zones[xi.zone.WEST_SARUTABARUTA]
-----------------------------------
---@type TNpcEntity
local entity = {}

entity.onTrade = function(player, npc, trade)
    xi.cs.quest(player, npc, 'windurst.to_catch_a_falling_star', 'onTrade', trade)
end

entity.onTrigger = function(player, npc)
    if
        VanadielHour() <= 3 and
        player:getCharVar('QuestCatchAFallingStar_prog') == 0
    then
        player:messageSpecial(ID.text.FROST_DEPOSIT_TWINKLES)
        player:messageSpecial(ID.text.MELT_BARE_HANDS)
    else
        player:messageSpecial(ID.text.NOTHING_OUT_OF_ORDINARY)
    end
end

return entity
