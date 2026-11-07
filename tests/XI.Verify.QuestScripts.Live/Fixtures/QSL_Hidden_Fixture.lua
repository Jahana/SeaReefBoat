-----------------------------------
-- QuestScripts.Live's Lua fixture hidden quest (test only, never shipped)
-----------------------------------
-- Loaded into the QuestScripts.Live worlds' containers as the Lua quest file
-- scripts/quests/hiddenQuests/QSL_Hidden_Fixture (ScriptEngine.ExtraContainerFiles; Harness.cs NewWorld), so P9's test
-- hidden quest (PlatformP9.cs P9HiddenLive) takes a Lua hidden quest's container slot, HQuest[qslHiddenFixture], as a
-- port takes its file's. No hidden quest is named qslHiddenFixture (Program.cs TestKeysFree). Its one section is off:
-- a Bastok Markets zone-out entry that never runs.
-----------------------------------
local quest = HiddenQuest:new('qslHiddenFixture')

quest.sections =
{
    {
        check = function(player, vars)
            return false
        end,

        [xi.zone.BASTOK_MARKETS] =
        {
            onZoneOut = function(player)
            end,
        },
    },
}

return quest
