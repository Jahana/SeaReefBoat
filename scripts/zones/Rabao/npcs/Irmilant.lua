-----------------------------------
-- Area: Rabao
--  NPC: Irmilant
-- Starts and Ends Quests: The Immortal Lu Shang and Indomitable Spirit
-- !pos 3.78 9.54 56.21 247
-----------------------------------
---@type TNpcEntity
local entity = {}

entity.onTrade = function(player, npc, trade)
    if xi.cs.quest(player, npc, 'outlands.the_immortal_lu_shang', 'onTrade', trade) then
        return
    end

    xi.cs.quest(player, npc, 'outlands.indomitable_spirit', 'onTrade', trade)
end

entity.onTrigger = function(player, npc)
    if xi.cs.quest(player, npc, 'outlands.the_immortal_lu_shang', 'onTrigger') then
        return
    end

    xi.cs.quest(player, npc, 'outlands.indomitable_spirit', 'onTrigger')
end

entity.onEventFinish = function(player, csid, option, npc)
    xi.cs.questEvent(player, npc, 'outlands.the_immortal_lu_shang', csid, option)
    xi.cs.questEvent(player, npc, 'outlands.indomitable_spirit', csid, option)
end

return entity
