using System.Collections.Generic;
using AgeOfSurvival.Core.Crafting;
using UnityEngine;
using UnityEngine.UIElements;

namespace AgeOfSurvival.Runtime.Inventory
{
    public sealed partial class InventoryPrototypeUiDocument
    {
        private VisualElement _craftPanel;
        private ListView _craftList;
        private Label _craftDetails;
        private Label _craftStatus;
        private ProgressBar _craftProgress;
        private Button _craftButton;
        private CraftRecipeId _selectedCraftRecipeId;
        private bool _isCraftPanelOpen;

        public VisualElement CraftPanel => _craftPanel;
        public ListView CraftList => _craftList;
        public Button CraftButton => _craftButton;
        public string CraftStatusText => _craftStatus?.text ?? string.Empty;
        public bool IsCraftPanelOpen => _isCraftPanelOpen;

        public void SelectCraftForTests(CraftRecipeId recipeId)
        {
            _selectedCraftRecipeId = recipeId;
            RefreshCraftingUi();
        }

        public bool ClickCraftForTests()
        {
            if (_craftButton == null || !_craftButton.enabledSelf)
            {
                return false;
            }

            CraftSelected();
            return true;
        }

        public void ToggleCraftPanel()
        {
            bool shouldOpen = !_isCraftPanelOpen;
            if (shouldOpen)
            {
                SetPanelOpen(false);
            }

            SetCraftPanelOpen(shouldOpen);
        }

        private void InitializeCraftingUi()
        {
            var toggle = CreateButton("Craft prototype", ToggleCraftPanel);
            toggle.name = "craft-toggle";
            toggle.style.position = Position.Absolute;
            toggle.style.left = 180;
            toggle.style.top = 14;
            _root.Add(toggle);

            _craftPanel = new VisualElement { name = "craft-panel" };
            ConfigurePanel(_craftPanel);
            _craftPanel.style.maxWidth = 820;
            _root.Add(_craftPanel);
            SetCraftPanelOpen(false);

            var title = new Label("CRAFT — HAND");
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.fontSize = 18;
            title.style.marginBottom = 8;
            _craftPanel.Add(title);

            _craftStatus = new Label { name = "craft-status" };
            _craftStatus.style.unityFontStyleAndWeight = FontStyle.Bold;
            _craftStatus.style.marginBottom = 4;
            _craftPanel.Add(_craftStatus);

            _craftProgress = new ProgressBar
            {
                name = "craft-progress",
                title = "Craft",
                lowValue = 0f,
                highValue = 100f
            };
            _craftProgress.style.marginBottom = 10f;
            _craftPanel.Add(_craftProgress);

            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.flexGrow = 1f;

            _craftList = new ListView
            {
                name = "craft-recipe-list",
                fixedItemHeight = 30,
                selectionType = SelectionType.Single,
                virtualizationMethod = CollectionVirtualizationMethod.FixedHeight,
                makeItem = () => new Label(),
                bindItem = BindCraftRow
            };
            _craftList.style.flexGrow = 1f;
            _craftList.style.flexBasis = 0f;
            _craftList.style.minHeight = 180f;
            _craftList.selectionChanged += selectedItems =>
            {
                CraftRecipeRowViewModel selected = null;
                foreach (object item in selectedItems)
                {
                    selected = item as CraftRecipeRowViewModel;
                    break;
                }

                if (selected == null)
                {
                    return;
                }

                _selectedCraftRecipeId = selected.Id;
                RefreshCraftSelectionControls();
            };
            row.Add(_craftList);

            var detailColumn = new VisualElement();
            detailColumn.style.flexGrow = 1.35f;
            detailColumn.style.flexBasis = 0f;
            detailColumn.style.marginLeft = 12f;
            detailColumn.style.paddingLeft = 10f;
            detailColumn.style.paddingRight = 10f;
            detailColumn.style.paddingTop = 8f;
            detailColumn.style.paddingBottom = 8f;
            detailColumn.style.backgroundColor = new Color(0.09f, 0.11f, 0.105f, 0.97f);

            var detailsTitle = new Label("Recipe details");
            detailsTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            detailsTitle.style.marginBottom = 8f;
            detailColumn.Add(detailsTitle);

            _craftDetails = new Label
            {
                name = "craft-details",
                text = "Select a recipe"
            };
            _craftDetails.style.whiteSpace = WhiteSpace.Normal;
            _craftDetails.style.flexGrow = 1f;
            detailColumn.Add(_craftDetails);

            _craftButton = CreateButton("Craft selected", CraftSelected);
            _craftButton.name = "craft-selected";
            detailColumn.Add(_craftButton);
            row.Add(detailColumn);

            _craftPanel.Add(row);

            var footer = new VisualElement();
            footer.style.flexDirection = FlexDirection.Row;
            footer.style.marginTop = 10f;
            footer.Add(CreateButton("Close", ToggleCraftPanel));
            _craftPanel.Add(footer);
        }

        private void RefreshCraftingUi()
        {
            if (_craftList == null)
            {
                return;
            }

            CraftPrototypeViewModel view = CraftPrototypeViewModelBuilder.Build(_session);
            _craftStatus.text = view.StatusText;
            _craftProgress.value = (float)(view.Progress * 100.0);

            var rows = new List<CraftRecipeRowViewModel>(view.Recipes);
            _craftList.itemsSource = rows;
            _craftList.Rebuild();

            RefreshCraftSelectionControls();
        }

        private void RefreshCraftSelectionControls()
        {
            CraftRecipeRowViewModel selected = null;
            if (_craftList?.itemsSource != null)
            {
                for (int i = 0; i < _craftList.itemsSource.Count; i++)
                {
                    var row = _craftList.itemsSource[i] as CraftRecipeRowViewModel;
                    if (row != null && row.Id.Equals(_selectedCraftRecipeId))
                    {
                        selected = row;
                        break;
                    }
                }
            }

            _craftDetails.text = selected == null
                ? "Select a recipe"
                : selected.DetailsText;
            _craftButton.SetEnabled(selected != null && selected.CanCraft);
        }

        private void CraftSelected()
        {
            if (!_selectedCraftRecipeId.IsValid)
            {
                return;
            }

            _session.StartCraft(
                _selectedCraftRecipeId,
                _session.CurrentTick);
            RefreshCraftingUi();
            RefreshButtons();
        }

        private void BindCraftRow(VisualElement element, int index)
        {
            var label = (Label)element;
            var row = (CraftRecipeRowViewModel)_craftList.itemsSource[index];
            label.text = row.DisplayText;
        }

        private void SetCraftPanelOpen(bool isOpen)
        {
            _isCraftPanelOpen = isOpen;
            _craftPanel.style.display = isOpen
                ? DisplayStyle.Flex
                : DisplayStyle.None;

            if (isOpen)
            {
                RefreshCraftingUi();
            }
        }
    }
}
