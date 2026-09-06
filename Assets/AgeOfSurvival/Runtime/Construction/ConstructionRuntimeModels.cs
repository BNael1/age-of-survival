using System;
using System.Collections.Generic;
using AgeOfSurvival.Core.Construction;
using AgeOfSurvival.Core.Inventory;
using AgeOfSurvival.Core.Persistence;

namespace AgeOfSurvival.Runtime.Construction
{
    public enum ConstructionCommandKind
    {
        Toggle = 0,
        Open = 1,
        Close = 2,
        SelectDefinition = 3,
        ConfirmPlacement = 4,
        Cancel = 5
    }

    public readonly struct ConstructionCommand
    {
        private ConstructionCommand(
            ConstructionCommandKind kind,
            ConstructionDefinitionId definitionId,
            ConstructionSpaceKey space)
        {
            Kind = kind;
            DefinitionId = definitionId;
            Space = space;
        }

        public ConstructionCommandKind Kind { get; }
        public ConstructionDefinitionId DefinitionId { get; }
        public ConstructionSpaceKey Space { get; }

        public static ConstructionCommand Toggle() =>
            new ConstructionCommand(ConstructionCommandKind.Toggle, default, default);

        public static ConstructionCommand Open() =>
            new ConstructionCommand(ConstructionCommandKind.Open, default, default);

        public static ConstructionCommand Close() =>
            new ConstructionCommand(ConstructionCommandKind.Close, default, default);

        public static ConstructionCommand Cancel() =>
            new ConstructionCommand(ConstructionCommandKind.Cancel, default, default);

        public static ConstructionCommand Select(ConstructionDefinitionId definitionId) =>
            new ConstructionCommand(
                ConstructionCommandKind.SelectDefinition,
                definitionId,
                default);

        public static ConstructionCommand Confirm(ConstructionSpaceKey space) =>
            new ConstructionCommand(
                ConstructionCommandKind.ConfirmPlacement,
                default,
                space);
    }

    public enum ConstructionPreviewStatus
    {
        None = 0,
        PointerBlockedByUi = 1,
        NotProjectable = 2,
        Projectable = 3,
        Occupied = 4
    }

    public readonly struct ConstructionPreviewState
    {
        private readonly byte _initialized;

        public ConstructionPreviewState(
            ConstructionSpaceKey space,
            ConstructionPreviewStatus status)
        {
            Space = space;
            Status = status;
            _initialized = 1;
        }

        public ConstructionSpaceKey Space { get; }
        public ConstructionPreviewStatus Status { get; }
        public bool HasTarget => _initialized == 1 && Space.IsValid;
        public bool IsTechnicallyPlaceable =>
            HasTarget && Status == ConstructionPreviewStatus.Projectable;
    }

    public sealed class ConstructionModeState
    {
        public bool IsActive { get; private set; }
        public ConstructionDefinitionId SelectedDefinitionId { get; private set; }
        public ConstructionPreviewState Preview { get; private set; }

        public bool HasSelection => SelectedDefinitionId.IsValid;

