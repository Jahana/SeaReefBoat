using System;
using System.Collections.Generic;
using XI.Ipc;
using XI.Map;

namespace XI.Zone
{
    // /nominate and /vote (0x0A0 SWITCH_PROPOSAL, 0x0A1 SWITCH_VOTE: NominateManager, one per zone, keyed by the
    // proposer's name) and /random (0x0A2 DICE).
    public partial class ZoneHost
    {
        private const byte ProposalParty = 0x01, ProposalLinkshell1 = 0x02, ProposalLinkshell2 = 0x03, ProposalSay = 0x05, ProposalShout = 0x06;
        private const byte SwitchProcActive = 0x00, SwitchProcClosed = 0x02;
        private const int ProposalMaxOptions = 8;
        private const long ProposalCooldownMs = 60_000;
        private const ushort MsgStdNoPartyMembers = 36, MsgStdNoLinkshellEquipped = 37, MsgStdDiceRoll = 88,
            MsgStdNotProposedAnything = 143, MsgStdOnlyChooseFromGivenChoices = 144,
            MsgStdAlreadyVotedOnProposal = 145;

        // xirand::GetRandomNumber(n): an int in [0, n)
        public Func<int, int> RandomBelow = Random.Shared.Next;

        // NominateProposal
        private sealed class Proposal
        {
            public uint ProposerId;
            public string ProposerName;
            public ushort ProposerActIndex;
            public byte Kind;
            public ushort AllNum;
            public byte[] Question;
            public List<byte[]> Options;
            public readonly ushort[] VoteTbl = new ushort[9]; // 1-indexed by option
            public readonly HashSet<uint> Voters = new HashSet<uint>();
            public uint PartyId, AllianceId, LinkshellId;

            // NominateProposal::inScope: the voter is in the proposer's party / alliance or holds the shell
            public bool InScope(CharData c) => Kind switch
            {
                ProposalParty => c.Party != null &&
                                 (c.Party.Id == PartyId || AllianceId != 0 && c.Party.Alliance != null && c.Party.Alliance.Id == AllianceId),
                ProposalLinkshell1 or ProposalLinkshell2 => c.Linkshell1 != null && c.Linkshell1.Id == LinkshellId ||
                                                           c.Linkshell2 != null && c.Linkshell2.Id == LinkshellId,
                _ => true,
            };
        }

        // CZone::nominateManager's activeProposals_, per zone
        private readonly Dictionary<ushort, Dictionary<string, Proposal>> proposals_ = new Dictionary<ushort, Dictionary<string, Proposal>>();

        private Dictionary<string, Proposal> ProposalsIn(ushort zone)
        {
            if (!proposals_.TryGetValue(zone, out var map)) proposals_[zone] = map = new Dictionary<string, Proposal>();
            return map;
        }

        private void RegisterNominateHandlers()
        {
            // GP_CLI_COMMAND_SWITCH_PROPOSAL: Kind u8 @4, Str[128] @5
            Register(0x0A0, (s, c, p) => Pv(c)
                    .BlockedBy(BlockedState.InEvent)
                    .OneOf("Kind", p.Buffer[4], ProposalParty, ProposalLinkshell1, ProposalLinkshell2, ProposalSay, ProposalShout),
                (s, c, p) => OnProposal(s, c, p.Buffer[4], UntrustedBytes(p, 5, 128)));

            // GP_CLI_COMMAND_SWITCH_VOTE: Index u8 @4, Name[15] @5
            Register(0x0A1, (s, c, p) => Pv(c).BlockedBy(BlockedState.InEvent),
                (s, c, p) => OnVote(s, c, p.Buffer[4], CborRaw.FromBytes(UntrustedBytes(p, 5, 15))));

            // GP_CLI_COMMAND_DICE: the /random parameter @4 goes unused
            Register(0x0A2, (s, c, p) => Pv(c).BlockedBy(BlockedState.InEvent),
                (s, c, p) => PushCharInRange(c, MessageNamed(c, (uint)RandomBelow(1000), MsgStdDiceRoll), true));
        }

