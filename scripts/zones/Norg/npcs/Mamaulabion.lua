-----------------------------------
-- Area: Norg
--  NPC: Mamaulabion
-- Starts and finishes Quest: Mama Mia
-- !pos -57 -9 68 252

--CSIDs for Mamaulabion
--93 / 93 = Standard
--191 / 191 = start quest
--192 / 192 = quest accepted
--193 / 193 = given an item
--194 / 194 = given an item you already gave
--195 / 195 = all 7 items given
--196 / 196 = after 7 items, but need more time until reward is given
--197 / 197 = reward
--198 / 198 = after quest is complete
--243 / 243 = get new ring if you dropped yours

--I did alot of copy/pasting, so you may notice a reduncency on comments XD
--But it can make it easier to follow aswell.

--"Upon completion of this quest, the above items no longer appear in the rewards list for defeating the Prime Avatars."
--will require changing other avatar quests and making a variable for it all. (if this gets scripted, please remove this comment)

-----------------------------------
---@type TNpcEntity
local entity = {}

entity.onTrade = function(player, npc, trade)
    xi.cs.quest(player, npc, 'outlands.mama_mia', 'onTrade', trade)
end

entity.onTrigger = function(player, npc)
    if xi.cs.quest(player, npc, 'outlands.mama_mia', 'onTrigger') then
        return
    end

    player:startEvent(93) -- Standard dialog
end

entity.onEventFinish = function(player, csid, option, npc)
    xi.cs.questEvent(player, npc, 'outlands.mama_mia', csid, option)
end

return entity
