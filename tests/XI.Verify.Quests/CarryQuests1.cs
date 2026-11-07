// XI.Verify.Quests -- the carry-quests-1 follow-ups that only the fake can show.
// - The fake's player:setPos(x, y, z) moves Position and keeps the facing, as CLuaBaseEntity::setPos does (its rotation
//   is the entity's own when no fourth number is given): the Windurst branch 1 review's follow-up.
using System;
using XI.Quests;
using XI.Quests.Authoring;

internal static partial class Program
{
    static void RunCarryQuests1()
    {
        Console.WriteLine("\n== carry-quests-1: the fake's follow-ups ==");
        var f = new Fake { Position = (1, 2, 3, 77) };
        f.SetPos(4.5, -6, 7.25);
        Check("carry1:fake SetPos(x, y, z) moves Position and keeps the facing",
              "4.5/-6/7.25/77|Player.SetPos(4.5,-6,7.25)",
              FormattableString.Invariant($"{f.Pos.X}/{f.Pos.Y}/{f.Pos.Z}/{f.Pos.Rot}|{string.Join(";", f.Calls)}"));
        f.SetPos(1, 1, 1, 9);
        Check("carry1:  the four-argument SetPos still sets the facing", "1/1/1/9",
              FormattableString.Invariant($"{f.Pos.X}/{f.Pos.Y}/{f.Pos.Z}/{f.Pos.Rot}"));
    }
}