        // NominateManager::onProposal: a /nominate while the char's poll is open closes it; otherwise, after the 60 s
        // cooldown since the last close, a new poll for the scope
        private void OnProposal(MapSession s, CharData c, byte kind, byte[] raw)
        {
            var active = ProposalsIn(c.Zone);
            if (active.TryGetValue(c.Name, out var open))
            {
                FinalizeProposal(c, open);
                active.Remove(c.Name);
                return;
            }
            if (c.LastProposalCloseMs is long closed && NowMs() - closed < ProposalCooldownMs)
            {
                Push(s, SelfPackets.Message(MsgStdCannotUseCommandAtTheMoment));
                return;
            }
            // validateProposerScope
            if (kind == ProposalParty && c.Party == null)
            {
                Push(s, SelfPackets.Message(MsgStdNoPartyMembers));
                return;
            }
            if (kind == ProposalLinkshell1 && c.Linkshell1 == null || kind == ProposalLinkshell2 && c.Linkshell2 == null)
            {
                Push(s, SelfPackets.Message(MsgStdNoLinkshellEquipped));
                return;
            }

            var (question, options) = ParseProposal(raw);
            var proposal = new Proposal
            {
                ProposerId = c.Id, ProposerName = c.Name, ProposerActIndex = c.TargId, Kind = kind,
                AllNum = (ushort)(kind == ProposalParty ? 18 : kind is ProposalLinkshell1 or ProposalLinkshell2 ? 64 : 512),
                Question = question, Options = options,
            };
            if (kind == ProposalParty)
            {
                proposal.PartyId = c.Party.Id;
                proposal.AllianceId = c.Party.Alliance?.Id ?? 0;
            }
            else if (kind == ProposalLinkshell1) proposal.LinkshellId = c.Linkshell1.Id;
            else if (kind == ProposalLinkshell2) proposal.LinkshellId = c.Linkshell2.Id;

            BroadcastProposalStart(proposal);
            active[c.Name] = proposal;
        }

        // NominateManager::onVote: one vote per char in the poll of that name in this zone
        private void OnVote(MapSession s, CharData c, byte index, string proposerName)
        {
            if (!ProposalsIn(c.Zone).TryGetValue(proposerName, out var proposal) || !proposal.InScope(c))
            {
                Push(s, MessageString2(proposerName, MsgStdNotProposedAnything));
                return;
            }
            if (proposal.Voters.Contains(c.Id))
            {
                Push(s, MessageString2(proposerName, MsgStdAlreadyVotedOnProposal));
                return;
            }
            int choices = proposal.Options.Count;
            if (choices == 0 || index < 1 || index > choices)
            {
                Push(s, ParamMessage((uint)choices, MsgStdOnlyChooseFromGivenChoices));
                return;
            }

            proposal.VoteTbl[index]++;
            proposal.Voters.Add(c.Id);
            // the live tally to the voter and the proposer
            Push(s, SwitchProc(proposal, false));
            var (proposer, ps) = CharById(proposal.ProposerId);
            if (proposer != null && proposer != c) Push(ps, SwitchProc(proposal, false));
        }

        // NominateManager::onCharLeavingZone (removeCharFromZone): the char's poll closes with it
        private void LeaveNominate(CharData c)
        {
            if (!proposals_.TryGetValue(c.Zone, out var active) || !active.TryGetValue(c.Name, out var proposal)) return;
            FinalizeProposal(c, proposal);
            active.Remove(c.Name);
        }

        // NominateManager::finalize: the results to the proposer and every voter, and the cooldown starts
        private void FinalizeProposal(CharData c, Proposal proposal)
        {
            var (proposer, ps) = CharById(proposal.ProposerId);
            if (proposer != null) Push(ps, SwitchProc(proposal, true));
            foreach (uint voterId in proposal.Voters)
            {
                if (voterId == proposal.ProposerId) continue;
                var (voter, vs) = CharById(voterId);
                if (voter != null) Push(vs, SwitchProc(proposal, true));
            }
            c.LastProposalCloseMs = NowMs();
        }

        // NominateManager::broadcastStart: say in range with the proposer, shout to the shout range and the proposer,
        // party to the alliance and linkshell to the shell's members in the proposer's zone
        private void BroadcastProposalStart(Proposal proposal)
        {
            var (proposer, ps) = CharById(proposal.ProposerId);
            if (proposer == null) return;
            var packet = SwitchStart(proposal);
            switch (proposal.Kind)
            {
                case ProposalSay:
                    PushCharInRange(proposer, packet, true);
                    break;
                case ProposalShout:
                    Push(ps, new BasicPacket(packet.Buffer));
                    PushCharInShout(proposer, packet);
                    break;
                case ProposalParty:
                    foreach (var member in ForAlliance(proposer))
                        if (SameArea(member, proposer)) PushTo(member, new BasicPacket(packet.Buffer));
                    break;
                default:
                    var shell = proposal.Kind == ProposalLinkshell1 ? proposer.Linkshell1 : proposer.Linkshell2;
                    if (shell == null) break;
                    foreach (var member in shell.Members)
                        if (SameArea(member, proposer)) PushTo(member, new BasicPacket(packet.Buffer));
                    break;
            }
        }

