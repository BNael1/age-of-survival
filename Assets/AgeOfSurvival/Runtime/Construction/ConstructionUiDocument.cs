using System;
using System.Collections.Generic;
using AgeOfSurvival.Core.Construction;
using AgeOfSurvival.Core.Inventory;
using UnityEngine;
using UnityEngine.UIElements;

namespace AgeOfSurvival.Runtime.Construction
{
    public sealed class ConstructionUiDocument
    {
        private readonly ConstructionRuntimeSession _session;
        private readonly Action _openMode;
        private readonly Action _closeMode;
        private readonly Func<ConstructionInstanceId, ConstructionRuntimeResult> _beginWork;
        private readonly Action _endWork;
        private readonly VisualElement _panel;
        private readonly VisualElement _pieces;
        private readonly ScrollView _sites;
        private readonly Label _activePiece;
        private readonly Label _status;
        private readonly Button _toggle;
        private string _lastSignature;

        public ConstructionUiDocument(
            VisualElement root,
            ConstructionRuntimeSession session,
            Action openMode,
            Action closeMode,
            Func<ConstructionInstanceId, ConstructionRuntimeResult> beginWork,
            Action endWork)
        {
            Root = root ?? throw new ArgumentNullException(nameof(root));
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _openMode = openMode ?? throw new ArgumentNullException(nameof(openMode));
            _closeMode = closeMode ?? throw new ArgumentNullException(nameof(closeMode));
            _beginWork = beginWork ?? throw new ArgumentNullException(nameof(beginWork));
            _endWork = endWork ?? throw new ArgumentNullException(nameof(endWork));

            ConfigureRoot(Root);
            _toggle = Button("Construction [B]", ToggleMode, "construction-toggle");
            _toggle.style.position = Position.Absolute;
            _toggle.style.right = 14;
            _toggle.style.top = 12;
            _toggle.style.width = 168;
            Root.Add(_toggle);

            _panel = new VisualElement { name = "construction-panel" };
            ConfigurePanel(_panel);
            Root.Add(_panel);

            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            var title = new Label("MODE CONSTRUCTION");
            title.style.flexGrow = 1;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.fontSize = 17;
            header.Add(title);
            header.Add(Button("Fermer", _closeMode, "construction-close"));
            _panel.Add(header);

            var notice = new Label("PROTOTYPE — coûts et contrôles non définitifs");
            notice.style.color = new Color(0.95f, 0.72f, 0.32f, 1f);
            notice.style.marginTop = 4;
            notice.style.marginBottom = 7;
            _panel.Add(notice);

            _pieces = new VisualElement { name = "construction-pieces" };
            _pieces.style.flexDirection = FlexDirection.Row;
            _pieces.style.flexWrap = Wrap.Wrap;
            _panel.Add(_pieces);
            BuildPieceButtons();

            _activePiece = new Label();
            _activePiece.style.marginTop = 7;
            _activePiece.style.marginBottom = 5;
            _activePiece.style.unityFontStyleAndWeight = FontStyle.Bold;
            _panel.Add(_activePiece);

            var divider = new VisualElement();
            divider.style.height = 1;
            divider.style.backgroundColor = new Color(0.31f, 0.42f, 0.36f, 1f);
            divider.style.marginBottom = 6;
            _panel.Add(divider);

            var sitesTitle = new Label("Chantiers et structures");
            sitesTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            _panel.Add(sitesTitle);

            _sites = new ScrollView(ScrollViewMode.Vertical) { name = "construction-sites" };
            _sites.style.flexGrow = 1;
            _sites.style.marginTop = 4;
            _panel.Add(_sites);

            _status = new Label();
            _status.style.whiteSpace = WhiteSpace.Normal;
            _status.style.minHeight = 34;
            _status.style.marginTop = 6;
            _status.style.paddingTop = 5;
            _status.style.borderTopWidth = 1;
            _status.style.borderTopColor = new Color(0.31f, 0.42f, 0.36f, 1f);
            _panel.Add(_status);

            Refresh(force: true);
        }

        public VisualElement Root { get; }
        public VisualElement Panel => _panel;
        public Button ToggleButton => _toggle;
        public bool IsOpen => _session.Mode.IsActive;

        public void Refresh(bool force = false)
        {
            _panel.style.display = _session.Mode.IsActive
                ? DisplayStyle.Flex
                : DisplayStyle.None;
            _toggle.text = _session.Mode.IsActive
                ? "Construction active"
                : "Construction [B]";

            _activePiece.text = ActivePieceText();
            string signature = StateSignature();
            if (force || !string.Equals(signature, _lastSignature, StringComparison.Ordinal))
            {
                _lastSignature = signature;
                RebuildSites();
            }
            _status.text = StatusText();
        }

        public void SelectForTests(ConstructionDefinitionId id)
        {
            _session.Execute(ConstructionCommand.Select(id));
            Refresh(force: true);
        }

