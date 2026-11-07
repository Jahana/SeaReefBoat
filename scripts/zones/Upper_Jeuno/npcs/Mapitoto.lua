-----------------------------------
-- Area: Upper Jeuno
--  NPC: Mapitoto
-- Type: Full Speed Ahead Mount NPC
-- !pos -54.310 8.200 85.940 244
-----------------------------------
local ID = zones[xi.zone.UPPER_JEUNO]
-----------------------------------
---@type TNpcEntity
local entity = {}

entity.onTrade = function(player, npc, trade)
    if
        player:hasKeyItem(xi.keyItem.TRAINERS_WHISTLE) and
        trade:getSlotCount() == 1 and
        -- The Fenrir (10057) and Omega (10067) items and mounts have their own questlines, so they aren't valid trades here
        not (npcUtil.tradeHasExactly(trade, xi.item.MOUNT_FENRIR) or npcUtil.tradeHasExactly(trade, xi.item.MOUNT_OMEGA))
    then
        local item  = trade:getItemId(0)
        local mount = item - xi.item.MOUNT_TIGER
        if item == xi.item.CHOCOBO_WHISTLE then
            -- TODO: The reply to a whistle with no chocobo registered.
            if not player:getFieldChocobo() then
                return
            end

            player:startEvent(10227, xi.item.CHOCOBO_WHISTLE, xi.keyItem.TRAINERS_WHISTLE, 0, xi.keyItem.CHOCOBO_COMPANION)
            player:setLocalVar('FullSpeedAheadReward', xi.keyItem.CHOCOBO_COMPANION)

            -- The key item comes with the event; its message comes at the end.
            player:addKeyItem(xi.keyItem.CHOCOBO_COMPANION)
        elseif item == xi.item.RED_RAPTOR_NOTEBOOK then -- Key Items, Items, and Mount IDs don't line up for 4 mounts starting with Red Raptor
            player:setLocalVar('FullSpeedAheadReward', xi.keyItem.TIGER_COMPANION + mount + 3)
            player:startEvent(10227, item, xi.keyItem.TRAINERS_WHISTLE, xi.mount.TIGER + mount + 2)
        elseif
            item >= xi.item.GOLDEN_BOMB_NOTEBOOK and
            item <= xi.item.WIVRE_NOTEBOOK
        then
            -- These are all offset by one due to Red Raptor
            player:setLocalVar('FullSpeedAheadReward', xi.keyItem.TIGER_COMPANION + mount - 1)
            player:startEvent(10227, item, xi.keyItem.TRAINERS_WHISTLE, xi.mount.TIGER + mount - 2)
        elseif mount >= xi.mount.CHOCOBO and mount <= xi.mount.MOUNT_MAX then
            player:setLocalVar('FullSpeedAheadReward', xi.keyItem.TIGER_COMPANION + mount)
            player:startEvent(10227, item, xi.keyItem.TRAINERS_WHISTLE, xi.mount.TIGER + mount - 1)
        end
    end
end

entity.onTrigger = function(player, npc)
    xi.cs.quest(player, npc, 'jeuno.full_speed_ahead', 'onTrigger')
end

entity.onEventFinish = function(player, csid, option, npc)
    if csid == 10223 or csid == 10224 or csid == 10225 then
        xi.cs.questEvent(player, npc, 'jeuno.full_speed_ahead', csid, option)
    elseif csid == 10227 then
        local rewardKI = player:getLocalVar('FullSpeedAheadReward')
        player:setLocalVar('FullSpeedAheadReward', 0)
        if rewardKI == xi.keyItem.CHOCOBO_COMPANION then
            -- Nothing is confirmed, so this hands the whistle back.
            player:confirmTrade()
            player:messageSpecial(ID.text.KEYITEM_OBTAINED, rewardKI)
        elseif
            rewardKI > 0 and
            player:tradeComplete()
        then
            npcUtil.giveKeyItem(player, rewardKI)
        end
    end
end

return entity
