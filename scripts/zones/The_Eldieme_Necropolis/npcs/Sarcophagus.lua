-----------------------------------
-- Area: The Eldieme Necropolis
--  NPC: Sarcophagus
-- Involved in Quests: A New Dawn (BST AF3)
-- !pos -420 8 500 195
-----------------------------------
local ID = zones[xi.zone.THE_ELDIEME_NECROPOLIS]
-----------------------------------
---@type TNpcEntity
local entity = {}

entity.onTrigger = function(player, npc)
    -- A NEW DAWN (Beastmaster AF3)
    if xi.cs.quest(player, npc, 'jeuno.a_new_dawn', 'onTrigger') then
        return

    -- DEFAULT DIALOG
    else
        player:messageSpecial(ID.text.SARCOPHAGUS_CANNOT_BE_OPENED)
    end
end

entity.onEventFinish = function(player, csid, option, npc)
    -- A NEW DAWN
    if csid == 45 then
        xi.cs.questEvent(player, npc, 'jeuno.a_new_dawn', csid, option)
    end
end

return entity
