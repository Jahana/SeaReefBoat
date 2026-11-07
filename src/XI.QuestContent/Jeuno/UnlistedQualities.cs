// Unlisted Qualities (Log ID 3, Quest ID 77), a line-for-line port of scripts/quests/jeuno/Unlisted_Qualities.lua
// (quest rework B3b, Jeuno group 13). Luto Mewrilah : !pos -52 0 46 244; Red Ghost : !pos -99 0 0 246; Akta : !pos 6 0
// -68.6 243; Kuah Dakonsa : !pos -40 0 -65 245; Bheem : !pos -89 0 168 244. The adventuring fellow's making. Available:
// Luto Mewrilah's 10031 (importantOnce); its finish's option names the fellow (bits 0-3 the name, 4-6 the race): a name
// over 7 or the cancel option leaves it available, else begun with Data = the packed name and race (personality, face
// and size unset). Prog 0: Luto's 10033, Bheem's 10037 (oncePerZone); Akta's 10103 while the size is unset (option 1-3:
// the size), Kuah Dakonsa's 20000 while the face is unset (option 0-15: the face), Red Ghost's 320 while the
// personality is unset (option 0-5 for a male fellow, 6-11 for a female one: the personality, stored + 1); each event
// carries Data as its option [7], and once all three are set Prog is 1. Prog 1: Bheem's 10171 (Data as [7]); its finish
// (not cancelled): Prog 2. Prog 2: Luto's 10032, with no finish: the Lua's completion is commented out ("until core
// changes made to save Fellows"), so the quest never completes.
// Kept Lua quirks:
// - Red Ghost's 320 guard `option < 0 and option > 11` is never true (and, not or): an out-of-range option goes on to
//   the male / female test, which refuses it anyway.
// - The 10031 guard `race > 7` is never true (race is masked to 3 bits).
// - Kuah Dakonsa's option 15 is accepted, but packs as face 15, which unpackData reads as unset (-1): the face stays
//   unset and Kuah answers again.
// - Bheem's ['Bheem'].onEventUpdate (player:updateEvent({ [7] = data })) is never run: the lookup finds an
//   onEventUpdate by its csid under the zone's onEventUpdate key (interaction_lookup.lua:524-525), never under an NPC's
//   name, so the handler is dead in Lua and not ported (no builder form either: OnEventUpdate is keyed by csid).
// - A Data of 0 (a quest accepted with no Data, as a GM leaves it) reads face 0 as set, and personality and size as
//   unset.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Jeuno
{
    public sealed class UnlistedQualities : QuestScript
    {
        public UnlistedQualities() : base(QuestLog.Jeuno, "UNLISTED_QUALITIES") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/jeuno/Unlisted_Qualities";

        // Packs Fellow properties into a single integer, as expected by events.
        // 0000 0000 FFFF SS00 00PP PP00 0RRR NNNN
        // Face, Size, Personality, Race, Name
        static int PackData(int name, int race, int pers, int face, int size)
        {
            return name |
                race << 4 |
                (pers >= 1 ? pers : 0) << 10 |
                (size >= 1 ? size : 0) << 18 |
                (face >= 0 ? face : 15) << 20;
        }

        static (int Name, int Race, int Pers, int Size, int Face) UnpackData(int packed)
        {
            int rawPers = (packed >> 10) & 0x0F;
            int rawSize = (packed >> 18) & 0x03;
            int rawFace = (packed >> 20) & 0x0F;

            return (
                packed & 0x0F,
                (packed >> 4) & 0x07,
                rawPers >= 1 ? rawPers : -1,
                rawSize >= 1 ? rawSize : -1,
                rawFace < 15 ? rawFace : -1);
        }

        // Check if all 3 NPC info gathered, advance to Bheem phase
        static void CheckAdvanceToPhase1(IQuestContext q)
        {
            var d = UnpackData(q.Quest.Var("Data"));
            if (d.Pers >= 1 && d.Face >= 0 && d.Size >= 1)
            {
                q.Quest.SetStep(1);
            }
        }

        protected override void Build(QuestScriptBuilder b)
        {
            b.Steps(1, 2);

            // Quest start: Talk to Luto, select name/race
            b.State(QuestStatus.Available)
                .Npc(Zone.UpperJeuno, Npc.UpperJeuno.LutoMewrilah).OnTrigger(QuestReply.Event(10031).ImportantOnce())
                .OnEventFinish(Zone.UpperJeuno, 10031, q =>
                {
                    if (q.Option == Utils.EventCancelledOption)
                    {
                        return;
                    }

                    // Parse race and name from option (0-indexed)
                    int name = q.Option & 0x0F;
                    int race = (q.Option >> 4) & 0x07;

                    // 8 races, 8 names per race.
                    if (race > 7 || name > 7)
                    {
                        return;
                    }

                    q.Quest.Begin();
                    q.Quest.SetVar("Data", PackData(name, race, -1, -1, -1));
                });

            // Prog 0: Gather info from NPCs (size, face, personality)
            b.State(QuestStatus.Accepted, step: 0)
                .Npc(Zone.UpperJeuno, Npc.UpperJeuno.LutoMewrilah).OnTrigger(QuestReply.Event(10033))
                .Npc(Zone.UpperJeuno, Npc.UpperJeuno.Bheem).OnTrigger(QuestReply.Event(10037).OncePerZone())
                .Npc(Zone.RuludeGardens, Npc.RuludeGardens.Akta).OnTrigger(q =>
                {
                    int data = q.Quest.Var("Data");
                    var d = UnpackData(data);

                    if (d.Size == -1)
                    {
                        return QuestReply.Event(10103).Option(7, data);
                    }
                    return null;
                })
                .OnEventFinish(Zone.RuludeGardens, 10103, q =>
                {
                    // 1 = Small, 2 = Average Height, 3 = Pretty Tall
                    if (q.Option < 1 || q.Option > 3)
                    {
                        return;
                    }

                    var d = UnpackData(q.Quest.Var("Data"));
                    q.Quest.SetVar("Data", PackData(d.Name, d.Race, d.Pers, d.Face, q.Option));
                    CheckAdvanceToPhase1(q);
                })
                .Npc(Zone.LowerJeuno, Npc.LowerJeuno.KuahDakonsa).OnTrigger(q =>
                {
                    int data = q.Quest.Var("Data");
                    var d = UnpackData(data);

                    if (d.Face == -1)
                    {
                        return QuestReply.Event(20000).Option(7, data);
                    }
                    return null;
                })
                .OnEventFinish(Zone.LowerJeuno, 20000, q =>
                {
                    // Event returns 0-15 for faces 1A-8B
                    if (q.Option < 0 || q.Option > 15)
                    {
                        return;
                    }

                    var d = UnpackData(q.Quest.Var("Data"));
                    q.Quest.SetVar("Data", PackData(d.Name, d.Race, d.Pers, q.Option, d.Size));
                    CheckAdvanceToPhase1(q);
                })
                .Npc(Zone.PortJeuno, Npc.PortJeuno.RedGhost).OnTrigger(q =>
                {
                    int data = q.Quest.Var("Data");
                    var d = UnpackData(data);

                    if (d.Pers == -1)
                    {
                        return QuestReply.Event(320).Option(7, data);
                    }
                    return null;
                })
                .OnEventFinish(Zone.PortJeuno, 320, q =>
                {
                    // the Lua's guard as it is: never true (and, not or)
                    if (q.Option < 0 && q.Option > 11)
                    {
                        return;
                    }

                    var d = UnpackData(q.Quest.Var("Data"));
                    // Race is 0-indexed here (unlike xi.race enum)
                    bool isFemale = (d.Race % 2 == 1 && d.Race != 7) || d.Race == 6;

                    // Red Ghost returns 0-5 for male, 6-11 for female
                    bool validMale = q.Option >= 0 && q.Option <= 5;
                    bool validFemale = q.Option >= 6 && q.Option <= 11;

                    if ((isFemale && validFemale) || (!isFemale && validMale))
                    {
                        // This gets stored as +1 as Bheem expects it 1-indexed.
                        q.Quest.SetVar("Data", PackData(d.Name, d.Race, q.Option + 1, d.Face, d.Size));
                        CheckAdvanceToPhase1(q);
                    }
                });

            // Prog 1: Talk to Bheem after gathering all info
            b.State(QuestStatus.Accepted, step: 1)
                .Npc(Zone.UpperJeuno, Npc.UpperJeuno.Bheem).OnTrigger(q =>
                {
                    int data = q.Quest.Var("Data");
                    return QuestReply.Event(10171).Option(7, data);
                })
                // ['Bheem'].onEventUpdate (player:updateEvent({ [7] = data })): dead in Lua (see the header), not ported
                .OnEventFinish(Zone.UpperJeuno, 10171, q =>
                {
                    if (q.Option == Utils.EventCancelledOption)
                    {
                        return;
                    }

                    q.Quest.SetStep(2);
                });

            // Prog 2: Return to Luto Mewrilah to complete
            b.State(QuestStatus.Accepted, step: 2)
                .Npc(Zone.UpperJeuno, Npc.UpperJeuno.LutoMewrilah).OnTrigger(QuestReply.ProgressEvent(10032));
            // TODO: Quest completion disabled until core changes made to save Fellows (the Lua's commented-out 10032
            // finish: option 0 and npcUtil.giveItem(player, xi.item.SILVER_INGOT), then quest:complete)
        }
    }
}
