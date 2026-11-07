-----------------------------------
-- QuestScripts.Live's Lua fixture quest (test only, never shipped)
-----------------------------------
-- Loaded into the QuestScripts.Live worlds' containers as the Lua quest file scripts/quests/jeuno/QSL_Fixture
-- (ScriptEngine.ExtraContainerFiles; Harness.cs NewWorld), so its container, its slot in the list and the lookup
-- order are a Lua quest's. Its key, jeuno MYSTERY_OF_FIRE (Quest[3][33]), has no Lua file, no Lua draft, no C# quest
-- and no quest-drafts entry (Program.cs TestKeysFree fails loudly if a Lua file, draft or C# quest appears). Its
-- shape is what the switch, reload and refusal checks read: Brutus's onTrigger in each of the three sections
-- (FixtureTriggers), Tebhi's onTrade in the accepted one, and Upper Jeuno's onEventFinish 141 / 143 (available) and
-- 135 / 144 (accepted). The handlers answer nothing a test reads; the
-- available section is off unless the char has the QSL_FixtureOn char var, so no other check ever meets it.
-----------------------------------
local quest = Quest:new(xi.questLog.JEUNO, xi.quest.id.jeuno.MYSTERY_OF_FIRE)

quest.sections =
{
    {
        check = function(player, status, vars)
            return status == xi.questStatus.QUEST_AVAILABLE and player:getCharVar('QSL_FixtureOn') == 1
        end,

        [xi.zone.UPPER_JEUNO] =
        {
            ['Brutus'] =
            {
                onTrigger = function(player, npc)
                    return quest:progressEvent(141)
                end,
            },

            onEventFinish =
            {
                [141] = function(player, csid, option, npc)
                    quest:begin(player)
                end,

                [143] = function(player, csid, option, npc)
                    quest:begin(player)
                end,
            },
        },
    },

    {
        check = function(player, status, vars)
            return status == xi.questStatus.QUEST_ACCEPTED
        end,

        [xi.zone.UPPER_JEUNO] =
        {
            ['Brutus'] =
            {
                onTrigger = function(player, npc)
                    return quest:event(142)
                end,
            },

            onEventFinish =
            {
                [135] = function(player, csid, option, npc)
                    quest:complete(player)
                end,

                [144] = function(player, csid, option, npc)
                    quest:setVar(player, 'Prog', 4)
                end,
            },
        },

        [xi.zone.CASTLE_OZTROJA] =
        {
            ['Tebhi'] =
            {
                onTrade = function(player, npc, trade)
                    if npcUtil.tradeHasExactly(trade, xi.item.BEAST_COLLAR) then
                        return quest:messageSpecial(7000)
                    end
                end,
            },
        },
    },

    {
        check = function(player, status, vars)
            return status == xi.questStatus.QUEST_COMPLETED
        end,

        [xi.zone.UPPER_JEUNO] =
        {
            ['Brutus'] =
            {
                onTrigger = function(player, npc)
                    return quest:event(151):importantOnce()
                end,
            },
        },
    },
}

return quest
