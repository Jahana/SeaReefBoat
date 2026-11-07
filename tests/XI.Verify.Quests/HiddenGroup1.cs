// XI.Verify.Quests -- quest rework B3b hidden quests group 1 (the twelve trust quests): each definition validates
// clean; what the traces do not pin (each check clause by clause, the setting, the reply kinds and the memory numbers).
using System;
using System.Linq;
using XI.Quests;
using XI.Quests.Authoring;
using QC = XI.QuestContent;

internal static partial class Program
{
    static void RunHiddenGroup1()
    {
        Console.WriteLine("== quest rework B3b hidden quests group 1: the trusts; what the traces do not pin ==");
        var quests = new (string Name, QuestScript Script, Action<Fake> Ready)[]
        {
            ("Abquhbah", new QC.HiddenQuests.TrustAbquhbah(), f =>
            {
                f.CompletedMissions.Add($"{(int)QC.MissionLog.Toau}/{QC.Mission.Toau.ImmortalSentries}");
                f.CurrentMissions[(int)QC.MissionLog.Rov] = QC.Mission.Rov.EverForward;
            }),
            ("Cherukiki", new QC.HiddenQuests.TrustCherukiki(),
                f => f.CurrentMissions[(int)QC.MissionLog.Cop] = QC.Mission.Cop.Dawn + 1),
            ("Halver", new QC.HiddenQuests.TrustHalver(),
                f => f.CompletedMissions.Add($"{(int)QC.MissionLog.Rov}/{QC.Mission.Rov.ThePathUntraveled}")),
            ("Ingrid", new QC.HiddenQuests.TrustIngrid(),
                f => f.CompletedMissions.Add($"{(int)QC.MissionLog.Soa}/{QC.Mission.Soa.TheMercilessOne}")),
            ("Maat", new QC.HiddenQuests.TrustMaat(), f => { }),
            ("Nashmeira", new QC.HiddenQuests.TrustNashmeira(),
                f => f.CompletedMissions.Add($"{(int)QC.MissionLog.Toau}/{QC.Mission.Toau.TheEmpressCrowned}")),
            ("Prishe", new QC.HiddenQuests.TrustPrishe(), f =>
            {
                f.CurrentMissions[(int)QC.MissionLog.Cop] = QC.Mission.Cop.Dawn + 1;
                f.MissionVars[$"{(int)QC.MissionLog.Cop}/{QC.Mission.Cop.Dawn}/Status"] = 5;
            }),
            ("Semih", new QC.HiddenQuests.TrustSemih(),
                f => f.CompletedMissions.Add($"{(int)QC.MissionLog.Rov}/{QC.Mission.Rov.ThePathUntraveled}")),
            ("Shantotto", new QC.HiddenQuests.TrustShantotto(), f => { }),
            ("Shikaree Z", new QC.HiddenQuests.TrustShikareeZ(),
                f => f.CompletedMissions.Add($"{(int)QC.MissionLog.Cop}/{QC.Mission.Cop.ThreePaths}")),
            ("Ulmia", new QC.HiddenQuests.TrustUlmia(), f =>
            {
                f.CurrentMissions[(int)QC.MissionLog.Cop] = QC.Mission.Cop.Dawn + 1;
                f.MissionVars[$"{(int)QC.MissionLog.Cop}/{QC.Mission.Cop.Dawn}/Status"] = 2;
            }),
            ("Zeid II", new QC.HiddenQuests.TrustZeidII(),
                f => f.CompletedMissions.Add($"{(int)QC.MissionLog.Rov}/{QC.Mission.Rov.VoltoOscuro}")),
        };
        foreach (var (name, script, _) in quests)
            Check($"hidden g1:{name} clean", "", string.Join(" | ", QuestScriptValidator.Validate(script.Compile())));

        // The setting: no flow turns ENABLE_TRUST_QUESTS off (the lane's setting is 1), so the fake does. A char the
        // check holds for answers true with the setting 1, false with it 0 or unset (Lua's nil is not 1).
        foreach (var (name, script, ready) in quests)
        {
            var st = script.Compile().States[0];
            string Holds(int? setting)
            {
                var f = new Fake { Hidden = true };
                f.KeyItems[QC.KeyItem.WindurstTrustPermit] = true;
                ready(f);
                if (setting is int s) f.MainSettings["ENABLE_TRUST_QUESTS"] = s;
                return st.IsMatch(QuestStatus.Available, 0, f).ToString();
            }
            Check($"hidden g1:{name}'s check reads ENABLE_TRUST_QUESTS", "True|False|False",
                  $"{Holds(1)}|{Holds(0)}|{Holds(null)}");
        }
        RunHiddenGroup1Kinds();
        RunHiddenGroup1Checks(quests);
    }

