# Stabilization & Architecture Tasks

## Immediate Focus — Tick Runtime
1. Replace `Tick` with a `readonly struct Ticks(ulong Milliseconds)` sourced from a singleton `GlobalClock`. Clock exposes `Now`, `AdvanceBy(TimeSpan)`, and `Subscribe(Action<Ticks> onTick, Action<Ticks> onTickEnd)`.
2. Build a tick actor system: worlds, channels, and maps register mailboxes that the clock drives sequentially each tick. Each actor owns its queue and runs on a single thread to guarantee deterministic updates.
3. Move existing polling in `World/Sessions/RoomServer.cs` onto this scheduler. `Program.cs` wires the clock and starts the world actor before listeners accept sockets.

## World / Channel / Map Executors
1. Define contexts: `WorldContext` owns channels, `ChannelExecutor` owns rooms, `RoomExecutor` owns room objects (players, mobs, drops).
2. All mutations run through executor messages; no shared mutable state across maps. Sessions hop maps by posting `JoinMap`/`LeaveMap` commands that execute during the map tick tail (`onTickEnd`).

## Session Flow & Mailboxes
1. Extend `Session<T>` (`World/Sessions/RoomServer.cs:61`) with references to world/channel/room contexts so gameplay can traverse upward (party, buffs, etc.).
2. Split network IO from gameplay: incoming packets go into a bounded `SessionMailbox`. Map executor consumes the mailbox during its tick and turns messages into gameplay events.

## Timers, Delay Queues & Events
1. Add a `DelayQueue<T>` per map plus optional per-object queues keyed by `Ticks`. Expire entries at the start of the tick, enqueue follow-up events, and allow rescheduling.
2. Standardize event types (`GiveExperience`, `ApplyBuff`, `SpawnLoot`, `CloseSession`). Replace chained method calls (kill→exp→level) with queued events processed in-order.

## Backpressure Strategy
1. Keep socket channels bounded; when outbound queue fills, mark the session `SlowConsumer` and enqueue a `SessionOverloaded` event. Decide at `onTickEnd` whether to drop packets, emit warnings, or kick.
2. For broadcasts, send once per tick and fan out through executor messages. Skip or batch delivery for slow sessions; raise metrics so operators can see churn.

## Persistence & IO Isolation
1. Add `Data/FliegenPilzContext` (EF Core, SQLite dev / PostgreSQL prod). Use explicit structs like `struct CharacterId(int Value)` and mirror for maps, items, etc.
2. Create a single `DatabaseActor` per world. Maps enqueue commands (`LoadCharacter`, `SaveSession`, `PersistDrop`) and await completion via tasks; actors serialize DB access without blocking the tick.

## Strong Typing & Packets
1. Wrap identifiers (`MapId`, `ItemId`, `SkillId`) in small structs, update packet builders/readers to accept them, and enforce conversions in one place.
2. Add round-trip tests for `Proto/*.cs` messages to guard encode/decode efficiency and prepare for future source generation.

## Testing & Diagnostics
1. Create a `TickTestHost` that simulates the clock and actors for integration tests (buff expiry, delayed events, session transfers).
2. Add stress tests for backpressure and timer overflow. Instrument executors with structured logs plus counters for queue depth, tick latency, dropped packets, and slow sessions.