        private void ToggleMode()
        {
            if (_session.Mode.IsActive) _closeMode();
            else _openMode();
            Refresh(force: true);
        }

        private void BuildPieceButtons()
        {
            IReadOnlyList<ConstructionPrototypeDefinition> definitions =
                _session.Catalog.Definitions;
            for (int index = 0; index < definitions.Count; index++)
            {
                ConstructionPrototypeDefinition definition = definitions[index];
                string kind = definition.Core.SpaceKind == ConstructionSpaceKind.Surface
                    ? "Surface"
                    : definition.Core.SpaceKind == ConstructionSpaceKind.Roof ? "Toit" : "Edge";
                Button button = Button(
                    definition.DisplayName + "\n" + kind,
                    () =>
                    {
                        _session.Execute(ConstructionCommand.Select(definition.Core.Id));
                        Refresh(force: true);
                    },
                    "piece-" + definition.Core.Id.Value);
                button.style.width = 145;
                button.style.height = 46;
                button.style.whiteSpace = WhiteSpace.Normal;
                _pieces.Add(button);
            }
        }

        private void RebuildSites()
        {
            _sites.Clear();
            ConstructionSiteView[] sites = _session.CaptureSites();
            CompletedStructureState[] structures = _session.World.CaptureCanonicalStructures();
            if (sites.Length == 0 && structures.Length == 0)
            {
                var empty = new Label("Aucun placement. Sélectionnez une pièce puis cliquez dans le monde.");
                empty.style.whiteSpace = WhiteSpace.Normal;
                empty.style.color = new Color(0.72f, 0.78f, 0.74f, 1f);
                _sites.Add(empty);
                return;
            }

            for (int index = 0; index < sites.Length; index++)
                _sites.Add(CreateSiteCard(sites[index]));
            for (int index = 0; index < structures.Length; index++)
                _sites.Add(CreateStructureCard(structures[index]));
        }

        private VisualElement CreateSiteCard(ConstructionSiteView view)
        {
            VisualElement card = Card();
            var title = new Label(view.Definition.DisplayName + " — chantier");
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            card.Add(title);

            var requirements = new List<string>();
            for (int index = 0; index < view.Definition.Core.Materials.Count; index++)
            {
                ConstructionMaterialRequirement requirement =
                    view.Definition.Core.Materials[index];
                int deposited = _session.DepositedQuantity(
                    view.Site.InstanceId,
                    requirement.DefinitionId);
                requirements.Add($"{requirement.DefinitionId}: {deposited}/{requirement.Quantity}");
            }
            card.Add(new Label(string.Join(" · ", requirements)) { style = { whiteSpace = WhiteSpace.Normal } });
            card.Add(new Label(
                $"Travail : {view.Site.WorkCompletedUnits}/{view.Definition.Core.RequiredWorkUnits}"));

            var actions = new VisualElement();
            actions.style.flexDirection = FlexDirection.Row;
            actions.style.flexWrap = Wrap.Wrap;
            actions.Add(Button("Apporter", () =>
            {
                _session.DepositAllAvailable(view.Site.InstanceId);
                Refresh(force: true);
            }, "deposit-" + view.Site.InstanceId.Value));
            Button work = HoldButton(
                "Maintenir : travailler",
                () =>
                {
                    _beginWork(view.Site.InstanceId);
                    Refresh(force: true);
                },
                () =>
                {
                    _endWork();
                    Refresh(force: true);
                },
                "work-" + view.Site.InstanceId.Value);
            work.SetEnabled(_session.AreMaterialsComplete(view.Site.InstanceId));
            actions.Add(work);
            actions.Add(Button("Démonter 100%", () =>
            {
                _session.TryDismantle(view.Site.InstanceId);
                Refresh(force: true);
            }, "dismantle-" + view.Site.InstanceId.Value));
            card.Add(actions);
            return card;
        }

        private VisualElement CreateStructureCard(CompletedStructureState structure)
        {
            VisualElement card = Card();
            ConstructionPrototypeDefinition definition =
                _session.Catalog.Require(structure.DefinitionId);
            var title = new Label(definition.DisplayName + " — terminé");
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.color = new Color(0.4f, 0.93f, 0.65f, 1f);
            card.Add(title);
            card.Add(Button("Démonter (~70% floor)", () =>
            {
                _session.TryDismantle(structure.InstanceId);
                Refresh(force: true);
            }, "dismantle-" + structure.InstanceId.Value));
            return card;
        }

        private string ActivePieceText()
        {
            if (!_session.Mode.HasSelection) return "Pièce active : aucune";
            ConstructionPrototypeDefinition definition =
                _session.Catalog.Require(_session.Mode.SelectedDefinitionId);
            string status = _session.Mode.Preview.Status == ConstructionPreviewStatus.Projectable
                ? " — cible techniquement plaçable"
                : _session.Mode.Preview.Status == ConstructionPreviewStatus.Occupied
                    ? " — espace occupé"
                    : string.Empty;
            return "Pièce active : " + definition.DisplayName + status;
        }

