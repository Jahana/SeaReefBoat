using System;
using System.Collections.Generic;
using XI.Map;
using XI.Persistence;

namespace XI.Zone
{
    // c2s/0x096 COMBINE_ASK, synthutils.cpp and items/transactions/synth.cpp: the recipe is resolved, the crystal and
    // ingredients are claimed and greyed out, the crystal is used and the result rolled up front; CSynthState hands the
    // result out (or loses materials) 16 seconds later
    public sealed partial class ZoneHost
    {
        // xirand: GetRandomNumber(min, max) is a double in [min, max), GetRandomNumber(n) an int in [0, n)
        public Func<double> Random01 = Random.Shared.NextDouble;
        private double RandRange(double min, double max) => min + (max - min) * Random01();
        private int RandInt(int n) => (int)(Random01() * n);

        private const ushort MsgCannotUseInArea = 316, MsgWaitLonger = 94, MsgSkillGain = 38, MsgSkillLevelUp = 53, MsgSkillDrop = 310;
        private const ushort MangledMess = 29695;
        private const uint ItemFlagInscribable = ItemSignature.ItemFlagInscribable;
        private const byte LockNormal = 0x00, LockNoSelect = 0x0F;

        // SynthesisResult
        private const byte SynthSuccess = 0x00, SynthFailed = 0x01, SynthCancelBadRecipe = 0x03, SynthCancelSkillTooLow = 0x06,
            SynthCancelRareItem = 0x07, SynthSuccessDesynth = 0x0C, SynthInterruptedCritical = 0x0E;

        // SYNTHESIS_RESULT, and the RESULT_* the 0x030 effect carries
        private const byte SynthesisFail = 0, SynthesisSuccess = 1, SynthesisHq = 2;
        private const byte ResultSuccess = 0x00, ResultFail = 0x01, ResultHq = 0x02;

        private static readonly long[] ValidCrystals =
        {
            4096, 4097, 4098, 4099, 4100, 4101, 4102, 4103, 4111,
            4238, 4239, 4240, 4241, 4242, 4243, 4244, 4245,
            6506, 6507, 6508, 6509, 6510, 6511, 6512, 6513,
        };

        // crystalString and crystalProps: base, HQ and the third tier of each element share Fire 0 .. Dark 7, and the
        // effect the 0x030 plays for it
        private static int CrystalElement(ushort crystal)
        {
            foreach (int first in new[] { 4096, 4238, 6506 })
                if (crystal >= first && crystal < first + 8) return crystal - first;
            return -1;
        }

        private static readonly string[] ElementNames = { "Fire", "Ice", "Wind", "Earth", "Lightning", "Water", "Light", "Dark" };
        private static readonly short[] ElementEffects = { 0x12, 0x15, 0x11, 0x13, 0x14, 0x10, 0x16, 0x17 };

        private static string RecipeKey(ushort crystal, ushort[] ingredients)
        {
            int e = CrystalElement(crystal);
            return $"{(e < 0 ? "None" : ElementNames[e])}-{string.Join("-", ingredients)}";
        }

        // synth_recipes, one per ingredient key (a later row with the same key replaces an earlier one)
        private sealed class SynthRecipe
        {
            public uint Id;
            public byte Desynth;
            public int KeyItem;
            public readonly byte[] Skills = new byte[8]; // Wood .. Cook
            public readonly (ushort ItemId, byte Qty)[] Results = new (ushort, byte)[5];
            public string ContentTag;
        }

        private Dictionary<string, SynthRecipe> synthRecipes_;

        private static readonly string[] SkillColumns = { "Wood", "Smith", "Gold", "Cloth", "Leather", "Bone", "Alchemy", "Cook" };

