-----------------------------------
-- Area: Southern San d'Oria
--  NPC: Cletae
-- Guild Merchant NPC: Leathercrafting Guild
-- !pos -189.142 -8.800 14.449 230
-----------------------------------
local ID = zones[xi.zone.SOUTHERN_SAN_DORIA]
-----------------------------------
---@type TNpcEntity
local entity = {}

entity.onTrade = function(player, npc, trade)
    -- Flyers_For_Regine needs to be reviewed.
    xi.cs.quest(player, npc, 'sandoria.flyers_for_regine', 'onTrade', trade)
end

entity.onTrigger = function(player, npc)
    local guildSkillId = xi.skill.LEATHERCRAFT
    xi.shop.generalGuild(player, xi.shop.generalGuildStock[guildSkillId], guildSkillId)
    player:showText(npc, ID.text.CLETAE_DIALOG)
end

return entity