    // the kinds and numbers kept as extra guards: Event vs Progress (the Lua's startEvent / the engine's progress events
    // leave the same packets on these fixtures; the fix-round flows kill the kind mutants of six of the eight pinned
    // quests, only Prishe's and Ulmia's kinds are unseen), the memory bits and the Complete call of a quest the char
    // cannot see
    static void RunHiddenGroup1Kinds()
    {
        var prishe = new QC.HiddenQuests.TrustPrishe().Compile();
        var ulmia = new QC.HiddenQuests.TrustUlmia().Compile();
        var ingrid = new QC.HiddenQuests.TrustIngrid().Compile();
        string Door(QuestScriptDefinition def, Action<Fake> setup)
        {
            var f = new Fake { Hidden = true };
            setup(f);
            var trigger = def.States.SelectMany(s => s.Handlers)
                .Single(x => x.Kind == QuestHandlerKind.OnTrigger);
            return AU1Describe(trigger, f);
        }
        Check("hidden g1:Prishe's door is a progress event", "ProgressEvent(633,0,0,0,0)", Door(prishe, f => { }));
        Check("hidden g1:Prishe's door tells In the Mood for Love 2", "ProgressEvent(633,0,0,0,2)",
              Door(prishe, f => f.CompletedQuests.Add("Jeuno/IN_THE_MOOD_FOR_LOVE")));
        Check("hidden g1:Prishe's door tells A Chocobo's Tale 4", "ProgressEvent(633,0,0,0,4)",
              Door(prishe, f => f.CompletedQuests.Add("Jeuno/A_CHOCOBOS_TALE")));
        Check("hidden g1:Prishe's door tells Zilart mission 71 as 8", "ProgressEvent(633,0,0,0,8)",
              Door(prishe, f => f.CompletedMissions.Add("3/71")));
        Check("hidden g1:Prishe's door tells Zilart mission 89 as 16", "ProgressEvent(633,0,0,0,16)",
              Door(prishe, f => f.CompletedMissions.Add("3/89")));
        Check("hidden g1:Ulmia's gate is a progress event", "ProgressEvent(560,0,0,0,0)", Door(ulmia, f => { }));
        Check("hidden g1:Ulmia's gate tells Apocalypse Nigh 2", "ProgressEvent(560,0,0,0,2)",
              Door(ulmia, f => f.CompletedQuests.Add("Jeuno/APOCALYPSE_NIGH")));
        Check("hidden g1:Ingrid's door is a plain event", "Event(5062,257,0,0,255,255,4352,1)", Door(ingrid, f => { }));

        // the finish records the quest as complete (hidden: it only records, so no log shows it)
        foreach (var (name, def, csid) in new[] { ("Prishe", prishe, 633), ("Ulmia", ulmia, 560) })
            Check($"hidden g1:{name}'s finish completes the quest", "True", AU1Finish(def, csid, 2, true)
                  .Contains("Quest.Complete").ToString());

        // the rest of the replies and finishes, as the fake records them (extra guards for the Event / Progress kind: only
        // Prishe's and Ulmia's kinds are unseen by every flow)
        var cherukiki = new QC.HiddenQuests.TrustCherukiki().Compile();
        var maat = new QC.HiddenQuests.TrustMaat().Compile();
        var nashmeira = new QC.HiddenQuests.TrustNashmeira().Compile();
        var abquhbah = new QC.HiddenQuests.TrustAbquhbah().Compile();
        var shantotto = new QC.HiddenQuests.TrustShantotto().Compile();
        var shikaree = new QC.HiddenQuests.TrustShikareeZ().Compile();
        string Finish(QuestScriptDefinition def, int csid, int option, bool completes, Action<Fake> setup = null)
        {
            var h = def.States.SelectMany(s => s.Handlers)
                .Single(x => x.Kind == QuestHandlerKind.OnEventFinish && x.Id == csid);
            var f = new Fake
            {
                Status = QuestStatus.Accepted, Csid = csid, Option = option, CompleteResult = completes,
            };
            setup?.Invoke(f);
            h.Run(f);
            return string.Join(";", f.Calls);
        }
        Check("hidden g1:Cherukiki's door is a progress event", "ProgressEvent(10235,0,0,0,0)",
              Door(cherukiki, f => { }));
        Check("hidden g1:Nashmeira's door is a progress event", "ProgressEvent(5092,{text_table=0})",
              Door(nashmeira, f => { }));
        foreach (var (name, def, csid, learn) in new[]
        {
            ("Cherukiki", cherukiki, 10235, "Player.AddSpell(916,True);Text.Special(243:YOU_LEARNED_TRUST,0,916)"),
            ("Maat", maat, 10241, "Player.AddSpell(933,True);Text.Special(243:YOU_LEARNED_TRUST,0,933)"),
            ("Nashmeira", nashmeira, 5092, "Player.AddSpell(923,True);Text.Special(50:YOU_LEARNED_TRUST,0,923)"),
        })
        {
            Check($"hidden g1:{name}'s finish on option 2 completes and teaches", "Quest.Complete();" + learn,
                  Finish(def, csid, 2, true));
            Check($"hidden g1:{name}'s finish teaches nothing when the quest does not complete", "Quest.Complete()",
                  Finish(def, csid, 2, false));
            Check($"hidden g1:{name}'s finish on another option does nothing", "", Finish(def, csid, 1, true));
        }

        // Maat: six of the 16 low bits of maatsCap (a bit above them does not count)
        string Cap(int mask) => Door(maat, f => f.CharVars["maatsCap"] = mask);
        Check("hidden g1:Maat's door counts the 16 low bits", "Event(10242)|Event(10241)|Event(10241)|Event(10242)",
              string.Join("|", Cap(0x1F), Cap(0x3F), Cap(0x801F), Cap(0x1001F)));

        // Abquhbah: the permit and the nation in the options, the cipher only while the permit is held
        Check("hidden g1:Abquhbah's door carries the permit and the nation",
              "ProgressEvent(170,{[1]=1,[2]=2,text_table=0})",
              Door(abquhbah, f => { f.KeyItems[QC.KeyItem.BastokTrustPermit] = true; f.NationId = 2; }));
        Check("hidden g1:Abquhbah's door without a permit", "ProgressEvent(170,{[1]=0,[2]=2,text_table=0})",
              Door(abquhbah, f => f.NationId = 2));
        Check("hidden g1:Abquhbah's finish gives the cipher with a permit", "Player.GiveItem(10169)",
              Finish(abquhbah, 170, 1, true, f => f.KeyItems[QC.KeyItem.SanDoriaTrustPermit] = true));
        Check("hidden g1:Abquhbah's finish gives nothing without a permit", "", Finish(abquhbah, 170, 1, true));

        // Shikaree Z: the finish teaches only with a permit
        Check("hidden g1:Shikaree Z's finish teaches with a permit",
              "Player.AddSpell(915,True);Text.Special(241:YOU_LEARNED_TRUST,0,915)",
              Finish(shikaree, 869, 1, true, f => f.KeyItems[QC.KeyItem.WindurstTrustPermit] = true));
        Check("hidden g1:Shikaree Z's finish teaches nothing without one", "", Finish(shikaree, 869, 1, true));

        // Shantotto: the progress event needs Curses, Foiled... and each of the nine nation trusts
        var nine = new[] { 898, 901, 904, 899, 902, 905, 900, 897, 903 };
        void Nine(Fake f, int skip = 0)
        {
            f.CompletedQuests.Add("Windurst/CURSES_FOILED_A_GOLEM");
            foreach (var s in nine) if (s != skip) f.Spells.Add(s);
        }
        Check("hidden g1:Shantotto's door with the nine trusts", "ProgressEvent(529,{[3]=0,[7]=1})",
              Door(shantotto, f => Nine(f)));
        Check("hidden g1:Shantotto's door without the golem quest", "Event(529):importantEvent()",
              Door(shantotto, f => { Nine(f); f.CompletedQuests.Clear(); }));
        foreach (var skip in nine)
            Check($"hidden g1:Shantotto's door needs trust {skip}", "Event(529):importantEvent()",
                  Door(shantotto, f => Nine(f, skip)));
        Check("hidden g1:Shantotto's finish teaches on option 2",
              "Player.AddSpell(896,True);Text.Special(239:YOU_LEARNED_TRUST,0,896)", Finish(shantotto, 529, 2, true));
        Check("hidden g1:Shantotto's finish on another option does nothing", "", Finish(shantotto, 529, 1, true));
    }

