-----------------------------------
-- func: reloadquest
-- desc: Attempt to reload specified quest lua without a restart.
-----------------------------------
---@type TCommand
local commandObj = {}

commandObj.cmdprops =
{
    permission = 5,
    parameters = 's'
}

local function error(player, msg)
    player:printToPlayer(msg)
    player:printToPlayer('!reloadquest <lua-file-name>')
end

local function fileExists(path)
    local f = io.open(path, 'r')
    return f ~= nil and io.close(f)
end

local folders =
{
    'sandoria',
    'bastok',
    'windurst',
    'jeuno',
    'otherAreas',
    'outlands',
    'ahtUrhgan',
    'hiddenQuests',
    'abyssea',
    'adoulin',
}

commandObj.onTrigger = function(player, questName)
    if questName == nil then
        error(player, 'Unable to reload quest.')
    end

    for _, area in ipairs(folders) do
        local filename = table.concat({ 'scripts/quests/', area, '/', questName })
        if fileExists(filename .. '.lua') then
            -- a quest that runs in C# (quest rework B2) is rebuilt by the C# side, not from its Lua file
            local loaded = package.loaded[filename]
            local csharpKey = CSharpQuests and CSharpQuests.live and type(loaded) == 'table' and CSharpQuests.live[loaded.id]
            if csharpKey then
                player:printToPlayer(string.format('Quest \'%s\' runs in C# (%s); !reloadinteraction 1 (InteractionGlobal.reload(true)) reloads it.', questName, csharpKey))
                return
            end

            if package.loaded[filename] then
                local old = package.loaded[filename]
                package.loaded[filename] = nil
                if InteractionGlobal and old then
                    InteractionGlobal.lookup:removeContainer(old)
                end
            end

            local res = utils.prequire(filename)
            if InteractionGlobal and res then
                InteractionGlobal.lookup:addContainer(res)
                player:printToPlayer(string.format('Quest \'%s\' at \'%s\' has been reloaded.', questName, filename))
            end

            return
        end
    end

    -- a C# quest whose Lua file is gone (quest rework B3a): found by its LegacyPath
    for _, area in ipairs(folders) do
        local csharpKey = CSharpQuests and CSharpQuests.legacy and CSharpQuests.legacy[table.concat({ 'scripts/quests/', area, '/', questName })]
        if csharpKey then
            player:printToPlayer(string.format('Quest \'%s\' runs in C# (%s); !reloadinteraction 1 (InteractionGlobal.reload(true)) reloads it.', questName, csharpKey))
            return
        end
    end

    error(player, string.format('Unable to find quest \'%s\' to reload.', questName))
end

return commandObj
