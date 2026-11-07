using System;
using System.Collections.Generic;
using XI.Map;
using XI.Persistence;
using XI.Quests;
using XI.Script;

namespace XI.Zone
{
    // CLuaBaseEntity: the progression and inventory methods quest and NPC scripts reach -- position and level reads,
    // quest/mission/key item writers, rank, fame, titles, and the gil and item grants; a completed quest or mission
    // is a RoE event (ZoneHost.Roe.cs).
    // The status effect methods are in ZoneHost.StatusEffects.cs.
    public sealed partial class ZoneHost
    {
        // the stable quest keys (quest rework B1): xi.quest.id.<log name>.<CONST> for every CONST = id with a log name in
        // xi.quest.area and the id in range; empty without Lua (every key synthetic). A key or id taken twice keeps the first
        private QuestKeyRegistry BuildQuestKeyRegistry()
        {
            var reg = new QuestKeyRegistry();
            if (lua_ == null) return reg;
            int top = lua_.Top;
            try
            {
                lua_.GetPath("xi.quest.area");
                int areas = lua_.Top;
                lua_.GetPath("xi.quest.id");
                int ids = lua_.Top;
                if (!lua_.IsTable(areas) || !lua_.IsTable(ids)) return reg;
                lua_.PushNil();
                while (lua_.Next(areas))
                {
                    if (lua_.IsNumber(-2) && lua_.IsString(-1))
                    {
                        long area = lua_.ToIntegral(-2);
                        string areaName = lua_.ToStringValue(-1);
                        lua_.GetField(ids, areaName);
                        int t = lua_.Top;
                        if (lua_.IsTable(t) && area >= 0 && area < QuestBitBlob.Areas)
                        {
                            lua_.PushNil();
                            while (lua_.Next(t))
                            {
                                if (lua_.IsString(-2) && lua_.IsNumber(-1))
                                {
                                    string name = lua_.ToStringValue(-2);
                                    double v = lua_.ToNumber(-1);
                                    long id = (long)v;
                                    if (v == Math.Floor(v) && id >= 0 && id < QuestBitBlob.MaxQuestId &&
                                        !reg.Register(QuestKeyRegistry.KeyFor(areaName, name), new QuestWireRef((QuestLog)area, (ushort)id)))
                                        log_($"quests: {areaName}.{name} ({area}/{id}) not registered: its key or id is taken");
                                }
                                lua_.Pop(1);
                            }
                        }
                        lua_.Pop(1);
                    }
                    lua_.Pop(1);
                }
            }
            catch (LuaException e)
            {
                log_($"quests: xi.quest.id could not be read ({e.Message}); quest keys stay synthetic");
                return new QuestKeyRegistry();
            }
            finally
            {
                lua_.SetTop(top);
            }
            return reg;
        }

        private const int FameCap = 2500, RankPointsCap = 4000;
        private const int FameJeuno = 3, FameSelbinaRabao = 4, FameAbysseaFirst = 6, FameAbysseaLast = 14;
        private const int MissionLogZilart = 3, MissionLogToAU = 4, MissionLogWoTG = 5, MissionLogAssault = 7, MissionLogCampaign = 8, MissionLogRoV = 13;
        private const uint ItemFlagRare = 0x8000;
        private const ushort GilItemId = 0xFFFF;
        private const uint GilStackSize = 999999999;           // CItemCurrency's stack, whatever item_basic says
        private const ushort MsgStdItemEx = 220;               // "You cannot obtain the item. You already have one."

        // item_basic rows by itemid, read on first use: (stackSize, flags); null when the item does not exist
        private readonly Dictionary<ushort, (uint StackSize, uint Flags)?> itemBasic_ = new Dictionary<ushort, (uint, uint)?>();

