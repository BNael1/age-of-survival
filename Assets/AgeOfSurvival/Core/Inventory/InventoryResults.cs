namespace AgeOfSurvival.Core.Inventory
{
    public enum InventoryOperationOutcome
    {
        Rejected = 0,
        Complete = 1,
        Partial = 2,
        DestinationFull = 3,
        SameContainer = 4,
        SourceInsufficient = 5
    }

    public readonly struct AddItemResult
    {
        internal AddItemResult(int requested, int accepted)
        {
            Requested = requested;
            Accepted = accepted;
        }

        public int Requested { get; }
        public int Accepted { get; }
        public int Remaining => Requested - Accepted;
        public bool Changed => Accepted > 0;
        public InventoryOperationOutcome Outcome => Accepted == Requested
            ? InventoryOperationOutcome.Complete
            : Accepted == 0
                ? InventoryOperationOutcome.DestinationFull
                : InventoryOperationOutcome.Partial;
    }

    public readonly struct RemoveItemResult
    {
        internal RemoveItemResult(int requested, int removed)
        {
            Requested = requested;
            Removed = removed;
        }

        public int Requested { get; }
        public int Removed { get; }
        public int Remaining => Requested - Removed;
        public bool Succeeded => Requested > 0 && Requested == Removed;
        public InventoryOperationOutcome Outcome => Succeeded
            ? InventoryOperationOutcome.Complete
            : InventoryOperationOutcome.SourceInsufficient;
    }

    public readonly struct TransferResult
    {
        internal TransferResult(
            int requested,
            int transferred,
            InventoryOperationOutcome outcome)
        {
            Requested = requested;
            Transferred = transferred;
            Outcome = outcome;
        }

        public int Requested { get; }
        public int Transferred { get; }
        public int Remaining => Requested - Transferred;
        public InventoryOperationOutcome Outcome { get; }
        public bool Changed => Transferred > 0;
    }
}
