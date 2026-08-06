using System;
using System.Collections.Generic;
using AgeOfSurvival.Core.Characters;
using AgeOfSurvival.Core.Inventory;
using AgeOfSurvival.Core.Persistence;
using AgeOfSurvival.Core.World.Generation;

namespace AgeOfSurvival.Runtime.Inventory
{
    public sealed partial class InventoryPrototypeSession
    {
        private WorldPopulationSettings _persistenceWorld =
            WorldPopulationDefaults.CreateTemperatePrototypeV1(
                new WorldSeed(0UL));
        private bool _restoredFromSave;

        internal InventoryPrototypeSession(RestoredGameState restored)
        {
            if (restored == null) throw new ArgumentNullException(nameof(restored));

            _readOnlyResources = _resources.AsReadOnly();
            _readOnlyGroundContainers = _groundContainers.AsReadOnly();
            Inventory = restored.Inventory;
            MainContainer = Inventory.MainContainer;
            BagContainer = Inventory.FindContainer(
                InventoryPrototypeCatalog.BagContainerId)
                ?? throw new InvalidOperationException(
                    "The saved prototype bag container is missing.");

            if (!Inventory.TryFindUnique(
                    new ItemInstanceId("prototype-tool-01"),
                    out UniqueItemState tool,
                    out _,
                    out _))
            {
                throw new InvalidOperationException(
                    "The saved prototype tool is missing.");
            }

            if (!Inventory.TryFindUnique(
                    new ItemInstanceId("prototype-bag-01"),
                    out UniqueItemState bag,
                    out _,
                    out _))
            {
                throw new InvalidOperationException(
                    "The saved prototype bag is missing.");
            }

            Tool = tool;
            Bag = bag;
            Commands = new InventoryPrototypeCommands(Inventory);
            CurrentTick = restored.FixedTick;
            Health = restored.Health;
            CurrentPlayerPosition = restored.PlayerPosition;
            _persistenceWorld = restored.World;
            _restoredFromSave = true;

            ChunkMutationState[] mutations =
                restored.Chunks.Store.CaptureCanonicalStates();
            for (int index = 0; index < mutations.Length; index++)
            {
                _chunkMutationStore.Put(mutations[index]);
            }
        }

        public bool RestoredFromSave => _restoredFromSave;
        public WorldPopulationSettings PersistenceWorld => _persistenceWorld;

        public GameSaveSnapshot CaptureGameSaveSnapshot()
        {
            var mutations = new List<ChunkMutationState>();
            var known = new HashSet<ChunkCoordinate>();
            ChunkMutationState[] stored =
                _chunkMutationStore.CaptureCanonicalStates();
            for (int index = 0; index < stored.Length; index++)
            {
                if (!known.Add(stored[index].Coordinate))
                {
                    throw new InvalidOperationException(
                        "A stored chunk mutation is duplicated.");
                }

                mutations.Add(stored[index]);
            }

            var active = new List<ChunkCoordinate>(_activeGeneratedChunks);
            active.Sort();
            for (int index = 0; index < active.Count; index++)
            {
                ChunkMutationState mutation = CaptureChunkMutation(
                    active[index],
                    _persistenceWorld.Generation.ChunkLayout);
                if (mutation.IsEmpty) continue;
                if (!known.Add(mutation.Coordinate))
                {
                    throw new InvalidOperationException(
                        "A chunk cannot be active and stored simultaneously.");
                }

                mutations.Add(mutation);
            }

            return new GameSaveSnapshot(
                new WorldIdentitySnapshot(
                    _persistenceWorld.Generation,
                    _persistenceWorld.Profile.Id,
                    _persistenceWorld.Profile.Revision),
                CurrentTick,
                CurrentPlayerPosition,
                new PlayerHealthSnapshot(Health),
                Inventory.CaptureSnapshot(),
                mutations);
        }

        public bool CancelActiveTransferForSaveAndQuit()
        {
            if (TransferAction == null
                || TransferAction.Status != TransferActionStatus.Active)
            {
                return false;
            }

            TransferActionResult result = AdvanceTransfer(
                CurrentTick,
                CurrentPlayerPosition,
                true);
            return result.Action != null
                && result.Action.Status == TransferActionStatus.Interrupted;
        }
    }
}