        // synthutils::LoadSynthRecipes ("Loading synth recipes"): at the map's init (LoadRecipes), else on the first
        // synth
        private Dictionary<string, SynthRecipe> SynthRecipes()
        {
            if (synthRecipes_ != null) return synthRecipes_;
            log_("Loading synth recipes");
            synthRecipes_ = new Dictionary<string, SynthRecipe>();
            var r = Exec("SELECT ID, Desynth, KeyItem, Wood, Smith, Gold, Cloth, Leather, Bone, Alchemy, Cook, Crystal, HQCrystal, " +
                         "Ingredient1, Ingredient2, Ingredient3, Ingredient4, Ingredient5, Ingredient6, Ingredient7, Ingredient8, " +
                         "Result, ResultHQ1, ResultHQ2, ResultHQ3, ResultQty, ResultHQ1Qty, ResultHQ2Qty, ResultHQ3Qty, ResultName, content_tag " +
                         "FROM synth_recipes");
            if (r == null)
            {
                log_("Failed to load synth recipes");
                return synthRecipes_;
            }
            while (r.Next())
            {
                var recipe = new SynthRecipe
                {
                    Id = r.Get<uint>("ID"),
                    Desynth = r.Get<byte>("Desynth"),
                    KeyItem = r.Get<int>("KeyItem"),
                    ContentTag = r.IsNull("content_tag") ? "" : r.Get<string>("content_tag"),
                };
                for (int i = 0; i < 8; i++) recipe.Skills[i] = r.Get<byte>(SkillColumns[i]);
                recipe.Results[1] = (r.Get<ushort>("Result"), r.Get<byte>("ResultQty"));
                for (int i = 1; i <= 3; i++) recipe.Results[i + 1] = (r.Get<ushort>($"ResultHQ{i}"), r.Get<byte>($"ResultHQ{i}Qty"));
                var ingredients = new ushort[8];
                for (int i = 0; i < 8; i++) ingredients[i] = r.Get<ushort>($"Ingredient{i + 1}");
                synthRecipes_[RecipeKey(r.Get<ushort>("Crystal"), ingredients)] = recipe;
            }
            return synthRecipes_;
        }

        // SynthOffer: an ingredient whose slot stayed 0xFF was not usable and counts as no ingredient
        private sealed class SynthOffer
        {
            public ushort Crystal;
            public byte CrystalSlot;
            public readonly ushort[] Ids = new ushort[8];
            public readonly byte[] Slots = { 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF };
        }

        // CCraftState and the SynthTransaction holding the claimed stacks: slot 0 is the crystal, 1..8 the ingredients
        internal sealed class SynthSession
        {
            public uint RecipeId;
            public bool Desynth;
            public ushort Crystal;
            public byte Element;
            public readonly (ushort ItemId, byte Qty)[] Results = new (ushort, byte)[5];
            public readonly byte[] SkillRequired = new byte[8];
            public readonly ushort[] Ingredients = new ushort[8];
            public readonly bool[] Broken = new bool[8];
            public byte FailingSkill, Result;

            internal ItemTransaction Tx;
            public readonly Item[] Claimed = new Item[9];
            public readonly bool[] Saved = new bool[9];
            public (ushort ItemId, byte Qty)? PendingResult;
            public bool Delivering;
            public long FinishAtMs;

            // heldSlots: the inventory shows one less free slot until the result is delivered
            public int HeldSlots(Container loc) => loc == Container.Inventory && !Delivering ? 1 : 0;
        }

        private PacketValidator ValidateCombineAsk(CharData c, BasicPacket p) => Pv(c)
            .BlockedBy(BlockedState.InEvent | BlockedState.AbnormalStatus | BlockedState.Crafting | BlockedState.PreventAction | BlockedState.Monstrosity)
            .OneOf("Crystal", U16(p, 6), ValidCrystals)
            .Range("Items", p.Buffer[9], 1, 8);

        // GP_CLI_COMMAND_COMBINE_ASK: Crystal@6, CrystalIdx@8, Items@9, ItemNo[8]@10, TableNo[8]@26
        private void OnCombineAsk(MapSession s, CharData c, BasicPacket p)
        {
            if (InPrison(c))
            {
                Push(s, SelfPackets.BattleMessage(c, 0, 0, MsgCannotUseInArea));
                return;
            }
            // the full synth duration is waited out whatever the client's animation speed
            if (c.LastSynthTimeMs + 15000 > NowMs())
            {
                Push(s, SelfPackets.BattleMessage(c, 0, 0, MsgWaitLonger));
                return;
            }
            // the universal container is busy (an open delivery box)
            if (!c.UContainer.IsContainerEmpty)
            {
                Push(s, SelfPackets.Message(MsgStdCannotBeProcessed));
                c.UContainer.Clean();
                return;
            }

            ushort crystal = U16(p, 6);
            byte crystalSlot = p.Buffer[8];
            var crystalItem = StorageItem(c, Container.Inventory, crystalSlot);
            if (crystalItem == null || crystalItem.ItemId != crystal || crystalItem.Quantity == 0)
            {
                Push(s, SelfPackets.BattleMessage(c, 0, 0, MsgCannotUseInArea));
                return;
            }
            if (IsBusy(c, crystalItem))
            {
                log_($"GP_CLI_COMMAND_COMBINE_ASK: {c.Name} trying to use unavailable crystal");
                Push(s, SelfPackets.BattleMessage(c, 0, 0, MsgCannotUseInArea));
                return;
            }

            var offer = new SynthOffer { Crystal = crystal, CrystalSlot = crystalSlot };
            var slotQty = new int[256];
            int items = p.Buffer[9];
            for (int i = 0; i < items; i++)
            {
                ushort id = U16(p, 10 + 2 * i);
                byte slot = p.Buffer[26 + i];
                slotQty[slot]++;
                var item = StorageItem(c, Container.Inventory, slot);
                if (item == null || item.ItemId != id) continue;
                if (IsBusy(c, item) || slotQty[slot] > item.Quantity)
                {
                    log_($"GP_CLI_COMMAND_COMBINE_ASK: {c.Name} trying to use unavailable ingredient");
                    continue;
                }
                offer.Ids[i] = id;
                offer.Slots[i] = slot;
            }
            StartSynth(s, c, offer);
        }