        private void AddProgressBindings(Dictionary<string, LuaFunction> m)
        {
            // position and levels
            m["getXPos"] = l => Ret(l, Entity(l) is BattleEntity b ? b.X : ((NpcEntity)Entity(l)).X);
            m["getYPos"] = l => Ret(l, Entity(l) is BattleEntity b ? b.Y : ((NpcEntity)Entity(l)).Y);
            m["getZPos"] = l => Ret(l, Entity(l) is BattleEntity b ? b.Z : ((NpcEntity)Entity(l)).Z);
            m["getPreviousZone"] = l => Ret(l, Entity(l) is CharData c ? c.PrevZone : 0);
            m["checkDistance"] = CheckDistance;
            m["getMainLvl"] = l => Ret(l, Battle(l) is { } b ? b.MLvl : 0);
            m["getSubLvl"] = l => Ret(l, Entity(l) is CharData c ? c.SLvl : 0);
            m["getLevelCap"] = l => Ret(l, Pc(l, "getLevelCap") is CharData c ? c.Genkai : 0);
            m["hasSpell"] = l => Ret(l, Pc(l, "hasSpell") is CharData c && HasBit(c.SpellList, l.ToIntArg(2)));
            // inMogHouse(kind = xi.mogHouse.ANY): ANY 0, OWN 1, VISITING 2
            m["inMogHouse"] = l =>
            {
                if (Pc(l, "inMogHouse") is not CharData c) return Ret(l, false);
                int kind = l.IsNil(2) ? 0 : l.ToEnumArg(2); // sol::optional<xi::MogHouse>
                return Ret(l, kind switch
                {
                    1 => c.MoghouseId == c.Id,
                    2 => c.MoghouseId != 0 && c.MoghouseId != c.Id,
                    _ => c.MoghouseId != 0,
                });
            };
            m["getPartyLeader"] = l =>
            {
                if (Pc(l, "getPartyLeader") is CharData c && c.Party?.Leader is CharData leader) l.PushObject(leader);
                else l.PushNil();
                return 1;
            };

            m["needToZone"] = l =>
            {
                if (Pc(l, "needToZone") is not CharData c) return Ret(l, false);
                if (l.ToBoolean(2))
                {
                    SetCharVar(c, "[generic]mustZone", c.Zone, 0);
                    return Ret(l, true);
                }
                return Ret(l, CharVar(c, "[generic]mustZone") != 0);
            };

            // quests and missions
            m["addQuest"] = l => QuestWrite(l, "addQuest");
            m["delQuest"] = l => QuestWrite(l, "delQuest");
            m["delCurrentQuest"] = l => QuestWrite(l, "delCurrentQuest");
            m["completeQuest"] = l => QuestWrite(l, "completeQuest");
            m["addMission"] = AddMission;
            m["delMission"] = DelMission;
            m["completeMission"] = CompleteMission;
            m["setMissionStatus"] = SetMissionStatus;

            // key items
            m["addKeyItem"] = l => KeyItemWrite(l, true);
            m["delKeyItem"] = l => KeyItemWrite(l, false);

            // rank
            m["getRank"] = l =>
            {
                int nation = l.ToIntArg(2);
                return Ret(l, Pc(l, "getRank") is CharData c && nation >= 0 && nation < 3 ? c.Rank[nation] : 0);
            };
            m["setRank"] = l =>
            {
                if (Pc(l, "setRank") is CharData c && c.Nation < 3)
                {
                    c.Rank[c.Nation] = (byte)l.ToIntArg(2);
                    SaveMissionsList(c);
                }
                return 0;
            };
            m["getRankPoints"] = l => Ret(l, Pc(l, "getRankPoints") is CharData c ? c.RankPoints : 0);
            m["addRankPoints"] = l =>
            {
                if (Pc(l, "addRankPoints") is CharData c)
                {
                    c.RankPoints = (ushort)Math.Min(c.RankPoints + (ushort)l.ToIntArg(2), RankPointsCap);
                    SaveMissionsList(c);
                }
                return 0;
            };
            m["setRankPoints"] = l =>
            {
                if (Pc(l, "setRankPoints") is CharData c)
                {
                    c.RankPoints = (ushort)Math.Min((int)(ushort)l.ToIntArg(2), RankPointsCap);
                    SaveMissionsList(c);
                }
                return 0;
            };

            // fame
            m["getFame"] = l => Ret(l, Pc(l, "getFame") is CharData c ? Fame(c, l.ToEnumArg(2)) : 0);
            m["getFameLevel"] = GetFameLevel;
            m["addFame"] = l => FameWrite(l, "addFame", true);
            m["setFame"] = l => FameWrite(l, "setFame", false);

            // titles
            m["getTitle"] = l => Ret(l, Pc(l, "getTitle") is CharData c ? c.Title : 0);
            m["hasTitle"] = l => Ret(l, Pc(l, "hasTitle") is CharData c && HasBit(c.Titles, l.ToIntArg(2)));
            m["addTitle"] = l => SetTitle(l, "addTitle");
            m["setTitle"] = l => SetTitle(l, "setTitle");
            m["getCampaignAllegiance"] = l => Ret(l, Pc(l, "getCampaignAllegiance") is CharData c ? c.CampaignAllegiance : 0);

            // records of eminence (the rest are in ZoneHost.Roe)
            m["getEminenceCompleted"] = l =>
            {
                int record = (ushort)l.ToIntArg(2);
                return Ret(l, Pc(l, "getEminenceCompleted") is CharData c && record < 4096 &&
                              (c.EminenceLog[EminenceCompleteOffset + record / 8] & 1 << record % 8) != 0);
            };

            // inventory
            m["hasItem"] = l =>
            {
                if (Entity(l) is not CharData c) return Ret(l, false);
                ushort id = (ushort)l.ToIntArg(2);
                if (!l.IsNil(3) && l.Top >= 3)
                {
                    int loc = (byte)l.ToIntArg(3);
                    return Ret(l, SearchItem(c, (Container)(loc < CharData.MaxContainers ? loc : 0), id) != null);
                }
                return Ret(l, HasItem(c, id));
            };
            m["getFreeSlotsCount"] = l =>
            {
                if (Pc(l, "getFreeSlotsCount") is not CharData c) return Ret(l, 0);
                int loc = l.IsNumber(2) ? (byte)l.ToEnum(2) : 0; // as<CONTAINER_ID>
                return Ret(l, loc < CharData.MaxContainers ? FreeSlots(c, (Container)loc) : 0);
            };
            m["addGil"] = l =>
            {
                int gil = l.ToIntArg(2);
                if (Pc(l, "addGil") is not CharData c) return 0;
                if (Gil(c) == null)
                {
                    log_("lua: addGil : No Gil in currency slot");
                    return 0;
                }
                if (!AdjustGil(c, gil)) log_($"lua: could not adjust gil for {c.Name}");
                return 0;
            };
            m["addItem"] = AddItem;
        }

