-----------------------------------
-- Area: Windurst Woods
--  NPC: Ponono
-- Type: Clothcraft Guild Master
-- !pos -38.243 -2.25 -120.954 241
-- TODO Allow players to purchase additional Cuttings
-- TODO Allow players to cancel quest
-- TODO Requre check for other 3 quests An Understanding General, A Generous General,
-- TODO: Convert this quest to interaction.
-----------------------------------
---@type TNpcEntity
local entity = {}

entity.onTrade = function(player, npc, trade)
    if xi.cs.quest(player, npc, 'otherAreas.a_moral_manifest', 'onTrade', trade) then
        return
    end

    xi.crafting.guildMasterOnTrade(player, npc, trade)
end

entity.onTrigger = function(player, npc)
    -- TODO: Convert this to interaction. The way this is coded, it WILL block you out from a guildmaster.
    if xi.cs.quest(player, npc, 'otherAreas.a_moral_manifest', 'onTrigger') then
        return
    end

    -- Regular guildmaster logic.
    xi.crafting.guildMasterOnTrigger(player, npc)
end

entity.onEventFinish = function(player, csid, option, npc)
    if xi.cs.questEvent(player, npc, 'otherAreas.a_moral_manifest', csid, option) then
        return
    end

    xi.crafting.guildMasterOnEventFinish(player, csid, option, npc)
end

return entity