        private const ushort MordionGaolZone = 131;

        // resolveRecipe: the craft state for the offer, or null once the client has been told why not
        private SynthSession ResolveRecipe(MapSession s, CharData c, SynthOffer offer)
        {
            if (!SynthRecipes().TryGetValue(RecipeKey(offer.Crystal, offer.Ids), out var recipe) ||
                (recipe.ContentTag != "" && !contentEnabled_(recipe.ContentTag)))
            {
                Push(s, CombineAns(c, SynthCancelBadRecipe));
                return null;
            }
            if (((ItemBasic(recipe.Results[1].ItemId)?.Flags ?? 0) & ItemFlagRare) != 0 && HasItem(c, recipe.Results[1].ItemId))
            {
                Push(s, CombineAns(c, SynthCancelRareItem));
                return null;
            }
            if (recipe.KeyItem != 0 && !c.HasKeyItem(recipe.KeyItem))
            {
                Push(s, CombineAns(c, SynthCancelBadRecipe));
                return null;
            }

            int element = CrystalElement(offer.Crystal);
            var synth = new SynthSession
            {
                RecipeId = recipe.Id, Desynth = recipe.Desynth != 0, Crystal = offer.Crystal, Element = (byte)Math.Max(element, 0),
            };
            Array.Copy(recipe.Results, synth.Results, 5);
            Array.Copy(offer.Ids, synth.Ingredients, 8);
            for (int i = 0; i < 8; i++)
            {
                synth.SkillRequired[i] = recipe.Skills[i];
                if (c.RealSkills[Skill.Woodworking + i] < recipe.Skills[i] * 10 - 150)
                {
                    Push(s, CombineAns(c, SynthCancelSkillTooLow));
                    return null;
                }
            }
            return synth;
        }

        // SynthTransaction::start: the result needs a free slot, then the crystal and every ingredient stack are claimed
        // and greyed out (one ITEM_LIST per stack)
        private bool StartSynthTransaction(CharData c, SynthSession synth, SynthOffer offer)
        {
            var tx = new ItemTransaction(this, c);
            bool Abandon()
            {
                tx.Dispose();
                return false;
            }
            if (offer.CrystalSlot == 0xFF) return Abandon();
            if (FreeSlots(c, Container.Inventory) == 0)
            {
                log_($"SynthTransaction::start: {c.Name} has no free inventory slot");
                return Abandon();
            }
            synth.Claimed[0] = ClaimAndLock(tx, c, offer.CrystalSlot);
            if (synth.Claimed[0] == null)
            {
                log_($"SynthTransaction::start: {c.Name} could not claim crystal {offer.Crystal} at slot {offer.CrystalSlot}");
                return Abandon();
            }
            for (int i = 0; i < 8; i++)
            {
                byte slot = offer.Slots[i];
                if (slot == 0xFF) continue;
                if (slot == offer.CrystalSlot)
                {
                    log_($"SynthTransaction::start: {c.Name} ingredient entry {i} aliases crystal slot {slot}");
                    return Abandon();
                }
                if (StorageItem(c, Container.Inventory, slot) == null) continue;
                synth.Claimed[i + 1] = ClaimAndLock(tx, c, slot);
                if (synth.Claimed[i + 1] == null)
                {
                    log_($"SynthTransaction::start: {c.Name} could not claim ingredient {offer.Ids[i]} at slot {slot} (entry {i})");
                    return Abandon();
                }
            }
            synth.Tx = tx;
            return true;
        }

        private Item ClaimAndLock(ItemTransaction tx, CharData c, byte slot)
        {
            var item = StorageItem(c, Container.Inventory, slot);
            bool alreadyHeld = item != null && tx.Holds(item);
            var claimed = tx.ClaimSlot(Container.Inventory, slot);
            if (claimed != null && !alreadyHeld) PushTo(c, LoginPackets.ItemList(claimed, LockNoSelect));
            return claimed;
        }