        // eminencelog_t: uint16 active[31], padded to 4, then uint32 progress[31], then the completion bits
        private const int EminenceProgressOffset = 64, EminenceCompleteOffset = 64 + 31 * 4;

        private static bool HasBit(byte[] bits, int bit) => bit >= 0 && bit / 8 < bits.Length && (bits[bit / 8] & 1 << bit % 8) != 0;

        private int CheckDistance(LuaState l)
        {
            // any entity: chars, mobs and npcs
            var self = PosOf(Entity(l));
            (float X, float Y, float Z) from = (self.X, self.Y, self.Z);
            (float X, float Y, float Z) to;
            if (l.ToObject(2) is BattleEntity or NpcEntity) { var o = PosOf(l.ToObject(2)); to = (o.X, o.Y, o.Z); }
            else if (l.IsTable(2)) to = ((float)FieldNumber(l, 2, "x", 0), (float)FieldNumber(l, 2, "y", 0), (float)FieldNumber(l, 2, "z", 0));
            else if (l.IsNumber(2) && l.IsNumber(3) && l.IsNumber(4)) to = ((float)l.ToNumber(2), (float)l.ToNumber(3), (float)l.ToNumber(4));
            else
            {
                log_("lua: checkDistance : invalid inputs.");
                return Ret(l, 0.0);
            }
            float dx = from.X - to.X, dy = from.Y - to.Y, dz = from.Z - to.Z;
            return Ret(l, MathF.Sqrt(dx * dx + dy * dy + dz * dz));
        }