        private string StatusText()
        {
            ConstructionRuntimeResult result = _session.LastResult;
            if (result.Succeeded && result.InstanceId.IsValid)
            {
                if (result.GroundOverflow > 0)
                    return $"Récupération réussie : {result.Accepted} unité(s), dont {result.GroundOverflow} au sol.";
                return result.Accepted > 0
                    ? $"Opération réussie : {result.Accepted} unité(s)."
                    : "Opération réussie.";
            }
            switch (result.Reason)
            {
                case ConstructionRuntimeReason.None:
                    return "Le monde continue pendant la construction. Clic gauche place, clic droit ferme (prototype).";
                case ConstructionRuntimeReason.InventoryInsufficient:
                    return "Matériaux disponibles insuffisants.";
                case ConstructionRuntimeReason.MaterialsIncomplete:
                    return "Tous les matériaux doivent être déposés avant de travailler.";
                case ConstructionRuntimeReason.PlayerMoved:
                    return "Travail interrompu par le déplacement.";
                case ConstructionRuntimeReason.OutOfRange:
                    return "Travail interrompu : chantier hors de portée.";
                case ConstructionRuntimeReason.WorkActionInactive:
                    return "Maintenez le bouton pour travailler activement.";
                case ConstructionRuntimeReason.SpaceOccupied:
                    return "Espace canonique déjà occupé.";
                default:
                    return "État : " + result.Reason;
            }
        }

        private string StateSignature()
        {
            var parts = new List<string>
            {
                _session.Mode.IsActive.ToString(),
                _session.Mode.SelectedDefinitionId.ToString(),
                _session.ActiveWorkSite.ToString()
            };
            ConstructionSiteView[] sites = _session.CaptureSites();
            for (int index = 0; index < sites.Length; index++)
            {
                ConstructionSiteView site = sites[index];
                parts.Add(site.Site.InstanceId + ":" + site.Site.WorkCompletedUnits);
                for (int material = 0; material < site.Deposited.Count; material++)
                    parts.Add(site.Deposited[material].ToString());
            }
            CompletedStructureState[] structures = _session.World.CaptureCanonicalStructures();
            for (int index = 0; index < structures.Length; index++)
                parts.Add("done:" + structures[index].InstanceId);
            return string.Join("|", parts);
        }

        private static VisualElement Card()
        {
            var card = new VisualElement();
            card.style.paddingLeft = 7;
            card.style.paddingRight = 7;
            card.style.paddingTop = 6;
            card.style.paddingBottom = 6;
            card.style.marginBottom = 5;
            card.style.backgroundColor = new Color(0.12f, 0.16f, 0.14f, 0.98f);
            return card;
        }

        private static Button Button(string text, Action action, string name)
        {
            var button = new Button(action) { text = text, name = name };
            button.style.marginRight = 4;
            button.style.marginBottom = 4;
            button.style.minHeight = 30;
            button.style.backgroundColor = new Color(0.23f, 0.31f, 0.27f, 1f);
            button.style.color = Color.white;
            return button;
        }

        private static Button HoldButton(
            string text,
            Action begin,
            Action end,
            string name)
        {
            var button = new Button { text = text, name = name };
            button.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button != 0) return;
                begin();
                evt.StopPropagation();
            });
            button.RegisterCallback<PointerUpEvent>(evt =>
            {
                if (evt.button != 0) return;
                end();
                evt.StopPropagation();
            });
            button.style.marginRight = 4;
            button.style.marginBottom = 4;
            button.style.minHeight = 30;
            button.style.backgroundColor = new Color(0.23f, 0.31f, 0.27f, 1f);
            button.style.color = Color.white;
            return button;
        }

        private static void ConfigureRoot(VisualElement root)
        {
            root.style.position = Position.Absolute;
            root.style.left = 0;
            root.style.right = 0;
            root.style.top = 0;
            root.style.bottom = 0;
            root.style.color = Color.white;
            root.pickingMode = PickingMode.Ignore;
        }

        private static void ConfigurePanel(VisualElement panel)
        {
            panel.pickingMode = PickingMode.Position;
            panel.style.position = Position.Absolute;
            panel.style.right = 14;
            panel.style.top = 54;
            panel.style.bottom = 14;
            panel.style.width = 350;
            panel.style.paddingLeft = 11;
            panel.style.paddingRight = 11;
            panel.style.paddingTop = 10;
            panel.style.paddingBottom = 10;
            panel.style.backgroundColor = new Color(0.055f, 0.07f, 0.065f, 0.97f);
            panel.style.borderTopLeftRadius = 5;
            panel.style.borderTopRightRadius = 5;
            panel.style.borderBottomLeftRadius = 5;
            panel.style.borderBottomRightRadius = 5;
        }
    }
}