        // synthutils::startSynth
        private void StartSynth(MapSession s, CharData c, SynthOffer offer)
        {
            var synth = ResolveRecipe(s, c, offer);
            if (synth == null) return;
            if (!StartSynthTransaction(c, synth, offer))
            {
                log_($"startSynth: failed to claim ingredients for {c.Name}");
                Push(s, CombineAns(c, SynthCancelBadRecipe));
                return;
            }
            c.LastSynthTimeMs = NowMs();
            c.Synth = synth;

            // consumeCrystal: one crystal goes now, and the rest of the stack goes back to the char
            var crystal = synth.Claimed[0];
            if (!synth.Tx.Take(Container.Inventory, crystal.Slot, 1))
                log_($"SynthTransaction: {c.Name} kept the crystal in slot {crystal.Slot}");
            else
                synth.Tx.Release(crystal);
            synth.Claimed[0] = null;

            byte result = HandleSynthResult(c, synth);

            int skillType = HighestSynthSkill(synth);
            c.Animation = Anim.Synth;
            c.UpdateMask |= CharStats.UpdateHp;
            Push(s, CharStatus(c));
            SendUpdateMask(s, c);
            // CSynthState: 16 seconds, less the craft's SYNTH_SPEED_* in milliseconds
            synth.FinishAtMs = NowMs() + 16000 - (skillType != 0 ? c.GetMod(ModAt(Mod.SYNTH_SPEED_WOODWORKING, skillType - Skill.Woodworking)) : 0);

            int element = CrystalElement(synth.Crystal);
            PushCharInRange(c, Effect(c, element < 0 ? (short)0 : ElementEffects[element], result), true);
        }

        // the craft the recipe "belongs" to: the highest skill it requires
        private static int HighestSynthSkill(SynthSession synth)
        {
            int skillType = 0, highest = 0;
            for (int i = 0; i < 8; i++)
            {
                if (synth.SkillRequired[i] > highest)
                {
                    skillType = Skill.Woodworking + i;
                    highest = synth.SkillRequired[i];
                }
            }
            return skillType;
        }

        // getSynthDifficulty: the recipe level less the char's whole skill levels and the craft's skill mod
        private static int SynthDifficulty(CharData c, SynthSession synth, int i) =>
            synth.SkillRequired[i] - (byte)(c.RealSkills[Skill.Woodworking + i] / 10) - c.GetMod(ModAt(Mod.WOOD, i));

        // the craft-ordered mods (Woodworking .. Cooking, or Fire .. Dark) are consecutive
        private static Mod ModAt(Mod first, int i) => (Mod)((int)first + i);

        // canSynthesizeHQ: a craft ring blocks its craft's HQs
        private static bool CanSynthesizeHq(CharData c, int i) => c.GetMod(ModAt(Mod.SYNTH_ANTI_HQ_WOODWORKING, i)) == 0;

        private StatSettings Settings => loader_.Stats?.Settings ?? defaultSettings_;
        private static readonly StatSettings defaultSettings_ = new StatSettings();

        // calculateSynthResult: a break check per craft involved, then the HQ roll and its upgrades
        private byte CalculateSynthResult(CharData c, SynthSession synth)
        {
            int finalHqTier = 4;
            bool canHq = true;
            for (int i = 0; i < 8; i++)
            {
                if (synth.SkillRequired[i] == 0) continue;
                int difficulty = SynthDifficulty(c, synth, i);
                double successRate = 95.0;
                int tier = 0;
                if (difficulty >= 4)
                {
                    successRate = 80.0 - 10.0 * (difficulty - 3);
                    canHq = false;
                }
                else if (difficulty >= 1)
                {
                    successRate = 95.0 - 5.0 * difficulty;
                    canHq = false;
                }
                else if (difficulty >= -10) tier = 1;
                else if (difficulty >= -30) tier = 2;
                else if (difficulty >= -50) tier = 3;
                else tier = 4;
                if (tier < finalHqTier) finalHqTier = tier;

                successRate += c.GetMod(Mod.SYNTH_SUCCESS_RATE);
                if (!CanSynthesizeHq(c, i))
                {
                    canHq = false;
                    successRate += 1.0;
                }
                successRate = Math.Min(successRate, 99.0);

                // the break check is per craft involved
                if (RandRange(0.0, 100.0) > successRate)
                {
                    synth.FailingSkill = (byte)(Skill.Woodworking + i);
                    return SynthesisFail;
                }
            }
            if (!canHq) return SynthesisSuccess;

            double chanceHq = finalHqTier switch { 4 => 50.0, 3 => 25.0, 2 => 6.25, 1 => 1.5625, _ => 0.0 };
            chanceHq = (chanceHq + 100.0 * c.GetMod(Mod.SYNTH_HQ_RATE) / 512.0) * Settings.CraftHqChanceMultiplier;
            chanceHq = Math.Min(chanceHq, 80.0);
            if (RandRange(0.0, 100.0) > chanceHq) return SynthesisSuccess;
            if (finalHqTier <= 1) return SynthesisHq;

            int allowedUpgrades = finalHqTier == 2 ? 1 : 2, upgrade = 0;
            for (int tries = 0; tries < allowedUpgrades; tries++)
            {
                if (RandRange(0.0, 100.0) <= 25.0) upgrade++;
                else break;
            }
            return (byte)(SynthesisHq + upgrade);
        }

