// The Diremite pit logic of The Shrouded Maw's two Diabolos fights, shared by Darkness Named
// (scripts/battlefields/The_Shrouded_Maw/darkness_named.lua) and Waking Dreams (waking_dreams.lua), whose copies of it
// differ only in the mob id key (DIABOLOS and DIABOLOS_WD). Quest rework C3.
// CloseTiles closes the doors of the area's eight tiles (setupBattlefield). Tick, called from onBattlefieldTick after
// the base tick, walks the six Diremites of the area (offsets 1-6 from the area's Diabolos): one dead for 10 seconds
// since the deathTime its zone script set (Diremite.lua for Darkness Named, Diremite_Dominator.lua for Waking Dreams)
// respawns, and one that is alive and engaged marks the players outside the arena box as in the pit, resets its enmity
// for one that came back and disengages once every player is inside. Kept quirk: a tick with no players disengages the
// Diremite, since all players are then vacuously inside.
using XI.Quests.Authoring;

namespace XI.QuestContent.Battlefields.Shared
{
    public static class DiremitePits
    {
        // xi.animation.CLOSE_DOOR
        private const int CloseDoor = 9;

        // Arena boundaries: Defined like regular rectangular regions
        // [area - 1] = { x(min), x(max), y(min), y(max), z(min), z(max) }
        private static readonly double[][] ArenaBoxes =
        {
            new double[] { -250, -230, -46, -25, 275, 294 },
            new double[] { -10, 10, 0, 13, -6, 14 },
            new double[] { 270, 290, 35, 52, -286, -266 },
        };

        // setupBattlefield: the area's eight tiles, from the zone npc id DARKNESS_NAMED_TILE_OFFSET (both Lua files)
        public static void CloseTiles(IBattlefieldContext q)
        {
            int tileOffset = q.Engine.ZoneNpcId(Zone.TheShroudedMaw, "DARKNESS_NAMED_TILE_OFFSET").Value +
                (q.Battlefield.Area - 1) * 8;

            for (int tileId = tileOffset; tileId <= tileOffset + 7; tileId++)
            {
                q.Engine.NpcById(tileId).SetAnimation(CloseDoor);
            }
        }

        // onBattlefieldTick, after the base tick: diabolosKey is the zone mob id key of the first Diabolos
        public static void Tick(IBattlefieldContext q, string diabolosKey)
        {
            int areaOffset = (q.Battlefield.Area - 1) * 7;
            int baseId = q.Engine.ZoneMobId(Zone.TheShroudedMaw, diabolosKey).Value + areaOffset;

            // Check Diremites (offsets 1-6 from base)
            for (int i = 1; i <= 6; i++)
            {
                var diremite = q.Engine.MobById(baseId + i);
                if (diremite != null)
                {
                    HandleDiremiteRespawn(diremite, q);
                    HandleDiremiteEnmityReset(diremite, q);
                }
            }
        }

        private static bool IsPlayerInArenaBox(IBattlefieldPlayer player, double[] area)
        {
            double playerX = player.XPos;
            double playerY = player.YPos;
            double playerZ = player.ZPos;
            bool xCheck = playerX >= area[0] && playerX <= area[1];
            bool yCheck = playerY >= area[2] && playerY <= area[3];
            bool zCheck = playerZ >= area[4] && playerZ <= area[5];

            return xCheck && yCheck && zCheck;
        }

        private static void HandleDiremiteEnmityReset(IBattlefieldMob diremite, IBattlefieldContext q)
        {
            if (!diremite.IsAlive || !diremite.IsEngaged)
            {
                return;
            }

            // Did a player fall out of the arena?
            double[] box = ArenaBoxes[q.Battlefield.Area - 1];
            bool allPlayersInArena = true;

            foreach (var player in q.Battlefield.Players())
            {
                bool inArena = IsPlayerInArenaBox(player, box);
                bool wasInPit = player.LocalVar("inDiremitePit") == 1;

                if (!inArena)
                {
                    allPlayersInArena = false;
                    // Mark player as in pit
                    if (!wasInPit)
                    {
                        player.SetLocalVar("inDiremitePit", 1);
                    }
                }
                // Player is in arena - reset enmity if they just came from pit
                else if (wasInPit)
                {
                    diremite.ResetEnmity(player);
                    player.SetLocalVar("inDiremitePit", 0);
                }
            }

            // If all players are in the arena, disengage the Diremite completely
            // Diremites despawn if too far from spawn, controlled in the Diremite zone script
            if (allPlayersInArena)
            {
                diremite.Disengage();
            }
        }

        // Diremites respawn 10 seconds after death
        private static void HandleDiremiteRespawn(IBattlefieldMob diremite, IBattlefieldContext q)
        {
            if (!diremite.IsAlive)
            {
                long deathTime = diremite.LocalVar("deathTime");

                if (deathTime > 0 && (long)q.Engine.GetSystemTime() - deathTime >= 10)
                {
                    diremite.SetLocalVar("deathTime", 0);
                    q.Engine.SpawnMob(diremite.Id);
                }
            }
        }
    }
}