        // ---- quests and missions ----------------------------------------------------------------------------------

        // addQuest / delQuest / delCurrentQuest / completeQuest: the bit flips, then SendPartialQuestLog and SaveQuestsList
        private int QuestWrite(LuaState l, string method)
        {
            int log = l.ToEnumArg(2), quest = (ushort)l.ToIntArg(3); // QuestLog, uint16
            if (Pc(l, method) is not CharData c) return 0;
            if (log < 0 || log >= MaxQuestArea || quest >= MaxQuestId)
            {
                log_($"lua: {method}: questLogID {log} or QuestID {quest} is invalid");
                return 0;
            }
            int bit = 1 << quest % 8, cur = log * CharData.QuestLogSize + quest / 8, done = cur + 32;
            bool current = (c.QuestLog[cur] & bit) != 0, complete = (c.QuestLog[done] & bit) != 0;
            switch (method)
            {
                case "addQuest":
                    if (current) return 0;
                    c.QuestLog[cur] |= (byte)bit;
                    PushTo(c, GameOkPackets.QuestOffer(c, log));
                    break;
                case "delQuest":
                    if (!current && !complete) return 0;
                    c.QuestLog[cur] &= (byte)~bit;
                    c.QuestLog[done] &= (byte)~bit;
                    PushTo(c, GameOkPackets.QuestOffer(c, log));
                    PushTo(c, GameOkPackets.QuestComplete(c, log));
                    break;
                case "delCurrentQuest":
                    if (!current) return 0;
                    c.QuestLog[cur] &= (byte)~bit;
                    PushTo(c, GameOkPackets.QuestOffer(c, log));
                    PushTo(c, GameOkPackets.QuestComplete(c, log));
                    break;
                default:
                    if (complete) return 0;
                    c.QuestLog[cur] &= (byte)~bit;
                    c.QuestLog[done] |= (byte)bit;
                    PushTo(c, GameOkPackets.QuestOffer(c, log));
                    PushTo(c, GameOkPackets.QuestComplete(c, log));
                    break;
            }
            SaveQuestsList(c);
            if (method == "completeQuest") RoeEvent(RoeEventQuestComplete, c, NoDatagrams);
            return 0;
        }

        // the "no mission" value: 0xFFFF for the nation logs, 0 for the rest
        private static int MissionNone(int log) => log <= 2 ? 0xFFFF : 0;

        private bool MissionArgs(LuaState l, string method, out CharData c, out int log, out int mission)
        {
            log = l.ToEnumArg(2); // MissionLog
            mission = (ushort)l.ToIntArg(3);
            c = Pc(l, method);
            if (c == null) return false;
            if (log < 0 || log >= MaxMissionArea || mission >= MaxMissionId)
            {
                log_($"lua: {method}: missionLogID {log} or Mission {mission} is invalid");
                return false;
            }
            return true;
        }

        private int AddMission(LuaState l)
        {
            if (!MissionArgs(l, "addMission", out var c, out int log, out int mission)) return 0;
            if (c.MissionCurrent(log) != MissionNone(log))
                log_($"lua: addMission: overwriting current mission {c.MissionCurrent(log)} of log {log} with {mission}");
            c.SetMissionCurrent(log, mission);
            SendPartialMissionLog(c, log, false);
            SaveMissionsList(c);
            return 0;
        }

        private int DelMission(LuaState l)
        {
            if (!MissionArgs(l, "delMission", out var c, out int log, out int mission)) return 0;
            bool complete = log != MissionLogCoP && mission < 64 && c.MissionComplete(log, mission);
            if (c.MissionCurrent(log) == mission)
            {
                c.SetMissionCurrent(log, MissionNone(log));
                SendPartialMissionLog(c, log, false);
            }
            if (complete)
            {
                c.SetMissionComplete(log, mission, false);
                SendPartialMissionLog(c, log, true);
            }
            SaveMissionsList(c);
            return 0;
        }

