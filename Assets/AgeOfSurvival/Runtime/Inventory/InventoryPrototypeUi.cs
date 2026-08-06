using System;
using System.Collections.Generic;
using AgeOfSurvival.Core.Inventory;
using UnityEngine;
using UnityEngine.UIElements;

namespace AgeOfSurvival.Runtime.Inventory
{
    /// <summary>
    /// UI Toolkit view. It consumes immutable view-models and emits commands through
    /// InventoryPrototypeCommands; it never mutates Core collections directly.
    /// </summary>
    public sealed class InventoryPrototypeUiDocument
    {
        private readonly InventoryPrototypeSession _session;
        private readonly VisualElement _root;
        private readonly VisualElement _panel;
        private readonly Label _grossLoad;
        private readonly Label _perceivedLoad;
        private readonly Label _reductionInfo;
        private readonly Label _movementLoad;
        private readonly Label _movementMultiplier;
        private readonly Label[] _equipmentLabels = new Label[3];
        private readonly ListView _mainList;
        private readonly ListView _bagList;
        private readonly ListView _groundList;
        private readonly Label _mainCapacity;
        private readonly Label _bagCapacity;
        private readonly Label _groundCapacity;
        private readonly Label _transferStatus;
        private readonly ProgressBar _transferProgress;
        private readonly Button _transferButton;
        private readonly Button[] _equipButtons = new Button[3];
        private readonly Button[] _unequipButtons = new Button[3];
        private bool _isPanelOpen;
        private InventorySelection _selection;

        public InventoryPrototypeUiDocument(VisualElement root, InventoryPrototypeSession session)
        {
            _root = root ?? throw new ArgumentNullException(nameof(root));
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _root.Clear();
            ConfigureRoot(_root);

            var toggle = CreateButton("Inventory prototype", TogglePanel);
            toggle.name = "inventory-toggle";
            toggle.style.position = Position.Absolute;
            toggle.style.left = 14;
            toggle.style.top = 14;
            _root.Add(toggle);

            _panel = new VisualElement { name = "inventory-panel" };
            ConfigurePanel(_panel);
            _root.Add(_panel);
            SetPanelOpen(false);

            var title = new Label("INVENTORY — PROTOTYPE LIST VIEW");
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.fontSize = 18;
            title.style.marginBottom = 8;
            _panel.Add(title);

            var loadRow = new VisualElement();
            loadRow.style.flexDirection = FlexDirection.Row;
            loadRow.style.marginBottom = 6;
            _grossLoad = CreateInfoLabel();
            _perceivedLoad = CreateInfoLabel();
            loadRow.Add(_grossLoad);
            loadRow.Add(_perceivedLoad);
            _panel.Add(loadRow);

            _reductionInfo = new Label();
            _reductionInfo.tooltip = "Equipped-container reduction changes perceived load only; raw bag capacity never changes.";
            _reductionInfo.style.color = new Color(0.72f, 0.83f, 0.73f);
            _reductionInfo.style.marginBottom = 6;
            _panel.Add(_reductionInfo);

            var movementRow = new VisualElement();
            movementRow.style.flexDirection = FlexDirection.Row;
            movementRow.style.marginBottom = 8;
            _movementLoad = CreateInfoLabel();
            _movementLoad.name = "movement-load";
            _movementMultiplier = CreateInfoLabel();
            _movementMultiplier.name = "movement-multiplier";
            movementRow.Add(_movementLoad);
            movementRow.Add(_movementMultiplier);
            _panel.Add(movementRow);

            _transferStatus = new Label();
            _transferStatus.style.unityFontStyleAndWeight = FontStyle.Bold;
            _panel.Add(_transferStatus);
            _transferProgress = new ProgressBar { title = "Timed transfer", lowValue = 0f, highValue = 100f };
            _transferProgress.style.marginBottom = 8;
            _panel.Add(_transferProgress);

            var equipment = new VisualElement();
            equipment.name = "equipment-slots";
            equipment.style.flexDirection = FlexDirection.Row;
            equipment.style.marginBottom = 8;
            EquipmentSlot[] slots = StableSlots();
            for (int index = 0; index < slots.Length; index++)
            {
                int captured = index;
                var slot = new VisualElement();
                slot.style.flexGrow = 1;
                slot.style.marginRight = 6;
                slot.style.paddingLeft = 6;
                slot.style.paddingRight = 6;
                slot.style.paddingTop = 5;
                slot.style.paddingBottom = 5;
                slot.style.backgroundColor = new Color(0.12f, 0.15f, 0.14f, 0.96f);
                _equipmentLabels[index] = new Label();
                _equipmentLabels[index].style.unityFontStyleAndWeight = FontStyle.Bold;
                slot.Add(_equipmentLabels[index]);

                var actions = new VisualElement();
                actions.style.flexDirection = FlexDirection.Row;
                _equipButtons[index] = CreateButton("Equip", () => Equip(slots[captured]));
                _unequipButtons[index] = CreateButton("Unequip", () => Unequip(slots[captured]));
                actions.Add(_equipButtons[index]);
                actions.Add(_unequipButtons[index]);
                slot.Add(actions);
                equipment.Add(slot);
            }
            _panel.Add(equipment);

            var containers = new VisualElement();
            containers.style.flexDirection = FlexDirection.Row;
            containers.style.flexGrow = 1;
            _mainList = CreateContainerPanel(containers, "main-container", out _mainCapacity);
            _bagList = CreateContainerPanel(containers, "bag-container", out _bagCapacity);
            _groundList = CreateContainerPanel(containers, "ground-container", out _groundCapacity);
            ConfigureSelection(_mainList, _bagList, _groundList);
            ConfigureSelection(_bagList, _mainList, _groundList);
            ConfigureSelection(_groundList, _mainList, _bagList);
            _panel.Add(containers);

            var footer = new VisualElement();
            footer.style.flexDirection = FlexDirection.Row;
            footer.style.marginTop = 8;
            _transferButton = CreateButton("Transfer selected", TransferSelected);
            _transferButton.tooltip = "Moves as much as the destination can accept; the remainder stays in the source.";
            footer.Add(_transferButton);
            footer.Add(CreateButton("Close", TogglePanel));
            _panel.Add(footer);

            Refresh();
        }

