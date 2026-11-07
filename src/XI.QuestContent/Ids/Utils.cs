// Hand-written (tools/gen_content_ids.py reads enum tables, not single constants): the utils constants quest ports
// read. XI.Verify.Quests checks each against scripts/utils/utils.lua.
namespace XI.QuestContent
{
    public static class Utils
    {
        // utils.EVENT_CANCELLED_OPTION = bit.lshift(1, 30): the option of an event the client cancelled
        public const int EventCancelledOption = 1 << 30;
    }
}