        private int CompleteMission(LuaState l)
        {
            if (!MissionArgs(l, "completeMission", out var c, out int log, out int mission)) return 0;
            if (c.MissionCurrent(log) != mission)
            {
                log_($"lua: completeMission: {mission} on log {log} is not the current mission");
                return 0;
            }
            c.SetMissionCurrent(log, MissionNone(log));
            if (log != MissionLogCoP && mission < 64)
            {
                c.SetMissionComplete(log, mission, true);
                SendPartialMissionLog(c, log, true);
            }
            SendPartialMissionLog(c, log, false);
            SaveMissionsList(c);
            RoeEvent(RoeEventMissionComplete, c, NoDatagrams);
            return 0;
        }

        // setMissionStatus(log, value[, pos]): a nibble at pos (pushing the mission log), or the whole status word
        private int SetMissionStatus(LuaState l)
        {
            int log = l.ToEnumArg(2); // MissionLog
            uint value = l.ToUIntArg(3);
            if (Pc(l, "setMissionStatus") is not CharData c) return 0;
            if (log < 0 || log >= MaxMissionArea)
            {
                log_($"lua: setMissionStatus: missionLogID {log} is invalid");
                return 0;
            }
            if (l.IsNumber(4))
            {
                int pos = (byte)l.ToIntegral(4);
                if (pos > 7 || value > 0xF)
                {
                    log_($"lua: setMissionStatus: position {pos} or value {value} is invalid");
                    return 0;
                }
                uint status = (uint)c.MissionStatusUpper(log) << 16 | c.MissionStatusLower(log);
                status = status & ~(0xFu << 4 * pos) | value << 4 * pos;
                c.SetMissionStatus(log, status);
                SendPartialMissionLog(c, log, false);
            }
            else
            {
                c.SetMissionStatus(log, value);
            }
            SaveMissionsList(c);
            return 0;
        }

        // charutils::SendPartialMissionLog
        private void SendPartialMissionLog(CharData c, int log, bool completed)
        {
            const int QuestLogAhtUrghan = 6;
            if (log <= MissionLogZilart)
                PushTo(c, completed ? GameOkPackets.NationsComplete(c) : GameOkPackets.Mission(c, UnixNow()));
            else if (log == MissionLogToAU || log == MissionLogWoTG)
                PushTo(c, completed ? GameOkPackets.ToAUWoTGComplete(c) : GameOkPackets.QuestOffer(c, QuestLogAhtUrghan));
            else if (log == MissionLogAssault)
                PushTo(c, completed ? GameOkPackets.QuestComplete(c, QuestLogAhtUrghan) : GameOkPackets.QuestOffer(c, QuestLogAhtUrghan));
            else if (log == MissionLogCampaign)
            {
                if (completed)
                {
                    PushTo(c, GameOkPackets.CampaignComplete(c, 0));
                    PushTo(c, GameOkPackets.CampaignComplete(c, 1));
                }
                else
                {
                    PushTo(c, GameOkPackets.QuestOffer(c, QuestLogAhtUrghan));
                }
            }
            else if (log <= MissionLogRoV) // CoP and ACP..RoV keep both in the MISSION packet
                PushTo(c, GameOkPackets.Mission(c, UnixNow()));
        }

        // charutils::SaveMissionsList, with the char's mission, assault and campaign rows
        private void SaveMissionsList(CharData c) =>
            PersistProgress(c, () => Exec("UPDATE chars LEFT JOIN char_profile USING(charid) SET missions = ?, assault = ?, campaign = ?, rank_points = ?, " +
                                          "rank_sandoria = ?, rank_bastok = ?, rank_windurst = ? WHERE charid = ? LIMIT 1",
                                          BoundValue.Of(c.MissionLog), BoundValue.Of(c.AssaultLog), BoundValue.Of(c.CampaignLog), BoundValue.Of(c.RankPoints),
                                          BoundValue.Of(c.Rank[0]), BoundValue.Of(c.Rank[1]), BoundValue.Of(c.Rank[2]), BoundValue.Of(c.Id)) != null,
                            p => p.SyncMissionsList(c.MissionLog, c.AssaultLog, c.CampaignLog));

