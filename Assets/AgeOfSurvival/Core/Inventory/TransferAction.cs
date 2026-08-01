using System;
using AgeOfSurvival.Core.Characters;

namespace AgeOfSurvival.Core.Inventory
{
    public readonly struct TransferActionId : IEquatable<TransferActionId>
    {
        public TransferActionId(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Transfer action id is required.", nameof(value));
            Value = value;
        }
        public string Value { get; }
        public bool IsValid => !string.IsNullOrWhiteSpace(Value);
        public bool Equals(TransferActionId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is TransferActionId other && Equals(other);
        public override int GetHashCode() => Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
        public override string ToString() => Value ?? string.Empty;
    }

    public enum TransferActionStatus { Invalid = 0, Active = 1, Completed = 2, Interrupted = 3, Failed = 4 }
    public enum TransferActionReason { None = 0, InvalidRequest = 1, SourceInsufficient = 2, DestinationFull = 3, PlayerMoved = 4, OutOfRange = 5, AlreadyFinalized = 6, AnotherActionActive = 7 }

    public sealed class TransferTimingDefinition
    {
        public TransferTimingDefinition(int baseTicks, int ticksPerEncumbranceUnit, int encumbranceUnitsPerUnit, int minimumTicks)
        {
            if (baseTicks < 0 || ticksPerEncumbranceUnit < 0 || encumbranceUnitsPerUnit <= 0 || minimumTicks <= 0)
                throw new ArgumentOutOfRangeException(nameof(baseTicks), "Timing values must be non-negative and divisors positive.");
            BaseTicks = baseTicks;
            TicksPerEncumbranceUnit = ticksPerEncumbranceUnit;
            EncumbranceUnitsPerUnit = encumbranceUnitsPerUnit;
            MinimumTicks = minimumTicks;
        }
        public int BaseTicks { get; }
        public int TicksPerEncumbranceUnit { get; }
        public int EncumbranceUnitsPerUnit { get; }
        public int MinimumTicks { get; }
        public int DurationTicks(EncumbranceValue encumbrance)
        {
            long scaled = checked(encumbrance.Units * TicksPerEncumbranceUnit);
            long variable = (scaled + EncumbranceUnitsPerUnit - 1) / EncumbranceUnitsPerUnit;
            return checked((int)Math.Max(MinimumTicks, BaseTicks + variable));
        }
    }

    public sealed class TransferActionState
    {
        internal TransferActionState(TransferActionId id, ContainerId sourceId, ContainerId destinationId,
            ItemDefinitionId definitionId, int requestedQuantity, int plannedQuantity, long startTick,
            int durationTicks, WorldPosition sourcePosition, double maximumDistance)
        {
            Id = id; SourceId = sourceId; DestinationId = destinationId; DefinitionId = definitionId;
            RequestedQuantity = requestedQuantity; PlannedQuantity = plannedQuantity; StartTick = startTick;
            DurationTicks = durationTicks; SourcePosition = sourcePosition; MaximumDistance = maximumDistance;
            Status = TransferActionStatus.Active;
        }
        public TransferActionId Id { get; }
        public ContainerId SourceId { get; }
        public ContainerId DestinationId { get; }
        public ItemDefinitionId DefinitionId { get; }
        public int RequestedQuantity { get; }
        public int PlannedQuantity { get; }
        public int TransferredQuantity { get; internal set; }
        public long StartTick { get; }
        public int DurationTicks { get; }
        public WorldPosition SourcePosition { get; }
        public double MaximumDistance { get; }
        public TransferActionStatus Status { get; internal set; }
        public TransferActionReason Reason { get; internal set; }
        public long? FinalizedTick { get; internal set; }
        public int RemainingQuantity => RequestedQuantity - TransferredQuantity;
        public double ProgressAt(long tick)
        {
            if (Status == TransferActionStatus.Completed) return 1.0;
            long effectiveTick = FinalizedTick.HasValue && tick > FinalizedTick.Value
                ? FinalizedTick.Value
                : tick;
            if (effectiveTick <= StartTick) return 0.0;
            return Math.Min(1.0, (double)(effectiveTick - StartTick) / DurationTicks);
        }
    }

    public readonly struct TransferActionResult
    {
        public TransferActionResult(TransferActionState action, TransferActionReason reason)
        { Action = action; Reason = reason; }
        public TransferActionState Action { get; }
        public TransferActionReason Reason { get; }
        public bool Succeeded => Action != null && Reason == TransferActionReason.None;
    }

    public static class TransferActionOperations
    {
        public static TransferActionResult Start(TransferActionId id, ContainerState source, ContainerState destination,
            ItemDefinition definition, int requestedQuantity, long startTick, WorldPosition sourcePosition,
            double maximumDistance, TransferTimingDefinition timing)
        {
            if (!id.IsValid || source == null || destination == null || definition == null || timing == null
                || definition.StateKind != ItemStateKind.Stackable || source.Id.Equals(destination.Id)
                || !InventoryOperations.IsDefinitionCompatible(source, definition)
                || !InventoryOperations.IsDefinitionCompatible(destination, definition)
                || requestedQuantity <= 0 || startTick < 0 || maximumDistance < 0
                || double.IsNaN(maximumDistance) || double.IsInfinity(maximumDistance))
                return new TransferActionResult(null, TransferActionReason.InvalidRequest);
            int available = InventoryOperations.Count(source, definition.Id);
            if (available <= 0) return new TransferActionResult(null, TransferActionReason.SourceInsufficient);
            int fits = definition.UnitEncumbrance.Units == 0 ? requestedQuantity
                : (int)Math.Min(int.MaxValue, destination.RemainingCapacity.Units / definition.UnitEncumbrance.Units);
            int planned = Math.Min(requestedQuantity, Math.Min(available, fits));
            if (planned <= 0) return new TransferActionResult(null, TransferActionReason.DestinationFull);
            int duration = timing.DurationTicks(definition.UnitEncumbrance.Multiply(planned));
            return new TransferActionResult(new TransferActionState(id, source.Id, destination.Id, definition.Id,
                requestedQuantity, planned, startTick, duration, sourcePosition, maximumDistance), TransferActionReason.None);
        }

        public static TransferActionResult Advance(TransferActionState action, long currentTick, WorldPosition playerPosition,
            bool playerMoved, ContainerState source, ContainerState destination, ItemDefinition definition)
        {
            if (action == null)
                return new TransferActionResult(null, TransferActionReason.InvalidRequest);
            if (action.Status != TransferActionStatus.Active)
                return new TransferActionResult(action, TransferActionReason.AlreadyFinalized);
            if (currentTick < action.StartTick || source == null || destination == null || definition == null
                || definition.StateKind != ItemStateKind.Stackable)
                return Fail(action, currentTick, TransferActionReason.InvalidRequest);
            if (!source.Id.Equals(action.SourceId)
                || !destination.Id.Equals(action.DestinationId)
                || !definition.Id.Equals(action.DefinitionId)
                || !InventoryOperations.IsDefinitionCompatible(source, definition)
                || !InventoryOperations.IsDefinitionCompatible(destination, definition))
                return Fail(action, currentTick, TransferActionReason.InvalidRequest);
            if (playerMoved) return Interrupt(action, currentTick, TransferActionReason.PlayerMoved);
            if (playerPosition.DistanceSquaredTo(action.SourcePosition) > action.MaximumDistance * action.MaximumDistance)
                return Interrupt(action, currentTick, TransferActionReason.OutOfRange);
            if (currentTick - action.StartTick < action.DurationTicks)
                return new TransferActionResult(action, TransferActionReason.None);

            int available = InventoryOperations.Count(source, definition.Id);
            if (available <= 0) return Fail(action, currentTick, TransferActionReason.SourceInsufficient);
            int attempt = Math.Min(action.PlannedQuantity, available);
            TransferResult transfer = InventoryOperations.TransferStack(source, destination, definition, attempt);
            action.TransferredQuantity = transfer.Transferred;
            if (transfer.Transferred == 0) return Fail(action, currentTick, TransferActionReason.DestinationFull);
            action.Status = TransferActionStatus.Completed;
            action.Reason = TransferActionReason.None;
            action.FinalizedTick = currentTick;
            return new TransferActionResult(action, TransferActionReason.None);
        }

        private static TransferActionResult Interrupt(
            TransferActionState action,
            long currentTick,
            TransferActionReason reason)
        {
            action.Status = TransferActionStatus.Interrupted;
            action.Reason = reason;
            action.FinalizedTick = currentTick;
            return new TransferActionResult(action, reason);
        }

        private static TransferActionResult Fail(
            TransferActionState action,
            long currentTick,
            TransferActionReason reason)
        {
            action.Status = TransferActionStatus.Failed;
            action.Reason = reason;
            action.FinalizedTick = currentTick;
            return new TransferActionResult(action, reason);
        }
    }
}
