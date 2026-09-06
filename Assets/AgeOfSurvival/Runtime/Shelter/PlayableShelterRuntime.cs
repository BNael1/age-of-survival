using System;
using AgeOfSurvival.Core.Characters;
using AgeOfSurvival.Core.Construction;
using AgeOfSurvival.Core.Shelter;
using AgeOfSurvival.Runtime.Construction;
using AgeOfSurvival.Runtime.Inventory;
using UnityEngine;

namespace AgeOfSurvival.Runtime.Shelter
{
    /// <summary>PROTOTYPE / NON GAMEPLAY FINAL. One hour = existing 216000-tick food day / 24.</summary>
    public static class ShelterPrototypeProfile
    {
        public const long TicksPerGameHour = 9000;
        public const long RestTicks = 180;
        public const long SleepTicks = 360;
    }

    public sealed class PlayableShelterRuntime : IDisposable
    {
        private readonly InventoryPrototypeSession _inventory;
        private readonly ConstructionRuntimeSession _construction;
        private readonly ConstructionShelterRuntimeBridge _bridge;
        private long _lastTick;
        private readonly IShelterComfortPolicy _comfort = new BaselineShelterComfortPolicy();
        public PlayableShelterRuntime(InventoryPrototypeSession inventory, ConstructionRuntimeSession construction)
        {
            _inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
            _construction = construction ?? throw new ArgumentNullException(nameof(construction));
            Session = new ShelterRuntimeSession(construction.Catalog.CoreCatalog,
                ConstructionPrototypeCatalog.CreateEnclosurePolicy(), ConstructionRoomAnalysisLimits.Default,
                ConstructionPrototypeCatalog.CreateRoofSupportPolicy(), ConstructionPrototypeCatalog.FloorId,
                new FullFloorSupportedRoofShelterPolicy(), new RoomFingerprintIdentityStrategy(),
                new ShelterFamiliarityRules(ShelterPrototypeProfile.TicksPerGameHour), inventory.Shelters);
            _bridge = new ConstructionShelterRuntimeBridge(construction, Session);
            _lastTick = inventory.CurrentTick;
            Reconcile(inventory.CurrentPlayerPosition, 0);
            construction.ShelterInvalidated += Refresh;
        }
        public ShelterRuntimeSession Session { get; }
        public ShelterRestSleepAction Action { get; private set; }
        public int ComfortScore => _comfort.Score(Session.CurrentShelterId,
            Session.HasCurrentShelter && Session.ContainsValid(Session.CurrentShelterId));
        public bool TryStart(ShelterRestSleepKind kind)
        {
            if (Action != null && Action.Status == ShelterRestSleepStatus.Active || !Session.HasCurrentShelter
                || !Session.ContainsValid(Session.CurrentShelterId)) return false;
            Action = ShelterRestSleepOperations.Start(kind, Session.CurrentShelterId, _inventory.CurrentTick,
                kind == ShelterRestSleepKind.Rest ? ShelterPrototypeProfile.RestTicks : ShelterPrototypeProfile.SleepTicks);
            return Action != null;
        }
        public void CancelForSaveAndQuit() => ShelterRestSleepOperations.Cancel(Action, _inventory.CurrentTick,
            ShelterRestSleepReason.SaveAndQuit);
        public bool IsActionActive => Action != null && Action.Status == ShelterRestSleepStatus.Active;
        public void Refresh()
        {
            _bridge.RefreshIfChanged();
            Reconcile(_inventory.CurrentPlayerPosition, 0);
            AdvanceAction(_inventory.CurrentTick, false);
        }
        public void AdvanceFixedTick(long tick, WorldPosition position, bool moved)
        {
            if (tick <= _lastTick) return;
            _bridge.RefreshIfChanged();
            Reconcile(position, tick - _lastTick);
            AdvanceAction(tick, moved);
            _lastTick = tick;
        }
        private void AdvanceAction(long tick, bool moved)
        {
            ShelterRestSleepOperations.Advance(Action, tick, Session.CurrentShelterId, Session.HasCurrentShelter,
                moved, Session.ContainsValid, kind =>
                {
                    if (kind == ShelterRestSleepKind.Rest) Session.RecordCompletedRest();
                    else Session.RecordCompletedNight();
                });
        }
        private void Reconcile(WorldPosition position, long elapsed)
        {
            if (position.TryToWorldCell(out var cell)) Session.AdvanceFixedTick(cell, elapsed);
            else Session.ClearPresence();
        }
        public void Dispose() { _construction.ShelterInvalidated -= Refresh; }
    }

    public static class ShelterRuntimeSessionProvider
    {
        private static PlayableShelterRuntime _current;
        private static InventoryPrototypeSession _inventory;
        private static ConstructionRuntimeSession _construction;
        public static PlayableShelterRuntime Current
        {
            get
            {
                var inventory = InventoryPrototypeSessionProvider.Current;
                var construction = ConstructionRuntimeSessionProvider.Current;
                if (_current == null || !ReferenceEquals(inventory, _inventory) || !ReferenceEquals(construction, _construction))
                    InstallPrepared(new PlayableShelterRuntime(inventory, construction), inventory, construction);
                return _current;
            }
        }
        internal static void InstallPrepared(PlayableShelterRuntime runtime, InventoryPrototypeSession inventory,
            ConstructionRuntimeSession construction)
        {
            _current?.Dispose();
            _current = runtime;
            _inventory = inventory;
            _construction = construction;
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() { _current?.Dispose(); _current = null; _inventory = null; _construction = null; }
    }
}