        // charutils::SaveQuestsList, with the char's quest rows
        private void SaveQuestsList(CharData c) =>
            PersistProgress(c, () => Exec("UPDATE chars SET quests = ? WHERE charid = ? LIMIT 1", BoundValue.Of(c.QuestLog), BoundValue.Of(c.Id)) != null,
                            p => p.SyncQuests(c.QuestLog));

        // a legacy write and the char's rows it changes, in one transaction (ProgressRows.Persist); the
        // rows are computed after the legacy write, so an increment's rows see its result
        private void PersistProgress(CharData c, Func<bool> legacy, Func<XI.Quests.CharProgress, XI.Quests.ProgressChanges> rows)
        {
            if (loader_?.Progress is { } progress) progress.Persist(c, legacy, rows);
            else legacy();
        }

        // ---- key items --------------------------------------------------------------------------------------------

        // addKeyItem / delKeyItem: the keyList bit, GP_SERV_COMMAND_SCENARIOITEM for its table (and the mount list for
        // table 6, the mounts), then SaveKeyItems
        private int KeyItemWrite(LuaState l, bool add)
        {
            int keyItem = (ushort)l.ToEnumArg(2); // xi::KeyItem
            if (Pc(l, add ? "addKeyItem" : "delKeyItem") is not CharData c) return 0;
            int table = keyItem >> 9, index = keyItem % 512;
            if (table >= 8)
            {
                log_($"lua: {(add ? "addKeyItem" : "delKeyItem")}: Index {table} exceeds key items table capacity.");
                return 0;
            }
            int off = table * 128 + index / 8;
            if (add) c.KeyItems[off] |= (byte)(1 << index % 8);
            else c.KeyItems[off] &= (byte)~(1 << index % 8);
            PushTo(c, GameOkPackets.ScenarioItem(c, table));
            if (add && table == 6) PushTo(c, GameOkPackets.MountData(c));
            Exec("UPDATE chars SET keyitems = ? WHERE charid = ? LIMIT 1", BoundValue.Of(c.KeyItems), BoundValue.Of(c.Id));
            return 0;
        }

        // ---- fame and titles --------------------------------------------------------------------------------------

        // getFame: Jeuno and Selbina/Rabao derive from the nations' fame
        private int Fame(CharData c, int area)
        {
            int points;
            if (area == FameJeuno) points = (c.Fame[0] + c.Fame[1] + c.Fame[2]) / 2;
            else if (area == FameSelbinaRabao) points = (c.Fame[0] + c.Fame[1]) * 2 / 3;
            else if (area >= 0 && area < c.Fame.Length) points = c.Fame[area];
            else
            {
                log_($"lua: Invalid fame area ({area}).");
                points = 0;
            }
            return Math.Min(points, FameCap);
        }

        // getFameLevel: the rank from xi.data.fame.getRankFromPoints; 1 on a Lua error, Abyssea capped at 6
        private int GetFameLevel(LuaState l)
        {
            int area = l.ToEnumArg(2); // xi::FameArea
            if (Pc(l, "getFameLevel") is not CharData c) return Ret(l, 0);
            int points = Fame(c, area);
            int level = 0;
            int top = l.Top;
            try
            {
                l.GetPath("xi.data.fame.getRankFromPoints");
                l.Push((double)points);
                l.Call(1, 1);
                if (l.IsNumber(-1)) level = (byte)l.ToIntegral(-1);
            }
            catch (LuaException e)
            {
                log_("lua: getFameLevel: " + e.Message);
            }
            finally
            {
                l.SetTop(top);
            }
            if (level == 0) level = 1;
            if (area >= FameAbysseaFirst && area <= FameAbysseaLast) level = Math.Min(level, 6);
            return Ret(l, level);
        }