        public ListView MainList => _mainList;
        public ListView BagList => _bagList;
        public ListView GroundList => _groundList;
        public Button TransferButton => _transferButton;
        public IReadOnlyList<Button> EquipButtons => _equipButtons;
        public IReadOnlyList<Button> UnequipButtons => _unequipButtons;
        public VisualElement Panel => _panel;
        public bool IsPanelOpen => _isPanelOpen;

        public void SelectForTests(InventorySelection selection)
        {
            _selection = selection;
            RefreshButtons();
        }

        public void Refresh()
        {
            InventoryPrototypeViewModel viewModel =
                InventoryPrototypeViewModelBuilder.Build(_session);
            _grossLoad.text = $"Gross carried: {viewModel.GrossLoadText}";
            _perceivedLoad.text = $"Perceived carried: {viewModel.PerceivedLoadText}";
            _reductionInfo.text = viewModel.ReductionText;
            _movementLoad.text = $"Movement load: {viewModel.MovementLoadText}";
            _movementMultiplier.text = $"Movement speed: {viewModel.MovementMultiplierText}";
            _transferStatus.text = viewModel.TransferStatusText;
            _transferProgress.value = (float)(viewModel.TransferProgress * 100.0);
            for (int index = 0; index < _equipmentLabels.Length; index++)
            {
                _equipmentLabels[index].text = viewModel.EquipmentLabels[index];
            }

            BindContainer(_mainList, _mainCapacity, viewModel.Main);
            BindContainer(_bagList, _bagCapacity, viewModel.Bag);
            BindContainer(_groundList, _groundCapacity, viewModel.Ground);
            RefreshButtons();
        }

        private void TransferSelected()
        {
            GroundContainerState ground = _session.FindGround(_selection.SourceContainerId);
            if (ground != null)
            {
                int quantity = InventoryOperations.Count(ground.Container, _selection.DefinitionId);
                _session.StartGroundTransfer(ground, quantity, _session.CurrentTick);
                ClearSelection();
                Refresh();
                return;
            }
            ContainerId destination = _selection.SourceContainerId.Equals(InventoryPrototypeCatalog.MainContainerId)
                ? InventoryPrototypeCatalog.BagContainerId
                : InventoryPrototypeCatalog.MainContainerId;
            _session.Commands.Transfer(_selection, destination);
            ClearSelection();
            Refresh();
        }

        private void Equip(EquipmentSlot slot)
        {
            _session.Commands.Equip(_selection, slot);
            ClearSelection();
            Refresh();
        }

        private void Unequip(EquipmentSlot slot)
        {
            _session.Commands.Unequip(slot);
            Refresh();
        }

        public void TogglePanel()
        {
            SetPanelOpen(!_isPanelOpen);
        }

        private void SetPanelOpen(bool isOpen)
        {
            _isPanelOpen = isOpen;
            _panel.style.display = isOpen
                ? DisplayStyle.Flex
                : DisplayStyle.None;
        }

        private void ClearSelection()
        {
            _selection = default;
            _mainList.ClearSelection();
            _bagList.ClearSelection();
            _groundList.ClearSelection();
        }

        private void RefreshButtons()
        {
            ContainerId destination = _selection.SourceContainerId.Equals(InventoryPrototypeCatalog.MainContainerId)
                ? InventoryPrototypeCatalog.BagContainerId
                : InventoryPrototypeCatalog.MainContainerId;
            GroundContainerState ground = _session.FindGround(_selection.SourceContainerId);
            _transferButton.SetEnabled(ground != null
                ? _session.CanStartGroundTransfer(ground)
                : _session.Commands.CanTransfer(_selection, destination));

            EquipmentSlot[] slots = StableSlots();
            for (int index = 0; index < slots.Length; index++)
            {
                _equipButtons[index].SetEnabled(_session.Commands.CanEquip(_selection, slots[index]));
                _unequipButtons[index].SetEnabled(_session.Commands.CanUnequip(slots[index]));
            }
        }

