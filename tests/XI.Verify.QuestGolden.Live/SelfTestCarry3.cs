using System;

// the carry-harness-3 fixes (TODO's golden harness rows): their pure parts, in the self-test (no database)
internal static partial class Program
{
    private static void SelfTestCarryHarness3()
    {
        Console.WriteLine("golden harness carries 3");
        SelfTestHeldItem();
    }

    // item <Item> ... held: a step whose addItem must add nothing; refused on a setup line
    private static void SelfTestHeldItem()
    {
        const string Head = "char zone=BastokMarkets pos=0,0,0\n";
        string ParseError(string text)
        {
            try
            {
                Flow.Parse(text, "h.flow", "h", null);
                return "-";
            }
            catch (FormatException e)
            {
                return e.Message;
            }
        }
        try
        {
            var f = Flow.Parse(Head + "login\nitem Grapnel held  # the pool's\nitem Grapnel x2\n" +
                               "item Grapnel x1 held\nitem AxeOfTrials wsp=300 held\n", "h.flow", "h", null);
            string Of(int i) => $"{f.Steps[i].Held}/{f.Steps[i].Amount}/{f.Steps[i].WsPoints?.ToString() ?? "-"}/" +
                                $"{f.Steps[i].Label}";
            Check("  item held: a step's last word (after x<n> or wsp=<n> too), off its words and in its label; " +
                  "a plain step is not held",
                  "True/1/-/item Grapnel held|False/2/-/item Grapnel x2|True/1/-/item Grapnel x1 held|" +
                  "True/1/300/item AxeOfTrials wsp=300 held",
                  $"{Of(1)}|{Of(2)}|{Of(3)}|{Of(4)}");
            bool same = Flow.Hash(Head + "login\nitem Grapnel\n") == Flow.Hash(Head + "login\nitem Grapnel held\n");
            Check("  item held: the word is in the flow's hash", "False", same.ToString());
        }
        catch (FormatException e)
        {
            Fail("  item held: parses", e.Message);
        }
        Check("  item held: refused on a setup line (before the login)",
              "h.flow:2: held is a step's (after login): a setup line writes a row",
              ParseError(Head + "item Grapnel held\nlogin\n"));
        Check("  item held: the last word only; another word after it is refused",
              "h.flow:3: held is an item step's last word: item <Item> [x<n>] [wsp=<n>] held",
              ParseError(Head + "login\nitem Grapnel held x1\n"));
        const string Why = ", and a held step's item is held already and adds nothing (the item not held before it?)";
        Check("  item held: quiet when the item was held and the count stayed; one that rose, or none held before " +
              "(a full bag), fails, naming the reads",
              $"-|item 17474 x1 held: the char held 0, then 1{Why}|item 17474 x1 held: the char held 0, then 0{Why}",
              $"{HeldItemProblem(17474, 1, "1,1") ?? "-"}|{HeldItemProblem(17474, 1, "0,1") ?? "-"}|" +
              $"{HeldItemProblem(17474, 1, "0,0") ?? "-"}");
    }
}