        // addFame / setFame: capped at 2500, then SaveFame; the derived areas cannot be written
        private int FameWrite(LuaState l, string method, bool add)
        {
            int area = l.ToEnumArg(2), fame = (ushort)l.ToIntArg(3); // xi::FameArea, uint16
            if (Pc(l, method) is not CharData c) return 0;
            if (area == FameJeuno || area == FameSelbinaRabao)
            {
                log_($"lua: Fame area ({area}) is derived from nation fame and cannot be awarded directly.");
                return 0;
            }
            if (area < 0 || area >= c.Fame.Length)
            {
                log_($"lua: Invalid fame area ({area}).");
                return 0;
            }
            c.Fame[area] = (ushort)Math.Min((int)(ushort)((add ? c.Fame[area] : 0) + fame), FameCap);
            var args = new List<BoundValue>();
            for (int a = 0; a < c.Fame.Length; a++)
                if (CharLoader.FameColumns[a] != null) args.Add(BoundValue.Of(c.Fame[a]));
            args.Add(BoundValue.Of(c.Id));
            Exec("UPDATE char_profile SET " + string.Join(", ", Array.ConvertAll(CharLoader.StoredFameColumns, col => col + " = ?")) +
                 " WHERE charid = ?", args.ToArray());
            return 0;
        }

        // addTitle / setTitle (charutils::setTitle): the current title, the 0x061 CLISTATUS, its bit, then SaveTitles
        private int SetTitle(LuaState l, string method)
        {
            int title = (ushort)l.ToIntArg(2);
            if (Pc(l, method) is not CharData c) return 0;
            c.Title = (ushort)title;
            if (loader_.Stats != null) PushTo(c, SelfPackets.CliStatus(c, loader_.Stats, UnityStatus(c)));
            if (title / 8 < c.Titles.Length) c.Titles[title / 8] |= (byte)(1 << title % 8);
            Exec("UPDATE chars LEFT JOIN char_stats USING(charid) SET titles = ?, title = ? WHERE charid = ? LIMIT 1",
                 BoundValue.Of(c.Titles), BoundValue.Of(c.Title), BoundValue.Of(c.Id));
            return 0;
        }

        // ---- inventory --------------------------------------------------------------------------------------------

        // CItemContainer::SearchItem: slots 0..size
        private static Item SearchItem(CharData c, Container loc, ushort id) =>
            c.Items.Find(i => i.Location == loc && i.ItemId == id && i.Slot <= c.StorageSize[(int)loc]);

        // charutils::HasItem: every container, the recycle bin included
        private static bool HasItem(CharData c, ushort id) =>
            id != 0 && c.Items.Exists(i => i.ItemId == id && i.Slot <= c.StorageSize[(int)i.Location]);

        // CItemContainer::GetFreeSlotsCount: size minus the items in slots 1..size (slot 0 holds gil), less the slots an
        // open synth holds for its result
        private static int FreeSlots(CharData c, Container loc)
        {
            int size = c.StorageSize[(int)loc];
            int used = c.Items.FindAll(i => i.Location == loc && i.Slot >= 1 && i.Slot <= size).Count;
            return Math.Max(size - used - (c.Synth?.HeldSlots(loc) ?? 0), 0);
        }

        private (uint StackSize, uint Flags)? ItemBasic(ushort id)
        {
            if (itemBasic_.TryGetValue(id, out var row)) return row;
            var r = Exec("SELECT stackSize, flags FROM item_basic WHERE itemid = ? LIMIT 1", BoundValue.Of(id));
            row = r != null && r.Next() ? (r.Get<uint>("stackSize"), r.Get<uint>("flags")) : null;
            if (row != null && id == GilItemId) row = (GilStackSize, row.Value.Flags);
            itemBasic_[id] = row;
            return row;
        }

        // adjustGil: the slot 0 quantity, refused when it would leave [0, the currency stack]
        private bool AdjustGil(CharData c, int delta)
        {
            var gil = Gil(c);
            long quantity = (long)gil.Quantity + delta;
            if (quantity < 0 || quantity > GilStackSize) return false;
            Exec("UPDATE char_inventory SET quantity = ? WHERE charid = ? AND location = ? AND slot = ?",
                 BoundValue.Of((uint)quantity), BoundValue.Of(c.Id), BoundValue.Of((byte)Container.Inventory), BoundValue.Of((byte)0));
            gil.Quantity = (uint)quantity;
            PushTo(c, EventPackets.ItemNum(Container.Inventory, 0, gil.Quantity));
            return true;
        }

