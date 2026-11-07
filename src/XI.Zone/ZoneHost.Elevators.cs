using System;
using System.Collections.Generic;
using XI.Common;
using XI.Map;

namespace XI.Zone
{
    // npcs.yaml's `elevator:` on a platform (convertElevator): its two doors, the lever that drives it (timed_automatic
    // runs itself off its period), whether its shaft plays the up animation to go down, the travel seconds, the period
    // and the door delay in milliseconds
    public sealed class ElevatorData
    {
        public string LowerDoor = "", UpperDoor = "";
        public byte Lever;
        public bool Reversed;
        public byte Travel;
        public uint Period, DoorDelay;

        public static ElevatorData Load(Dictionary<string, object> source, uint id, DataEnums enums)
        {
            if (source == null) return null;
            var lift = new ElevatorData
            {
                LowerDoor = source.Str("lower_door") ?? "",
                UpperDoor = source.Str("upper_door") ?? "",
                Lever = (byte)enums.Resolve("elevator", source.Str("lever") ?? "timed_automatic"),
                Reversed = source.Str("reversed") == "true",
                Travel = (byte)U32(source.Str("travel")),
                Period = U32(source.Str("period")),
                DoorDelay = U32(source.Str("door_delay")),
            };
            // a timed lift cycles off its period, so one without it has nothing to run on
            if (lift.Lever == ZoneHost.ElevatorTimedAutomatic && lift.Period == 0)
                throw new FormatException($"elevator {id} runs on a timer but states no period");
            return lift;
        }

        private static uint U32(string v) => v == null ? 0 : uint.Parse(v, System.Globalization.CultureInfo.InvariantCulture);
    }

    // transports/elevator.cpp and elevator_handler.cpp: ElevatorHandler::addElevator once a zone's NPCs are in (the
    // doors named, the platform and doors always relevant, TransportTravel set, a timed lift's first leg lined up with
    // the clock, the floor read off the platform's animation, the doors put right), tick (a timed lift shuts its
    // standing door doorDelay before it leaves; a leg starts once its period is up, playing up or down and stamping
    // TransportTimestamp; it arrives travel seconds on, opening the door there; a lever lift then waits again),
    // startElevator (a pulled lever shuts the door and starts doorDelay on) and elevatorState, on the Vana'diel
    // millisecond clock; luautils' RunElevator and GetElevatorState.
    public sealed partial class ZoneHost
    {
        internal const byte ElevatorTimedAutomatic = 0;
        private const byte ElevatorBottom = 0, ElevatorTop = 1, ElevatorAscend = 2, ElevatorDescend = 3;
        private const byte AnimationElevatorUp = 10, AnimationElevatorDown = 11;

        // the earth clock in milliseconds, for the lifts that run on fractions of a second
        public Func<long> UnixNowMs = EarthTime.UnixMs;

        private sealed class Elevator
        {
            public byte Id, State;
            public ushort Zone;
            public ulong LegStarted;
            public uint Period, MoveTime, DoorDelay;
            public NpcEntity Platform, LowerDoor, UpperDoor;
            public bool Activated, IsPermanent, AnimationsReversed;
        }

        private readonly List<Elevator> elevators_ = new List<Elevator>();

        // earth_time::vanadiel_timestamp_ms
        private ulong VanadielTimestampMs() => (ulong)(UnixNowMs() - VanaTime.EpochUnixSec * 1000);

        // zoneutils::InsertNPCs' tail: the lifts, once the zone is whole
        private void AddElevators(ZoneInfo info)
        {
            foreach (var npc in info.Npcs)
                if (npc.Elevator != null) AddElevator(info, npc, npc.Elevator);
        }

        private void AddElevator(ZoneInfo zone, NpcEntity platform, ElevatorData lift)
        {
            var lower = zone.Npcs.Find(n => n.Name == lift.LowerDoor);
            var upper = zone.Npcs.Find(n => n.Name == lift.UpperDoor);
            if (lower == null || upper == null)
                throw new InvalidOperationException($"elevator {platform.Name} in {zone.Name} names doors '{lift.LowerDoor}' and '{lift.UpperDoor}', which the zone does not both hold");
            if (elevators_.Exists(e => e.Zone == zone.Id && e.Platform.Name == platform.Name))
                throw new InvalidOperationException($"elevator {platform.Name} in {zone.Name} is declared twice");

            var elevator = new Elevator
            {
                Id = lift.Lever,
                Zone = zone.Id,
                Period = lift.Period,
                MoveTime = lift.Travel,
                DoorDelay = lift.DoorDelay,
                Platform = platform,
                LowerDoor = lower,
                UpperDoor = upper,
                Activated = lift.Lever == ElevatorTimedAutomatic,
                IsPermanent = lift.Lever == ElevatorTimedAutomatic,
                AnimationsReversed = lift.Reversed,
            };
            platform.AlwaysRelevant = true;
            platform.LocalVars["TransportTravel"] = elevator.MoveTime;
            lower.AlwaysRelevant = true;
            upper.AlwaysRelevant = true;

            // a timed lift lines its first leg up with the clock, a lever one waits to be pulled
            ulong now = VanadielTimestampMs();
            elevator.LegStarted = elevator.Period != 0 ? now - now % elevator.Period + elevator.Period : now;

            // whichever way the data has the platform facing is the floor it starts on
            if (platform.Animation == AnimationElevatorDown) elevator.State = ElevatorBottom;
            else if (platform.Animation == AnimationElevatorUp) elevator.State = ElevatorTop;
            else throw new InvalidOperationException($"elevator {platform.Name} rests on animation {platform.Animation}, which is neither of the two a platform can hold");
            if (elevator.AnimationsReversed) elevator.State = elevator.State == ElevatorTop ? ElevatorBottom : ElevatorTop;

            // put the doors right whatever the data says
            bool atTop = elevator.State == ElevatorTop;
            lower.Animation = atTop ? AnimationCloseDoor : AnimationOpenDoor;
            upper.Animation = atTop ? AnimationOpenDoor : AnimationCloseDoor;
            elevators_.Add(elevator);
        }