        private void ConfigureSelection(
            ListView list,
            ListView otherList,
            ListView secondOtherList)
        {
            list.selectionChanged += selectedItems =>
            {
                InventoryRowViewModel selected = null;
                foreach (object item in selectedItems)
                {
                    selected = item as InventoryRowViewModel;
                    break;
                }

                if (selected == null) return;
                _selection = selected.Selection;
                otherList.ClearSelection();
                secondOtherList.ClearSelection();
                RefreshButtons();
            };
        }

        private static void BindContainer(
            ListView list,
            Label capacity,
            InventoryContainerViewModel viewModel)
        {
            capacity.text = $"{viewModel.DisplayName} — {viewModel.CapacityText}";
            list.itemsSource = new List<InventoryRowViewModel>(viewModel.Rows);
            list.Rebuild();
        }

        private static ListView CreateContainerPanel(
            VisualElement parent,
            string name,
            out Label capacity)
        {
            var panel = new VisualElement { name = name };
            panel.style.flexGrow = 1;
            panel.style.flexBasis = 0;
            panel.style.marginRight = 6;
            panel.style.paddingLeft = 6;
            panel.style.paddingRight = 6;
            panel.style.paddingTop = 6;
            panel.style.paddingBottom = 6;
            panel.style.backgroundColor = new Color(0.09f, 0.11f, 0.105f, 0.97f);

            capacity = new Label();
            capacity.style.unityFontStyleAndWeight = FontStyle.Bold;
            capacity.style.marginBottom = 4;
            panel.Add(capacity);

            var columns = new VisualElement();
            columns.style.flexDirection = FlexDirection.Row;
            columns.style.backgroundColor = new Color(0.18f, 0.22f, 0.20f, 1f);
            columns.Add(CreateColumnLabel("Name", 1));
            columns.Add(CreateColumnLabel("Qty", 0.25f));
            columns.Add(CreateColumnLabel("Enc.", 0.35f));
            panel.Add(columns);

            var list = new ListView
            {
                name = $"{name}-list",
                fixedItemHeight = 26,
                selectionType = SelectionType.Single,
                virtualizationMethod = CollectionVirtualizationMethod.FixedHeight,
                makeItem = CreateRow,
                bindItem = BindRow
            };
            list.style.flexGrow = 1;
            list.style.minHeight = 180;
            panel.Add(list);
            parent.Add(panel);
            return list;
        }

        private static VisualElement CreateRow()
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.paddingLeft = 4;
            row.style.paddingRight = 4;
            row.Add(CreateRowLabel("name", 1));
            row.Add(CreateRowLabel("quantity", 0.25f));
            row.Add(CreateRowLabel("encumbrance", 0.35f));
            return row;
        }

        private static void BindRow(VisualElement element, int index)
        {
            var list = element.GetFirstAncestorOfType<ListView>();
            var row = (InventoryRowViewModel)list.itemsSource[index];
            element.Q<Label>("name").text = row.DisplayName;
            element.Q<Label>("quantity").text = row.Quantity.ToString();
            element.Q<Label>("encumbrance").text = row.EncumbranceText;
        }

        private static Label CreateRowLabel(string name, float grow)
        {
            var label = new Label { name = name };
            label.style.flexGrow = grow;
            label.style.flexBasis = 0;
            label.style.unityTextAlign = TextAnchor.MiddleLeft;
            return label;
        }

        private static Label CreateColumnLabel(string text, float grow)
        {
            Label label = CreateRowLabel(string.Empty, grow);
            label.text = text;
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.paddingLeft = 4;
            return label;
        }

        private static Label CreateInfoLabel()
        {
            var label = new Label();
            label.style.flexGrow = 1;
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            return label;
        }

        private static Button CreateButton(string text, Action action)
        {
            var button = new Button(action) { text = text };
            button.style.marginRight = 5;
            button.style.marginTop = 3;
            button.style.paddingLeft = 9;
            button.style.paddingRight = 9;
            button.style.height = 28;
            button.style.backgroundColor = new Color(0.23f, 0.31f, 0.27f, 1f);
            button.style.color = Color.white;
            return button;
        }

        private static EquipmentSlot[] StableSlots() => new[]
        {
            EquipmentSlot.LeftHand,
            EquipmentSlot.RightHand,
            EquipmentSlot.Back
        };

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
            panel.style.left = 14;
            panel.style.right = 14;
            panel.style.top = 54;
            panel.style.bottom = 14;
            panel.style.maxWidth = 1050;
            panel.style.paddingLeft = 12;
            panel.style.paddingRight = 12;
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