    // each quest's check, clause by clause: true with its ready char, false with that one clause broken (only some
    // quests' traces have a char that fails exactly one clause)
    static void RunHiddenGroup1Checks((string Name, QuestScript Script, Action<Fake> Ready)[] quests)
    {
        var spells = new System.Collections.Generic.Dictionary<string, int>
        {
            ["Abquhbah"] = 982, ["Cherukiki"] = 916, ["Halver"] = 972, ["Ingrid"] = 921, ["Maat"] = 933,
            ["Nashmeira"] = 923, ["Prishe"] = 913, ["Semih"] = 940, ["Shantotto"] = 896, ["Shikaree Z"] = 915,
            ["Ulmia"] = 914, ["Zeid II"] = 1010,
        };
        var ciphers = new System.Collections.Generic.Dictionary<string, int>
        {
            ["Abquhbah"] = QC.Item.CipherOfAbquhbahsAlterEgo, ["Halver"] = QC.Item.CipherOfHalversAlterEgo,
            ["Semih"] = QC.Item.CipherOfSemihsAlterEgo, ["Zeid II"] = QC.Item.CipherOfZeidsAlterEgoIi,
        };
        var nation = new[]
        {
            QC.KeyItem.WindurstTrustPermit, QC.KeyItem.BastokTrustPermit, QC.KeyItem.SanDoriaTrustPermit,
        };
        foreach (var (name, script, ready) in quests)
        {
            var st = script.Compile().States[0];
            string Holds(Action<Fake> change, bool permit = true)
            {
                var f = new Fake { Hidden = true };
                if (permit) f.KeyItems[QC.KeyItem.WindurstTrustPermit] = true;
                ready(f);
                change(f);
                f.MainSettings["ENABLE_TRUST_QUESTS"] = 1;
                return st.IsMatch(QuestStatus.Available, 0, f).ToString();
            }
            void Is(string what, bool expected, string actual) =>
                Check($"hidden g1:{name}: {what}", expected.ToString(), actual);
            Is("holds for the ready char", true, Holds(f => { }));
            Is("not once the trust is learned", false, Holds(f => f.Spells.Add(spells[name])));
            if (ciphers.TryGetValue(name, out var cipher))
                Is("not while its cipher is held", false, Holds(f => f.Items.Add(cipher)));
            if (name is "Shantotto" or "Shikaree Z")
            {
                Is("needs the Windurst permit", false, Holds(f => { }, permit: false));
                Is("the Bastok permit is not enough", false,
                   Holds(f => f.KeyItems[QC.KeyItem.BastokTrustPermit] = true, permit: false));
            }
            else if (name != "Zeid II")
            {
                Is("needs a trust permit", false, Holds(f => { }, permit: false));
                foreach (var ki in nation)
                    Is($"the permit {ki} alone is enough", true, Holds(f => f.KeyItems[ki] = true, permit: false));
            }
            else
                Is("needs no trust permit", true, Holds(f => { }, permit: false));
        }

        string Avail(string name, Action<Fake> change)
        {
            var (_, script, ready) = quests.First(x => x.Name == name);
            var f = new Fake { Hidden = true };
            f.KeyItems[QC.KeyItem.WindurstTrustPermit] = true;
            ready(f);
            change(f);
            f.MainSettings["ENABLE_TRUST_QUESTS"] = 1;
            return script.Compile().States[0].IsMatch(QuestStatus.Available, 0, f).ToString();
        }
        int Cop(Fake f, int mission) => f.CurrentMissions[(int)QC.MissionLog.Cop] = mission;
        string Key(int log, int mission) => $"{log}/{mission}";

        // the missions each check asks for
        foreach (var (name, log, mission) in new[]
        {
            ("Abquhbah", QC.MissionLog.Toau, QC.Mission.Toau.ImmortalSentries),
            ("Halver", QC.MissionLog.Rov, QC.Mission.Rov.ThePathUntraveled),
            ("Ingrid", QC.MissionLog.Soa, QC.Mission.Soa.TheMercilessOne),
            ("Nashmeira", QC.MissionLog.Toau, QC.Mission.Toau.TheEmpressCrowned),
            ("Semih", QC.MissionLog.Rov, QC.Mission.Rov.ThePathUntraveled),
            ("Shikaree Z", QC.MissionLog.Cop, QC.Mission.Cop.ThreePaths),
            ("Zeid II", QC.MissionLog.Rov, QC.Mission.Rov.VoltoOscuro),
        })
            Check($"hidden g1:{name}: needs its mission completed", "False",
                  Avail(name, f => f.CompletedMissions.Remove(Key(log, mission))));
        string Rov(int mission) => Avail("Abquhbah", f => f.CurrentMissions[(int)QC.MissionLog.Rov] = mission);
        Check("hidden g1:Abquhbah: needs Rov at Ever Forward or later", "False|True|True",
              $"{Rov(QC.Mission.Rov.EverForward - 1)}|{Rov(QC.Mission.Rov.EverForward)}|" +
              Rov(QC.Mission.Rov.EverForward + 1));

        // Cherukiki: between Chains and Bonds and The Warrior's Path (exclusive), or at Dawn with Status 2, or after
        string Chk(int mission, int? status = null) => Avail("Cherukiki", f =>
        {
            Cop(f, mission);
            if (status is int st) f.MissionVars[$"{(int)QC.MissionLog.Cop}/{QC.Mission.Cop.Dawn}/Status"] = st;
        });
        Check("hidden g1:Cherukiki: the missions", "False|True|True|False|False|True|False|True|True",
              string.Join("|", new[]
              {
                  Chk(QC.Mission.Cop.ChainsAndBonds), Chk(QC.Mission.Cop.ChainsAndBonds + 1),
                  Chk(QC.Mission.Cop.TheWarriorsPath - 1), Chk(QC.Mission.Cop.TheWarriorsPath),
                  Chk(QC.Mission.Cop.Dawn - 1), Chk(QC.Mission.Cop.Dawn, 2), Chk(QC.Mission.Cop.Dawn, 1),
                  Chk(QC.Mission.Cop.Dawn + 1), Chk(QC.Mission.Cop.Dawn + 100),
              }));

        // Prishe and Ulmia: past Dawn with Dawn's Status (5 / 2) or more
        string Dawn(string name, int mission, int status) => Avail(name, f =>
        {
            Cop(f, mission);
            f.MissionVars[$"{(int)QC.MissionLog.Cop}/{QC.Mission.Cop.Dawn}/Status"] = status;
        });
        Check("hidden g1:Prishe: past Dawn with Status 5 or more", "False|True|True|False|False",
              string.Join("|", Dawn("Prishe", QC.Mission.Cop.Dawn + 1, 4), Dawn("Prishe", QC.Mission.Cop.Dawn + 1, 5),
                          Dawn("Prishe", QC.Mission.Cop.Dawn + 1, 6), Dawn("Prishe", QC.Mission.Cop.Dawn, 5),
                          Dawn("Prishe", QC.Mission.Cop.Dawn - 1, 5)));
        Check("hidden g1:Ulmia: past Dawn with Status 2 or more", "False|True|True|False|False",
              string.Join("|", Dawn("Ulmia", QC.Mission.Cop.Dawn + 1, 1), Dawn("Ulmia", QC.Mission.Cop.Dawn + 1, 2),
                          Dawn("Ulmia", QC.Mission.Cop.Dawn + 1, 3), Dawn("Ulmia", QC.Mission.Cop.Dawn, 2),
                          Dawn("Ulmia", QC.Mission.Cop.Dawn - 1, 2)));

        // Shikaree Z: not from Flames in the Darkness to Fire in the Eyes of Men, not A Fate Decided
        string Sz(int? mission) => Avail("Shikaree Z", f =>
        {
            if (mission is int m) Cop(f, m);
        });
        Check("hidden g1:Shikaree Z: the current Cop mission", "True|True|False|False|False|True|True|False",
              string.Join("|", Sz(null), Sz(QC.Mission.Cop.FlamesInTheDarkness - 1),
                          Sz(QC.Mission.Cop.FlamesInTheDarkness),
                          Sz(QC.Mission.Cop.FlamesInTheDarkness + 1), Sz(QC.Mission.Cop.FireInTheEyesOfMen),
                          Sz(QC.Mission.Cop.FireInTheEyesOfMen + 1), Sz(QC.Mission.Cop.AFateDecided - 1),
                          Sz(QC.Mission.Cop.AFateDecided)));
    }
}