        // calculateDesynthResult: lower success rates, and a flat HQ chance split 30/20/10 between the tiers
        private byte CalculateDesynthResult(CharData c, SynthSession synth)
        {
            bool canHq = true;
            for (int i = 0; i < 8; i++)
            {
                if (synth.SkillRequired[i] == 0) continue;
                int difficulty = SynthDifficulty(c, synth, i);
                double successRate;
                if (difficulty >= 8) successRate = 10.0 - 10.0 * (difficulty - 7) / 3.0;
                else if (difficulty >= 1) successRate = 40.0 - 5.0 * (difficulty - 1);
                else successRate = 40.0;
                successRate += c.GetMod(Mod.SYNTH_SUCCESS_RATE_DESYNTHESIS);
                if (!CanSynthesizeHq(c, i))
                {
                    successRate += 1.0;
                    canHq = false;
                }
                if (RandRange(0.0, 100.0) > successRate)
                {
                    synth.FailingSkill = (byte)(Skill.Woodworking + i);
                    return SynthesisFail;
                }
            }
            if (!canHq) return SynthesisSuccess;

            double chanceHq = (60.0 + 100.0 * c.GetMod(Mod.SYNTH_HQ_RATE) / 512.0) * Settings.CraftHqChanceMultiplier;
            chanceHq = Math.Min(chanceHq, 80.0);
            if (RandRange(0.0, 100.0) > chanceHq) return SynthesisSuccess;
            int upgrade = 0;
            if (RandRange(0.0, 100.0) < 50.0)
            {
                upgrade = 1;
                if (RandRange(0.0, 100.0) < 100.0 / 3.0) upgrade = 2;
            }
            return (byte)(SynthesisHq + upgrade);
        }

        // handleSynthResult: the craft state keeps the tier, the 0x030 effect gets success, fail or HQ
        private byte HandleSynthResult(CharData c, SynthSession synth)
        {
            synth.Result = synth.Desynth ? CalculateDesynthResult(c, synth) : CalculateSynthResult(c, synth);
            return synth.Result switch
            {
                SynthesisFail => ResultFail,
                SynthesisSuccess => ResultSuccess,
                _ => ResultHq,
            };
        }

        // CSynthState::Update for every synthesising char: death crit-fails it, otherwise it ends when the time is up
        private void TickSynths()
        {
            long now = NowMs();
            foreach (var (c, s) in new List<KeyValuePair<CharData, MapSession>>(charSessions_))
            {
                if (c.Synth == null) continue;
                if (c.Hp <= 0) DoSynthCriticalFail(s, c);
                else if (now >= c.Synth.FinishAtMs) SendSynthDone(s, c);
            }
        }

        private static readonly Dictionary<ushort, byte> MaterialLossRates = new Dictionary<ushort, byte>
        {
            [489] = 0, [722] = 100, [860] = 100, [1288] = 100, [1289] = 100, [1290] = 100, [1291] = 100, [1292] = 100,
            [1409] = 100, [2169] = 100, [9091] = 0,
        };

        // handleMaterialLoss: each ingredient breaks at its base rate (50% unless listed) less the loss mods, kept to 20..100
        private void HandleMaterialLoss(CharData c, SynthSession synth)
        {
            int reduction = c.GetMod(Mod.SYNTH_MATERIAL_LOSS) + c.GetMod(ModAt(Mod.SYNTH_MATERIAL_LOSS_FIRE, synth.Element)) +
                            c.GetMod(ModAt(Mod.SYNTH_MATERIAL_LOSS_WOODWORKING, synth.FailingSkill - Skill.Woodworking));
            for (int i = 0; i < 8; i++)
            {
                ushort id = synth.Ingredients[i];
                if (id == 0) continue;
                byte baseRate = MaterialLossRates.TryGetValue(id, out byte rate) ? rate : (byte)50;
                if (baseRate == 0)
                {
                    synth.Saved[i + 1] = true;
                    continue;
                }
                int breakChance = Math.Clamp(baseRate - reduction, 20, 100);
                if (1 + RandInt(100) <= breakChance) synth.Broken[i] = true;
                else synth.Saved[i + 1] = true;
            }
        }

