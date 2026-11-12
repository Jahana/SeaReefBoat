// healCharacter(player) of Storms of Fate (scripts/battlefields/Riverne_Site_B01/storms_of_fate.lua): a living char
// gets its HP and MP filled and its TP emptied, and so does its pet if it has one. Quest rework C3, Q3b.
using XI.Quests.Authoring;

namespace XI.QuestContent.Battlefields.Shared
{
    public static class HealCharacter
    {
        public static void Heal(IBattlefieldPlayer player)
        {
            if (!player.IsAlive)
            {
                return;
            }

            player.SetHP(player.MaxHP);
            player.SetMP(player.MaxMP);
            player.SetTP(0);

            var pet = player.Pet();
            if (pet != null)
            {
                pet.SetHP(pet.MaxHP);
                pet.SetMP(pet.MaxMP);
                pet.SetTP(0);
            }
        }
    }
}
