using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace AgeOfSurvival.Runtime.Frontend
{
    internal static class FrontendStyles
    {
        private static readonly Color TextPrimary =
            new Color(0.92f, 0.90f, 0.82f, 1f);
        private static readonly Color TextMuted =
            new Color(0.65f, 0.66f, 0.60f, 1f);
        private static readonly Color Accent =
            new Color(0.69f, 0.52f, 0.25f, 1f);
        private static readonly Color Panel =
            new Color(0.035f, 0.045f, 0.04f, 0.94f);
        private static readonly Color Button =
            new Color(0.07f, 0.085f, 0.075f, 0.96f);

        public static void ConfigureRoot(VisualElement root)
        {
            root.Clear();
            root.style.position = Position.Absolute;
            root.style.left = 0;
            root.style.right = 0;
            root.style.top = 0;
            root.style.bottom = 0;
            root.style.color = TextPrimary;
            root.pickingMode = PickingMode.Position;
        }

        public static VisualElement CreateBackdrop(
            VisualElement root,
            float opacity)
        {
            var backdrop = new VisualElement { name = "frontend-backdrop" };
            Fill(backdrop);
            backdrop.style.backgroundColor =
                new Color(0.01f, 0.015f, 0.012f, opacity);
            backdrop.pickingMode = PickingMode.Position;
            root.Add(backdrop);
            return backdrop;
        }

        public static VisualElement CreateLeftShell(
            VisualElement root,
            string name)
        {
            var shell = new VisualElement { name = name };
            shell.style.position = Position.Absolute;
            shell.style.left = 42;
            shell.style.top = 36;
            shell.style.bottom = 36;
            shell.style.width = 390;
            shell.style.paddingLeft = 24;
            shell.style.paddingRight = 24;
            shell.style.paddingTop = 20;
            shell.style.paddingBottom = 20;
            shell.style.backgroundColor = Panel;
            shell.style.borderLeftWidth = 2;
            shell.style.borderLeftColor = Accent;
            shell.pickingMode = PickingMode.Position;
            root.Add(shell);
            return shell;
        }

        public static VisualElement CreateCenteredShell(
            VisualElement root,
            string name)
        {
            var shell = new VisualElement { name = name };
            shell.style.position = Position.Absolute;
            shell.style.width = 420;
            shell.style.top = Length.Percent(16);
            shell.style.left = Length.Percent(50);
            shell.style.marginLeft = -210;
            shell.style.paddingLeft = 26;
            shell.style.paddingRight = 26;
            shell.style.paddingTop = 24;
            shell.style.paddingBottom = 24;
            shell.style.backgroundColor = Panel;
            shell.style.borderTopWidth = 1;
            shell.style.borderRightWidth = 1;
            shell.style.borderBottomWidth = 1;
            shell.style.borderLeftWidth = 3;
            shell.style.borderTopColor = Accent;
            shell.style.borderRightColor = Accent;
            shell.style.borderBottomColor = Accent;
            shell.style.borderLeftColor = Accent;
            shell.pickingMode = PickingMode.Position;
            root.Add(shell);
            return shell;
        }

        public static Label CreateGameTitle()
        {
            var title = new Label("AGE OF SURVIVAL")
            {
                name = "game-title"
            };
            title.style.fontSize = 34;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.color = TextPrimary;
            title.style.marginBottom = 3;
            return title;
        }

        public static Label CreateSectionTitle(string text)
        {
            var title = new Label(text);
            title.style.fontSize = 21;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.color = TextPrimary;
            title.style.marginBottom = 12;
            return title;
        }

        public static Label CreateMutedLabel(string text)
        {
            var label = new Label(text);
            label.style.color = TextMuted;
            label.style.whiteSpace = WhiteSpace.Normal;
            label.style.marginBottom = 12;
            return label;
        }

        public static Button CreateMenuButton(
            string name,
            string text,
            Action action)
        {
            var button = new Button(action)
            {
                name = name,
                text = text
            };
            button.style.height = 42;
            button.style.marginTop = 2;
            button.style.marginBottom = 5;
            button.style.paddingLeft = 15;
            button.style.paddingRight = 12;
            button.style.unityTextAlign = TextAnchor.MiddleLeft;
            button.style.unityFontStyleAndWeight = FontStyle.Bold;
            button.style.fontSize = 17;
            button.style.color = TextPrimary;
            button.style.backgroundColor = Button;
            button.style.borderLeftWidth = 3;
            button.style.borderLeftColor = Accent;
            return button;
        }

        public static VisualElement CreatePanel(string name)
        {
            var panel = new VisualElement { name = name };
            panel.style.flexGrow = 1;
            panel.style.marginTop = 20;
            return panel;
        }

        public static void ShowOnly(
            VisualElement visible,
            params VisualElement[] panels)
        {
            for (int index = 0; index < panels.Length; index++)
            {
                panels[index].style.display =
                    ReferenceEquals(panels[index], visible)
                        ? DisplayStyle.Flex
                        : DisplayStyle.None;
            }
        }

        private static void Fill(VisualElement element)
        {
            element.style.position = Position.Absolute;
            element.style.left = 0;
            element.style.right = 0;
            element.style.top = 0;
            element.style.bottom = 0;
        }
    }
}