        // synthutils::sendSynthDone
        private void SendSynthDone(MapSession s, CharData c)
        {
            var synth = c.Synth;
            if (synth == null)
            {
                EndSynthAnimation(s, c);
                return;
            }
            if (synth.Result == SynthesisFail)
            {
                HandleMaterialLoss(c, synth);
                PushCharInRange(c, CombineInf(c, SynthFailed), false);
                Push(s, CombineAns(c, SynthFailed, MangledMess, 0));
            }
            else
            {
                var result = synth.Results[synth.Result];
                synth.PendingResult = result;
                byte message = synth.Desynth ? SynthSuccessDesynth : SynthSuccess;
                PushCharInRange(c, CombineInf(c, message, result.ItemId, result.Qty), false);
                Push(s, CombineAns(c, message, result.ItemId, result.Qty));
                // the craft the recipe belongs to: the highest skill required
                uint skillType = 0, highest = 0;
                for (int i = 0; i < 8; i++)
                {
                    if (synth.SkillRequired[i] > highest)
                    {
                        skillType = (uint)(Skill.Woodworking + i);
                        highest = synth.SkillRequired[i];
                    }
                }
                RoeEvent(RoeEventSynthSuccess, c, new (string, object)[] { ("itemid", (uint)result.ItemId), ("skillType", skillType) });
            }
            DoSynthSkillUp(s, c, synth);
            CommitSynth(c, synth);
            EndSynthAnimation(s, c);
        }

        // doSynthCriticalFail: every ingredient is lost
        private void DoSynthCriticalFail(MapSession s, CharData c)
        {
            var synth = c.Synth;
            if (synth == null) return;
            for (int i = 0; i < 8; i++)
                if (synth.Ingredients[i] != 0) synth.Broken[i] = true;
            PushCharInRange(c, CombineInf(c, SynthInterruptedCritical), false);
            Push(s, CombineAns(c, SynthInterruptedCritical, MangledMess, 0));
            CommitSynth(c, synth);
            EndSynthAnimation(s, c);
        }

        // charutils::forceSynthCritFail
        private void ForceSynthCritFail(string source, MapSession s, CharData c)
        {
            if (c.Synth == null) return;
            log_($"{source}: Force crit-failing {c.Name} synthesis!");
            DoSynthCriticalFail(s, c);
        }

        private void EndSynthAnimation(MapSession s, CharData c)
        {
            c.Animation = Anim.None;
            c.UpdateMask |= CharStats.UpdateHp;
            Push(s, CharStatus(c));
            SendUpdateMask(s, c);
        }

        // SynthTransaction::doCommit (a rollback is the same without the result): what was not saved is used up, the
        // stacks are ungreyed, then the result is handed out, signed when the item takes a signature and the crystal is
        // past 0x1080
        private void CommitSynth(CharData c, SynthSession synth)
        {
            synth.Delivering = true;
            var consume = new Dictionary<byte, uint>();
            for (int i = 0; i < 9; i++)
            {
                var item = synth.Claimed[i];
                if (item == null || synth.Saved[i] || !c.Items.Contains(item)) continue;
                consume[item.Slot] = consume.GetValueOrDefault(item.Slot) + 1;
            }
            var tx = synth.Tx;
            foreach (var (slot, quantity) in consume)
                if (!tx.Take(Container.Inventory, slot, quantity))
                    log_($"SynthTransaction: {c.Name} kept {quantity} of the ingredient in slot {slot}");

            foreach (var item in tx.Claims)
                if (c.Items.Contains(item)) PushTo(c, LoginPackets.ItemList(item, LockNormal));

            if (synth.PendingResult is { } result)
            {
                var slot = tx.Give(Container.Inventory, result.ItemId, result.Qty);
                if (slot == null)
                {
                    log_($"SynthTransaction: {c.Name} had no room for result {result.ItemId}, the ingredients are already spent");
                }
                else
                {
                    var item = StorageItem(c, Container.Inventory, (byte)slot.Value);
                    if (item != null && ((ItemBasic(item.ItemId)?.Flags ?? 0) & ItemFlagInscribable) != 0 && synth.Crystal > 0x1080)
                    {
                        ItemSignature.Set(item, c.Name);
                        Exec("UPDATE char_inventory SET signature = ? WHERE charid = ? AND location = 0 AND slot = ? LIMIT 1",
                             BoundValue.Of(c.Name), BoundValue.Of(c.Id), BoundValue.Of((byte)slot.Value));
                    }
                    if (item != null) PushTo(c, GameOkPackets.ItemAttr(item, 0));
                }
                PushTo(c, GameOkPackets.ItemSame(1, CharData.MaxContainers, c.SyncedContainers));
            }
            tx.Commit();
            c.Synth = null;
        }