        // parseInput: split on spaces and tabs outside quotes ("" is a token); the first token is the question, up to 8
        // more the options, none offering 8 blank ones
        private static (byte[] Question, List<byte[]> Options) ParseProposal(byte[] raw)
        {
            var tokens = new List<byte[]>();
            var cur = new List<byte>();
            bool inQuotes = false, hasToken = false;
            void Flush()
            {
                if (!hasToken) return;
                tokens.Add(cur.ToArray());
                cur.Clear();
                hasToken = false;
            }
            foreach (byte b in raw)
            {
                if (b == (byte)'"')
                {
                    inQuotes = !inQuotes;
                    hasToken = true;
                    continue;
                }
                if (!inQuotes && (b == (byte)' ' || b == (byte)'\t'))
                {
                    Flush();
                    continue;
                }
                cur.Add(b);
                hasToken = true;
            }
            Flush();

            var question = Array.Empty<byte>();
            var options = new List<byte[]>();
            if (tokens.Count > 0)
            {
                question = tokens[0];
                for (int i = 1; i < tokens.Count && options.Count < ProposalMaxOptions; i++) options.Add(tokens[i]);
            }
            if (options.Count == 0)
                for (int i = 0; i < ProposalMaxOptions; i++) options.Add(Array.Empty<byte>());
            return (question, options);
        }

        // formatBody: "[question]\n1:opt1\n2:opt2..." or with the tallies "[question]\n1[N]:opt1..."; without a question
        // the start body is empty and the results "1[N] 2[N] ..."
        private static byte[] ProposalBody(Proposal proposal, bool withTallies)
        {
            var out_ = new List<byte>();
            void Text(string t) => out_.AddRange(CborRaw.Bytes(t));
            if (proposal.Question.Length == 0)
            {
                for (int n = 1; withTallies && n <= proposal.Options.Count; n++) Text($"{n}[{proposal.VoteTbl[n]}] ");
                return out_.ToArray();
            }
            Text("[");
            out_.AddRange(proposal.Question);
            Text("]");
            for (int i = 0; i < proposal.Options.Count; i++)
            {
                int n = i + 1;
                Text(withTallies ? $"\n{n}[{proposal.VoteTbl[n]}]:" : $"\n{n}:");
                out_.AddRange(proposal.Options[i]);
            }
            return out_.ToArray();
        }

        // 0x078 GP_SERV_COMMAND_SWITCH_START: UniqueNo u32 @4, AllNum u32 @8, ActIndex u16 @12, sName[15] @14, Kind u8 @29,
        // Str[224] @30
        private static BasicPacket SwitchStart(Proposal proposal)
        {
            var body = ProposalBody(proposal, false);
            int len = Math.Min(body.Length, 223);
            var w = new PacketWriter(0x078, (uint)((30 + len + 1 + 3) & ~3));
            w.U32(4, proposal.ProposerId);
            w.U32(8, proposal.AllNum);
            w.U16(12, proposal.ProposerActIndex);
            w.Name(14, proposal.ProposerName, 15);
            w.U8(29, proposal.Kind);
            w.Bytes(30, body.AsSpan(0, len));
            return w.Packet;
        }

        // 0x079 GP_SERV_COMMAND_SWITCH_PROC: AllNum u32 @4, VoteNumTbl u16[9] @8, Kind u8 @26, State u8 @27, QuestionNum u8
        // @28 (1 + the options), sPropName[15] @29, Str[256] @44 (the results, only once closed)
        private static BasicPacket SwitchProc(Proposal proposal, bool isFinal)
        {
            var body = isFinal ? ProposalBody(proposal, true) : Array.Empty<byte>();
            int len = Math.Min(body.Length, 255);
            var w = new PacketWriter(0x079, (uint)((44 + len + 4 + 3) & ~3));
            w.U32(4, proposal.AllNum);
            for (int i = 0; i < 9; i++) w.U16(8 + 2 * i, proposal.VoteTbl[i]);
            w.U8(26, proposal.Kind);
            w.U8(27, isFinal ? SwitchProcClosed : SwitchProcActive);
            w.U8(28, 1 + proposal.Options.Count);
            w.Name(29, proposal.ProposerName, 15);
            w.Bytes(44, body.AsSpan(0, len));
            return w.Packet;
        }
    }
}
