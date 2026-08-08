using System.Reflection;
using AgeOfSurvival.Runtime.Frontend;
using AgeOfSurvival.Runtime.Inventory;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace AgeOfSurvival.Runtime.Tests.Inventory
{
    public sealed class UiSortingOrderTests
    {
        [Test]
        public void InventoryRendersAboveHealthHudAndBelowPauseMenu()
        {
            Assert.That(
                InventoryPrototypeUiBehaviour.SortingOrder,
                Is.GreaterThan(PlayerHealthHudBehaviour.SortingOrder));
            Assert.That(
                InventoryPrototypeUiBehaviour.SortingOrder,
                Is.LessThan(PauseMenuBehaviour.SortingOrder));
        }

        [TestCase(
            typeof(InventoryPrototypeUiBehaviour),
            "Start",
            InventoryPrototypeUiBehaviour.SortingOrder)]
        [TestCase(
            typeof(PlayerHealthHudBehaviour),
            "Start",
            PlayerHealthHudBehaviour.SortingOrder)]
        [TestCase(
            typeof(PauseMenuBehaviour),
            "CreateDocument",
            PauseMenuBehaviour.SortingOrder)]
        public void GeneratedPanelUsesDeclaredSortingOrder(
            System.Type behaviourType,
            string initializerName,
            int expectedSortingOrder)
        {
            var gameObject = new GameObject(
                $"sorting-order-test-{behaviourType.Name}");
            try
            {
                var behaviour = (MonoBehaviour)gameObject.AddComponent(behaviourType);
                MethodInfo initializer = behaviourType.GetMethod(
                    initializerName,
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(initializer, Is.Not.Null);

                initializer.Invoke(behaviour, null);

                UIDocument document = gameObject.GetComponent<UIDocument>();
                Assert.That(document, Is.Not.Null);
                Assert.That(document.panelSettings, Is.Not.Null);
                Assert.That(
                    document.panelSettings.sortingOrder,
                    Is.EqualTo(expectedSortingOrder));
                Assert.That(
                    document.sortingOrder,
                    Is.EqualTo(expectedSortingOrder));
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }
    }
}