        // UpdateEntityPacket(door, ENTITY_SPAWN, UPDATE_ALL_MOB, true): every char in the zone
        private void ElevatorOpenDoor(Elevator e, NpcEntity door)
        {
            door.Animation = AnimationOpenDoor;
            BroadcastTransportUpdate(e.Zone, door, EntityUpdatePacket.UpdateAllMob, EntityUpdate.Spawn);
        }

        private void ElevatorCloseDoor(Elevator e, NpcEntity door)
        {
            if (door.Animation == AnimationCloseDoor) return;
            door.Animation = AnimationCloseDoor;
            BroadcastTransportUpdate(e.Zone, door, EntityUpdatePacket.UpdateAllMob, EntityUpdate.Spawn);
        }

        private void ElevatorCloseStandingDoor(Elevator e)
        {
            if (e.State == ElevatorTop) ElevatorCloseDoor(e, e.UpperDoor);
            else if (e.State == ElevatorBottom) ElevatorCloseDoor(e, e.LowerDoor);
        }

        // start: off the floor it stands at; a timed lift follows the clock, a lever one runs from now
        private void ElevatorStart(Elevator e, ulong now)
        {
            switch (e.State)
            {
                case ElevatorTop:
                    e.State = ElevatorDescend;
                    e.Platform.Animation = e.AnimationsReversed ? AnimationElevatorUp : AnimationElevatorDown;
                    break;
                case ElevatorBottom:
                    e.State = ElevatorAscend;
                    e.Platform.Animation = e.AnimationsReversed ? AnimationElevatorDown : AnimationElevatorUp;
                    break;
                default:
                    return;
            }
            if (e.IsPermanent) e.LegStarted = now - now % e.Period;
            else
            {
                e.LegStarted = now;
                e.Activated = true;
            }
            // the wire field is whole seconds
            e.Platform.LocalVars["TransportTimestamp"] = (uint)(e.LegStarted / 1000);
            BroadcastTransportUpdate(e.Zone, e.Platform, EntityUpdatePacket.UpdateCombat);
        }

        private void ElevatorArrive(Elevator e)
        {
            if (!e.IsPermanent) e.Activated = false;
            e.State = e.State == ElevatorAscend ? ElevatorTop : ElevatorBottom;
            ElevatorOpenDoor(e, e.State == ElevatorBottom ? e.LowerDoor : e.UpperDoor);
        }

        // ElevatorHandler::tick
        private void TickElevators()
        {
            ulong now = VanadielTimestampMs();
            foreach (var e in elevators_)
            {
                if (!e.Activated) continue;
                switch (e.State)
                {
                    case ElevatorTop:
                    case ElevatorBottom:
                        // the leg is timed end to end, so the wait at the floor is whatever travel leaves over
                        if (e.IsPermanent && now + e.DoorDelay >= e.LegStarted + e.Period) ElevatorCloseStandingDoor(e);
                        if (now >= e.LegStarted + e.Period) ElevatorStart(e, now);
                        break;
                    case ElevatorAscend:
                    case ElevatorDescend:
                        if (now >= e.LegStarted + e.MoveTime * 1000UL) ElevatorArrive(e);
                        break;
                    default:
                        log_($"Unexpected state reached for elevator {e.Platform.Id}");
                        break;
                }
            }
        }

        // startElevator: a pulled lever shuts the standing door and starts doorDelay on, unless already mid-leg
        private void StartElevator(byte id)
        {
            var e = elevators_.Find(x => x.Id == id);
            if (e == null || e.Activated) return;
            ElevatorCloseStandingDoor(e);
            e.LegStarted = VanadielTimestampMs() + e.DoorDelay;
            e.Activated = true;
        }

        // elevatorState as luautils::GetElevatorState hands it to Lua: xi.elevatorState, or -1 without such a lift
        private int ElevatorState(byte id) => elevators_.Find(x => x.Id == id) is { } e ? e.State : -1;
    }
}
