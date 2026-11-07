-----------------------------------
-- Area: Kazham
--  NPC: Gatih Mijurabi
-- !pos 58.249 -13.086 -49.084 250
-----------------------------------
---@type TNpcEntity
local entity = {}

entity.onTrigger = function(player, npc)
    if xi.cs.quest(player, npc, 'outlands.personal_hygiene', 'onTrigger') then
        return
    end

    player:startEvent(196)
end

entity.onEventFinish = function(player, csid, option, npc)
    xi.cs.questEvent(player, npc, 'outlands.personal_hygiene', csid, option)
end

return entity
