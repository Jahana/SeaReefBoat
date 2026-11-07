namespace XI.Zone
{
    // GP_CLI_COMMAND_ACTION CastMagic (0x01A, ActionID 3): CPlayerController::Cast (canAct's 2.5 s since the last spell
    // finished and the spell off recast, else "unable to cast"; an untargetable target refuses it silently), then
    // CController::Cast / CAIContainer::Internal_Cast into the shared CMagicState (ZoneHost.Magic.cs) when the char's
    // state may change. canAct also gates the weapon skill, job ability, ranged attack and item use. Not ported: the
    // Geo luopan's action offset (no luopans).
    public sealed partial class ZoneHost
    {
        // CPlayerController::canAct
        private bool CanAct(CharData c) => NowMs() - c.LastSpellFinishedMs > CanActAfterSpellMs;

        // CPlayerController::Cast, then CAIContainer::Internal_Cast and ChangeState<CMagicState>
        private bool PlayerCast(CharData c, BattleEntity target, ushort spellId)
        {
            if (!CanAct(c) || RecastsOf(c).Has(RecastType.Magic, spellId))
            {
                PushTo(c, SelfPackets.BattleMessage(c, 0, 0, MsgUnableToCast));
                return false;
            }
            if (IsUntargetable(target)) return false;
            // ChangeState: refused while another state holds it (an item being used is one)
            if (!CanChangeState(c) || c.ItemUse != null) return false;
            return StartMagic(c, target, spellId, NowMs());
        }
    }
}