        // 0x009 GP_SERV_COMMAND_MESSAGE (PChar, param0, param1, MsgStd): the char's id and index and the message
        private static BasicPacket StdMessage(CharData c, ushort message)
        {
            var w = new PacketWriter(0x009, 0x24);
            w.U32(4, c.Id);
            w.U16(8, c.TargId);
            w.U16(10, message);
            return w.Packet;
        }

        // addItem({id, quantity, silent, ...}) or addItem(id[, quantity | silent[, silent]]): stacks of at most the
        // item's stack size while the inventory has room, each signed (setSignature) with a non-empty `signature`, given
        // the `appraisal` origin (setAppraisalID) and the `exdata` table (Exdata::fromTable, else raw byte indices).
        // Returns true for the last stack added (the C++ returns the CItem), else nil.
        private int AddItem(LuaState l)
        {
            if (Pc(l, "addItem") is not CharData c)
            {
                l.PushNil();
                return 1;
            }
            ushort id;
            int quantity = 1;
            bool silent = false;
            string signature = "";
            byte? appraisal = null;
            Dictionary<object, object> exdata = null;
            if (l.IsTable(2))
            {
                l.GetField(2, "id");
                bool hasId = !l.IsNil(-1);
                l.Pop(1);
                if (!hasId)
                {
                    log_("lua: AddItem: id is nil");
                    l.PushNil();
                    return 1;
                }
                id = (ushort)FieldInt(l, 2, "id", 0);
                quantity = (int)FieldInt(l, 2, "quantity", 1);
                silent = FieldBool(l, 2, "silent");
                signature = FieldString(l, 2, "signature") ?? "";
                l.GetField(2, "appraisal");
                if (l.IsNumber(-1)) appraisal = (byte)l.ToIntegral(-1);
                l.Pop(1);
                l.GetField(2, "exdata");
                if (l.IsTable(-1)) exdata = ItemExdata.FromLua(l, -1);
                l.Pop(1);
            }
            else
            {
                id = (ushort)l.ToIntArg(2);
                if (l.Type(3) == 1) silent = l.ToBoolean(3);
                else if (l.IsNumber(3)) quantity = (int)l.ToIntegral(3);
                if (l.Type(4) == 1) silent = l.ToBoolean(4);
            }

            // CLuaBaseEntity::addItem returns the CItem* of the last stack given (nil when none)
            Item added = null;
            while (FreeSlots(c, Container.Inventory) != 0 && quantity > 0)
            {
                var basic = ItemBasic(id);
                if (basic == null)
                {
                    log_($"lua: AddItem: Item <{id}> is not found in a database");
                    break;
                }
                uint stack = Math.Max(basic.Value.StackSize, 1);
                uint q = Math.Min((uint)quantity, stack);
                quantity -= (int)stack;
                void Setup(Item item)
                {
                    // setAppraisalID: Exdata::Appraisable's AppraisalId
                    if (appraisal is byte origin) item.Extra[22] = origin;
                    if (exdata != null) WriteExdata(item, exdata, "AddItem");
                }
                int slot = ApplyAddItem(c, Container.Inventory, id, q, silent, null, out _, signature, Setup);
                if (slot < 0) break;
                added = c.FindItem(Container.Inventory, (byte)slot);
            }
            if (added != null) l.PushObject(added, ItemType);
            else l.PushNil();
            return 1;
        }

        // Exdata::fromTable, or for an item with no typed exdata the table's keys as byte indices into m_extra
        private void WriteExdata(Item item, Dictionary<object, object> exdata, string caller)
        {
            if (ItemExdata.FromTable(item, exdata, loader_.Stats?.Items, id => Fishing.Fish.ContainsKey(id))) return;
            foreach (var (key, value) in exdata)
            {
                byte index = ItemExdata.RawByte(key);
                if (index < item.Extra.Length) item.Extra[index] = ItemExdata.RawByte(value);
                else log_($"{caller}: Trying to write to invalid exdata index: <{index}>");
            }
            item.Signature = ItemSignature.Get(item);
        }
    }
}
