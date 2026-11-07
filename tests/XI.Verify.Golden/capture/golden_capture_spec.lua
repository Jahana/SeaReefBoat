-----------------------------------
-- Golden-value capture spec for xi_test.exe (copied to scripts/tests/golden_capture/ by capture.sh, removed after).
-- Runs every case of lua/golden_probe.lua against the real C++ server and writes case<TAB>key<TAB>value lines.
-- XI_GOLDEN_DIR: tests/XI.Verify.Golden; XI_GOLDEN_OUT: the output file; XI_GOLDEN_ONLY (optional): a Lua pattern,
-- only the cases whose name matches it run.
-----------------------------------
local ffi = require('ffi')

describe('Golden capture', function()
    it('combat formulas', function()
        local dir    = assert(os.getenv('XI_GOLDEN_DIR'), 'XI_GOLDEN_DIR not set')
        local out    = assert(os.getenv('XI_GOLDEN_OUT'), 'XI_GOLDEN_OUT not set')
        local only   = os.getenv('XI_GOLDEN_ONLY')
        local golden = dofile(dir .. '/lua/golden_probe.lua')
        local f      = assert(io.open(out, 'wb'))
        local chars  = {}
        local errors = 0

        -- Firesday 12:00, as the C# check's clock (VanaTicksNow = 8000 days + 12 hours)
        xi.test.world:setVanaDay(xi.day.FIRESDAY)
        xi.test.world:setVanaTime(12, 0)

        -- every char up front: spawnPlayer ticks the world for a few seconds, and a mob standing next to an earlier
        -- case's char would fight it meanwhile
        for i, spec in ipairs(golden.chars) do
            chars[i] = xi.test.world:spawnPlayer({ zone = golden.zone, race = spec.race })
        end

        -- the merit / job point chars raise theirs in their Mog House (the edit packets need one), then come back
        for i, spec in ipairs(golden.chars) do
            if spec.merits or spec.jobPoints then
                local pair = chars[i]
                pair:gotoMogHouse(xi.zone.SOUTHERN_SAN_DORIA)
                golden.setupChar(GetPlayerByID(pair:getID()), spec, function(opcode, size, bytes)
                    local packet = ffi.new('uint8_t[?]', size)
                    for offset, byte in pairs(bytes) do
                        packet[offset] = byte
                    end

                    pair.packets:send(opcode, packet, size)
                end)
                pair:gotoZone(golden.zone)
            end
        end

        local function charOf(i)
            return GetPlayerByID(chars[i]:getID())
        end

        for _, c in ipairs(golden.cases()) do
            if not only or string.find(c.name, only) then
                local p = charOf(c.char or c.race)
                local ok, err = pcall(golden.run, p, c, function(k, v)
                    f:write(c.name, '\t', k, '\t', v, '\n')
                end, charOf)

                if not ok then
                    errors = errors + 1
                    f:write(c.name, '\t!error\t', (tostring(err):gsub('[\t\r\n]', ' ')), '\n')
                end
            end
        end

        f:close()
        assert(errors == 0, string.format('%d golden cases failed', errors))
    end)
end)