        private static readonly byte[][] SkillUpAmountWeights =
        {
            new byte[] { 85, 15, 0, 0, 0 }, new byte[] { 85, 15, 0, 0, 0 }, new byte[] { 85, 15, 0, 0, 0 },
            new byte[] { 80, 20, 0, 0, 0 }, new byte[] { 80, 20, 0, 0, 0 }, new byte[] { 70, 30, 0, 0, 0 },
            new byte[] { 70, 30, 0, 0, 0 }, new byte[] { 60, 40, 0, 0, 0 }, new byte[] { 60, 40, 0, 0, 0 },
            new byte[] { 50, 40, 10, 0, 0 }, new byte[] { 40, 40, 20, 0, 0 }, new byte[] { 40, 40, 20, 0, 0 },
            new byte[] { 15, 45, 30, 10, 0 }, new byte[] { 10, 40, 25, 25, 0 }, new byte[] { 0, 40, 30, 20, 10 },
        };

        // doSynthSkillUp: per craft involved, a chance to gain 0.1 .. 0.5 when the recipe is hard enough, and past the
        // common cap the specialization system takes the gain back from the highest other craft
        private void DoSynthSkillUp(MapSession s, CharData c, SynthSession synth)
        {
            var settings = Settings;
            for (int i = 0; i < 8; i++)
            {
                if (synth.SkillRequired[i] == 0) continue;
                int skillId = Skill.Woodworking + i;
                int maxSkill = (c.RealSkillRank[skillId] + 1) * 100;
                int charSkill = c.RealSkills[skillId];
                if (charSkill >= maxSkill) continue;

                int baseDiff = synth.SkillRequired[i] - charSkill / 10;
                if (baseDiff <= (settings.CraftModernSystem ? -11 : 0)) continue;
                if (synth.Result == SynthesisFail && (baseDiff > 5 || baseDiff <= 0)) continue;

                double chance;
                if (synth.Desynth) chance = 0.05;
                else if (settings.CraftModernSystem)
                    chance = baseDiff > 1
                        ? baseDiff * (3.0 - Math.Log(1.2 + charSkill / 100.0)) / 5.0
                        : (3.0 - Math.Log(1.2 + charSkill / 100.0)) / (6 - baseDiff);
                else chance = charSkill < 500 ? 0.6 : 0.25;
                chance += c.GetMod(Mod.SYNTH_SKILL_GAIN) / 100.0;
                chance *= settings.CraftChanceMultiplier;
                if (!synth.Desynth && synth.Result == SynthesisFail) chance /= 2;
                if (Random01() >= chance) continue;

                int amount = 1;
                if (charSkill < 600)
                {
                    var weights = SkillUpAmountWeights[Math.Clamp(baseDiff, 0, 14)];
                    int roll = RandInt(100), cumulative = 0;
                    for (int a = 1; a <= weights.Length; a++)
                    {
                        cumulative += weights[a - 1];
                        if (roll < cumulative)
                        {
                            amount = a;
                            break;
                        }
                    }
                }
                amount = Math.Min(amount, maxSkill - charSkill);

                int commonCap = settings.CraftCommonCap, cumulation = amount, highest = skillId, highestValue = commonCap;
                if (charSkill + amount > commonCap)
                {
                    for (int k = Skill.Woodworking; k <= Skill.Cooking; k++)
                    {
                        if (c.RealSkills[k] <= commonCap) continue;
                        cumulation += c.RealSkills[k] - commonCap;
                        if (k != skillId && c.RealSkills[k] > highestValue)
                        {
                            highest = k;
                            highestValue = c.RealSkills[k];
                        }
                    }
                }

                c.RealSkills[skillId] += (ushort)amount;
                Push(s, SelfPackets.BattleMessage(c, skillId, amount, MsgSkillGain));
                if (charSkill / 10 < (charSkill + amount) / 10)
                {
                    c.WorkingSkills[skillId] += 0x20;
                    if (c.RealSkills[skillId] >= maxSkill) c.WorkingSkills[skillId] |= 0x8000;
                    Push(s, SelfPackets.CliStatus2(c));
                    Push(s, SelfPackets.BattleMessage(c, skillId, (charSkill + amount) / 10, MsgSkillLevelUp));
                }
                SaveCharSkill(c, skillId);

                if (cumulation > settings.CraftSpecializationPoints)
                {
                    c.RealSkills[highest] -= (ushort)amount;
                    Push(s, SelfPackets.BattleMessage(c, highest, amount, MsgSkillDrop));
                    if ((c.RealSkills[highest] + amount) / 10 > c.RealSkills[highest] / 10)
                    {
                        c.WorkingSkills[highest] -= 0x20;
                        Push(s, SelfPackets.CliStatus2(c));
                        Push(s, SelfPackets.BattleMessage(c, highest, (c.RealSkills[highest] - amount) / 10, MsgSkillLevelUp));
                    }
                    SaveCharSkill(c, highest);
                }
            }
        }