        public bool Execute(
            ConstructionCommand command,
            ConstructionPrototypeCatalog catalog)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));

            switch (command.Kind)
            {
                case ConstructionCommandKind.Toggle:
                    IsActive = !IsActive;
                    if (!IsActive) Preview = default;
                    return true;
                case ConstructionCommandKind.Open:
                    IsActive = true;
                    return true;
                case ConstructionCommandKind.Close:
                case ConstructionCommandKind.Cancel:
                    IsActive = false;
                    Preview = default;
                    return true;
                case ConstructionCommandKind.SelectDefinition:
                    if (!catalog.TryFind(command.DefinitionId, out _)) return false;
                    SelectedDefinitionId = command.DefinitionId;
                    IsActive = true;
                    Preview = default;
                    return true;
                case ConstructionCommandKind.ConfirmPlacement:
                    return false;
                default:
                    return false;
            }
        }

        public void SetPreview(ConstructionPreviewState preview)
        {
            Preview = IsActive && HasSelection ? preview : default;
        }
    }

    public sealed class ConstructionPrototypeDefinition
    {
        public ConstructionPrototypeDefinition(
            ConstructionDefinition core,
            string category,
            string displayName,
            string description,
            string visualKey)
        {
            Core = core ?? throw new ArgumentNullException(nameof(core));
            if (string.IsNullOrWhiteSpace(category))
                throw new ArgumentException("A category is required.", nameof(category));
            if (string.IsNullOrWhiteSpace(displayName))
                throw new ArgumentException("A display name is required.", nameof(displayName));
            if (string.IsNullOrWhiteSpace(visualKey))
                throw new ArgumentException("A visual key is required.", nameof(visualKey));

            Category = category;
            DisplayName = displayName;
            Description = description ?? string.Empty;
            VisualKey = visualKey;
        }

        public ConstructionDefinition Core { get; }
        public string Category { get; }
        public string DisplayName { get; }
        public string Description { get; }
        public string VisualKey { get; }
    }

    /// <summary>
    /// Centralized PROTOTYPE / NON GAMEPLAY FINAL construction data.
    /// </summary>
    public sealed class ConstructionPrototypeCatalog
    {
        public static readonly ConstructionDefinitionId FloorId =
            new ConstructionDefinitionId("prototype.floor.basic");
        public static readonly ConstructionDefinitionId WallId =
            new ConstructionDefinitionId("prototype.wall.basic");
        public static readonly ConstructionDefinitionId OpeningId =
            new ConstructionDefinitionId("prototype.opening.frame");
        public static readonly ConstructionDefinitionId RoofId =
            new ConstructionDefinitionId("prototype.roof.basic");
        public static readonly ConstructionDefinitionId DoorId =
            new ConstructionDefinitionId("prototype.door.basic");

        private readonly Dictionary<ConstructionDefinitionId, ConstructionPrototypeDefinition> _byId;
        private readonly IReadOnlyList<ConstructionPrototypeDefinition> _definitions;

        public string PersistenceCatalogId { get; }
        public int PersistenceCatalogRevision { get; }

        public ConstructionPrototypeCatalog(
            IEnumerable<ConstructionPrototypeDefinition> definitions,
            string persistenceCatalogId,
            int persistenceCatalogRevision)
        {
            if (definitions == null) throw new ArgumentNullException(nameof(definitions));
            AgeOfSurvival.Core.World.Generation.StableIdentifierValidation.Validate(
                persistenceCatalogId,
                nameof(persistenceCatalogId));
            if (persistenceCatalogRevision <= 0)
                throw new ArgumentOutOfRangeException(nameof(persistenceCatalogRevision));

            _byId = new Dictionary<ConstructionDefinitionId, ConstructionPrototypeDefinition>();
            var copy = new List<ConstructionPrototypeDefinition>();
            foreach (ConstructionPrototypeDefinition definition in definitions)
            {
                if (definition == null)
                    throw new ArgumentException("Definitions cannot contain null entries.", nameof(definitions));
                if (_byId.ContainsKey(definition.Core.Id))
                    throw new ArgumentException("Construction definition IDs must be unique.", nameof(definitions));
                _byId.Add(definition.Core.Id, definition);
                copy.Add(definition);
            }

            if (copy.Count == 0)
                throw new ArgumentException("At least one construction definition is required.", nameof(definitions));

            copy.Sort((left, right) => left.Core.Id.CompareTo(right.Core.Id));
            _definitions = copy.AsReadOnly();
            CoreCatalog = new ConstructionDefinitionCatalog(CoreDefinitions(copy));
            PersistenceCatalogId = persistenceCatalogId;
            PersistenceCatalogRevision = persistenceCatalogRevision;
        }

        public IReadOnlyList<ConstructionPrototypeDefinition> Definitions => _definitions;
        public ConstructionDefinitionCatalog CoreCatalog { get; }

        public bool TryFind(
            ConstructionDefinitionId id,
            out ConstructionPrototypeDefinition definition)
        {
            if (!id.IsValid)
            {
                definition = null;
                return false;
            }

            return _byId.TryGetValue(id, out definition);
        }

        public ConstructionPrototypeDefinition Require(ConstructionDefinitionId id)
        {
            if (!TryFind(id, out ConstructionPrototypeDefinition definition))
                throw new KeyNotFoundException($"Unknown construction definition '{id}'.");
            return definition;
        }

        public static ConstructionPrototypeCatalog CreateDefault()
        {
            var definitions = new List<ConstructionPrototypeDefinition>(CreateRevision1().Definitions)
            {
                Create(RoofId, ConstructionSpaceKind.Roof, 40, "Toitures", "Toit en bois",
                    "Toit prototype : compte pour le refuge seulement s'il est supporté.",
                    "construction_floor", Requirement("branches", 3), Requirement("wood", 1)),
                Create(DoorId, ConstructionSpaceKind.Edge, 40, "Bords", "Porte en bois",
                    "Porte prototype autonome avec cadre. E pour ouvrir/fermer.",
                    "construction_wall", Requirement("branches", 2), Requirement("wood", 2))
            };
            return new ConstructionPrototypeCatalog(definitions,
                ConstructionSaveDefaults.PrototypeCatalogId,
                ConstructionSaveDefaults.PrototypeCatalogRevision);
        }

        /// <summary>Exact historical catalog, used to validate V4/V5 saves before migration.</summary>
        public static ConstructionPrototypeCatalog CreateRevision1()
        {
            return new ConstructionPrototypeCatalog(
                new[]
                {
                    Create(
                        FloorId,
                        ConstructionSpaceKind.Surface,
                        30,
                        "Surfaces",
                        "Sol en bois",
                        "Plancher simple sur cellule.",
                        "construction_floor",
                        Requirement("branches", 2),
                        Requirement("stones", 1)),
                    Create(
                        WallId,
                        ConstructionSpaceKind.Edge,
                        45,
                        "Bords",
                        "Mur en bois",
                        "Mur simple sur arête canonique.",
                        "construction_wall",
                        Requirement("branches", 3),
                        Requirement("wood", 1)),
                    Create(
                        OpeningId,
                        ConstructionSpaceKind.Edge,
                        35,
                        "Bords",
                        "Cadre d'ouverture",
                        "Ouverture prototype sur arête canonique.",
                        "construction_opening",
                        Requirement("branches", 2),
                        Requirement("wood", 1))
                },
                ConstructionSaveDefaults.PrototypeCatalogId,
                1);
        }

        public static ConstructionRoofSupportPolicy CreateRoofSupportPolicy()
        {
            return new ConstructionRoofSupportPolicy(
                new[] { WallId, OpeningId, DoorId },
                2);
        }

        public static ConstructionDoorPolicy CreateDoorPolicy() => new ConstructionDoorPolicy(new[] { DoorId });

        public static ConstructionEnclosureBlockingPolicy CreateEnclosurePolicy() =>
            new ConstructionEnclosureBlockingPolicy(new[] { WallId }, CreateDoorPolicy());

        private static ConstructionPrototypeDefinition Create(
            ConstructionDefinitionId id,
            ConstructionSpaceKind kind,
            int work,
            string category,
            string name,
            string description,
            string visualKey,
            params ConstructionMaterialRequirement[] requirements)
        {
            return new ConstructionPrototypeDefinition(
                new ConstructionDefinition(id, kind, work, requirements),
                category,
                name,
                description,
                visualKey);
        }

        private static ConstructionMaterialRequirement Requirement(string id, int quantity) =>
            new ConstructionMaterialRequirement(new ItemDefinitionId(id), quantity);

        private static IEnumerable<ConstructionDefinition> CoreDefinitions(
            IEnumerable<ConstructionPrototypeDefinition> definitions)
        {
            foreach (ConstructionPrototypeDefinition definition in definitions)
                yield return definition.Core;
        }
    }

    public interface IConstructionInstanceIdAllocator
    {
        string InstanceNamespace { get; }
        long NextSequence { get; }
        bool TryPeekNext(
            Func<ConstructionInstanceId, bool> isAvailable,
            out ConstructionInstanceId candidate);
        void Commit(ConstructionInstanceId candidate);
    }

    /// <summary>
    /// Persistable monotonic allocator. The namespace separates local prototype IDs
    /// from future server/mod namespaces; only Commit advances the durable sequence.
    /// </summary>
    public sealed class MonotonicConstructionInstanceIdAllocator :
        IConstructionInstanceIdAllocator
    {
        private readonly string _namespace;
        private long _nextSequence;
        private ConstructionInstanceId _peeked;

        public MonotonicConstructionInstanceIdAllocator(
            string instanceNamespace,
            long nextSequence = 1L)
        {
            if (string.IsNullOrWhiteSpace(instanceNamespace))
                throw new ArgumentException("An instance namespace is required.", nameof(instanceNamespace));
            if (nextSequence <= 0L)
                throw new ArgumentOutOfRangeException(nameof(nextSequence));
            _namespace = instanceNamespace.Trim();
            _nextSequence = nextSequence;
        }

        public long NextSequence => _nextSequence;
        public string InstanceNamespace => _namespace;

        public bool TryPeekNext(
            Func<ConstructionInstanceId, bool> isAvailable,
            out ConstructionInstanceId candidate)
        {
            bool found = ConstructionInstanceIdSequencePolicy
                .TryFindCommittableCandidate(
                    _namespace,
                    _nextSequence,
                    isAvailable,
                    out candidate);
            _peeked = found ? candidate : default;
            return found;
        }

        public void Commit(ConstructionInstanceId candidate)
        {
            if (!candidate.IsValid || !_peeked.Equals(candidate))
                throw new InvalidOperationException("Only the current allocated candidate can be committed.");

            if (!ConstructionInstanceIdSequencePolicy.TryGetCommittedNextSequence(
                    _namespace,
                    _nextSequence,
                    candidate,
                    out long committedNextSequence))
            {
                throw new InvalidOperationException("The allocated construction ID cannot advance this allocator.");
            }

            _nextSequence = committedNextSequence;
            _peeked = default;
        }
    }

    public enum ConstructionRuntimeReason
    {
        None = 0,
        InvalidRequest = 1,
        NoSelection = 2,
        UnknownDefinition = 3,
        ProjectionInvalid = 4,
        SpaceOccupied = 5,
        IdExhausted = 6,
        SiteNotFound = 7,
        StructureNotFound = 8,
        MaterialNotRequired = 9,
        InventoryInsufficient = 10,
        WorkAlreadyComplete = 12,
        CoreRejected = 13,
        MaterialsIncomplete = 14,
        WorkActionInactive = 15,
        PlayerMoved = 16,
        OutOfRange = 17,
        ContextInterrupted = 18,
        AnotherActionActive = 19
    }

    public readonly struct ConstructionRuntimeResult
    {
        public ConstructionRuntimeResult(
            ConstructionRuntimeReason reason,
            ConstructionInstanceId instanceId,
            int accepted = 0,
            ConstructionOperationReason coreReason = ConstructionOperationReason.None,
            int groundOverflow = 0)
        {
            Reason = reason;
            InstanceId = instanceId;
            Accepted = accepted;
            CoreReason = coreReason;
            GroundOverflow = groundOverflow;
        }

        public ConstructionRuntimeReason Reason { get; }
        public ConstructionInstanceId InstanceId { get; }
        public int Accepted { get; }
        public ConstructionOperationReason CoreReason { get; }
        public int GroundOverflow { get; }
        public bool Succeeded => Reason == ConstructionRuntimeReason.None;
    }

    public sealed class ConstructionSiteView
    {
        public ConstructionSiteView(
            ConstructionSiteState site,
            ConstructionPrototypeDefinition definition,
            IReadOnlyList<ConstructionMaterialQuantity> deposited)
        {
            Site = site ?? throw new ArgumentNullException(nameof(site));
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            Deposited = deposited ?? throw new ArgumentNullException(nameof(deposited));
        }

        public ConstructionSiteState Site { get; }
        public ConstructionPrototypeDefinition Definition { get; }
        public IReadOnlyList<ConstructionMaterialQuantity> Deposited { get; }
    }
}