        // charutils::SaveCharSkills
        private void SaveCharSkill(CharData c, int skillId) =>
            Exec("INSERT INTO char_skills SET charid = ?, skillid = ?, value = ?, rank = ? ON DUPLICATE KEY UPDATE value = ?, rank = ?",
                 BoundValue.Of(c.Id), BoundValue.Of((byte)skillId), BoundValue.Of(c.RealSkills[skillId]), BoundValue.Of(c.RealSkillRank[skillId]),
                 BoundValue.Of(c.RealSkills[skillId]), BoundValue.Of(c.RealSkillRank[skillId]));

        // CHAR_INRANGE(_SELF): the chars in the same zone and mog house within 50 yalms, and the char itself when asked
        private void PushCharInRange(CharData source, BasicPacket p, bool self)
        {
            foreach (var (c, s) in charSessions_)
            {
                if (c == source)
                {
                    if (self) s.PacketList.AddLast(new BasicPacket(p.Buffer));
                    continue;
                }
                if (!SameArea(c, source) || c.MoghouseId != source.MoghouseId) continue;
                float dx = c.X - source.X, dy = c.Y - source.Y, dz = c.Z - source.Z;
                if (dx * dx + dy * dy + dz * dz <= 50 * 50) s.PacketList.AddLast(new BasicPacket(p.Buffer));
            }
        }

        // 0x030 GP_SERV_COMMAND_EFFECT: the synth animation the crystal plays, with the result it ends on
        private static BasicPacket Effect(CharData c, short effect, byte result)
        {
            var w = new PacketWriter(0x030, 16);
            w.U32(4, c.Id);
            w.U16(8, c.TargId);
            w.U16(10, effect);
            w.U8(12, result);
            w.U8(13, c.Animation);
            return w.Packet;
        }

        // 0x06F GP_SERV_COMMAND_COMBINE_ANS: with a synth open, also its top four crafts, the crystal and ingredients,
        // and which ingredients broke
        private static BasicPacket CombineAns(CharData c, byte result, ushort itemId = 0, byte qty = 0)
        {
            var w = new PacketWriter(0x06F, 56);
            w.U8(4, result);
            if (itemId != 0)
            {
                w.U8(6, qty);
                w.U16(8, itemId);
            }
            var synth = c.Synth;
            if (synth == null) return w.Packet;
            var upKind = new int[4];
            for (int k = 0; k < 4; k++)
            {
                int best = 0;
                for (int skillId = Skill.Woodworking; skillId <= Skill.Cooking; skillId++)
                {
                    if (Array.IndexOf(upKind, skillId) >= 0) continue;
                    int required = synth.SkillRequired[skillId - Skill.Woodworking];
                    if (required > best)
                    {
                        best = required;
                        upKind[k] = skillId;
                    }
                }
                w.U8(26 + k, upKind[k]);
            }
            w.U16(34, synth.Crystal);
            for (int i = 0; i < 8; i++)
            {
                w.U16(36 + 2 * i, synth.Ingredients[i]);
                if (synth.Broken[i]) w.U16(10 + 2 * i, synth.Ingredients[i]);
            }
            return w.Packet;
        }

        // 0x070 GP_SERV_COMMAND_COMBINE_INF: what the chars around see of the synth; a failure packs the broken ingredients
        private static BasicPacket CombineInf(CharData c, byte result, ushort itemId = 0, byte qty = 0)
        {
            var w = new PacketWriter(0x070, 48);
            w.U8(4, result);
            w.U32(26, c.Id);
            w.U16(28, c.TargId);
            if (itemId != 0)
            {
                w.U8(6, qty);
                w.U16(8, itemId);
            }
            if (result == SynthFailed && c.Synth is { } synth)
            {
                int count = 0;
                for (int i = 0; i < 8; i++)
                    if (synth.Broken[i]) w.U16(10 + 2 * count++, synth.Ingredients[i]);
            }
            w.Name(30, c.Name, 16);
            return w.Packet;
        }
    }
}
