using System.Collections.Generic;
using System.Linq;
using AgeOfSurvival.Core.Characters;
using AgeOfSurvival.Core.Resources;
using NUnit.Framework;

namespace AgeOfSurvival.Core.Tests
{
    public sealed class ResourceInteractionTests
    {
        private static readonly WorldPosition PlayerPosition = new WorldPosition(4.5, 4.5);

        [Test]
        public void AvailableResourceInsideRadiusIsFound()
        {
            ResourceState resource = CreateResource("resource-a", 5.0, 4.5);

            ResourceState result = ResourceTargeting.FindNearestAvailable(
                new[] { resource },
                PlayerPosition,
                1.5);

            Assert.That(result, Is.SameAs(resource));
        }

        [Test]
        public void NearestAvailableResourceIsSelected()
        {
            ResourceState farther = CreateResource("resource-a", 5.5, 4.5);
            ResourceState nearer = CreateResource("resource-b", 4.75, 4.5);

            ResourceState result = ResourceTargeting.FindNearestAvailable(
                new[] { farther, nearer },
                PlayerPosition,
                1.5);

            Assert.That(result, Is.SameAs(nearer));
        }

        [Test]
        public void ResourceOutsideRadiusIsIgnored()
        {
            ResourceState result = ResourceTargeting.FindNearestAvailable(
                new[] { CreateResource("resource-a", 6.01, 4.5) },
                PlayerPosition,
                1.5);

            Assert.That(result, Is.Null);
        }

        [Test]
        public void ExactRadiusBoundaryIsIncluded()
        {
            ResourceState resource = CreateResource("resource-a", 6.0, 4.5);

            ResourceState result = ResourceTargeting.FindNearestAvailable(
                new[] { resource },
                PlayerPosition,
                1.5);

            Assert.That(result, Is.SameAs(resource));
        }

        [Test]
        public void HarvestedResourceIsIgnored()
        {
            ResourceState harvested = CreateResource(
                "resource-a",
                4.75,
                4.5,
                ResourceAvailability.Harvested);

            ResourceState result = ResourceTargeting.FindNearestAvailable(
                new[] { harvested },
                PlayerPosition,
                1.5);

            Assert.That(result, Is.Null);
        }

        [Test]
        public void EqualDistanceUsesStableIdentifierAsTieBreaker()
        {
            ResourceState laterId = CreateResource("resource-b", 5.0, 4.5);
            ResourceState earlierId = CreateResource("resource-a", 4.0, 4.5);

            ResourceState result = ResourceTargeting.FindNearestAvailable(
                new[] { laterId, earlierId },
                PlayerPosition,
                1.5);

            Assert.That(result, Is.SameAs(earlierId));
        }

        [Test]
        public void ValidInteractionHarvestsSelectedResource()
        {
            ResourceState resource = CreateResource("resource-a", 5.0, 4.5);

            ResourceInteractionResult result = Apply(new[] { resource });

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.HarvestedResourceId, Is.EqualTo(resource.Id));
            Assert.That(resource.Availability, Is.EqualTo(ResourceAvailability.Harvested));
        }

        [Test]
        public void InteractionWithoutTargetChangesNothing()
        {
            ResourceState outside = CreateResource("resource-a", 7.0, 4.5);

            ResourceInteractionResult result = Apply(new[] { outside });

            Assert.That(result.Outcome, Is.EqualTo(ResourceInteractionOutcome.NoAvailableTarget));
            Assert.That(outside.Availability, Is.EqualTo(ResourceAvailability.Available));

            ResourceInteractionResult defaultResult = default(ResourceInteractionResult);
            Assert.That(
                defaultResult.Outcome,
                Is.EqualTo(ResourceInteractionOutcome.NoAvailableTarget));
            Assert.That(defaultResult.Succeeded, Is.False);
        }

        [Test]
        public void RepeatedInteractionDoesNotHarvestSameResourceTwice()
        {
            ResourceState resource = CreateResource("resource-a", 5.0, 4.5);
            IReadOnlyList<ResourceState> resources = new[] { resource };

            ResourceInteractionResult first = Apply(resources);
            ResourceInteractionResult second = Apply(resources);

            Assert.That(first.Succeeded, Is.True);
            Assert.That(second.Succeeded, Is.False);
            Assert.That(resource.Availability, Is.EqualTo(ResourceAvailability.Harvested));
        }

        [Test]
        public void OneCommandHarvestsExactlyOneResource()
        {
            var resources = new[]
            {
                CreateResource("resource-a", 5.0, 4.5),
                CreateResource("resource-b", 4.0, 4.5),
                CreateResource("resource-c", 4.5, 5.0)
            };

            Apply(resources);

            Assert.That(
                resources.Count(resource =>
                    resource.Availability == ResourceAvailability.Harvested),
                Is.EqualTo(1));
        }

        [Test]
        public void StableIdentifierUsesOrdinalValueEqualityAndOrdering()
        {
            var first = new ResourceId("resource-a");
            var same = new ResourceId("resource-a");
            var later = new ResourceId("resource-b");

            Assert.That(first, Is.EqualTo(same));
            Assert.That(first.CompareTo(later), Is.LessThan(0));

            ResourceId defaultId = default(ResourceId);
            Assert.That(defaultId.IsValid, Is.False);
            Assert.That(defaultId.GetHashCode(), Is.EqualTo(0));
            Assert.That(defaultId.ToString(), Is.EqualTo(string.Empty));
            Assert.That(
                () => new ResourceState(defaultId, PlayerPosition),
                Throws.ArgumentException);
        }

        private static ResourceInteractionResult Apply(
            IReadOnlyList<ResourceState> resources)
        {
            return ResourceInteraction.Apply(
                new ResourceInteractionCommand(),
                resources,
                PlayerPosition,
                1.5);
        }

        private static ResourceState CreateResource(
            string id,
            double x,
            double y,
            ResourceAvailability availability = ResourceAvailability.Available)
        {
            return new ResourceState(
                new ResourceId(id),
                new WorldPosition(x, y),
                availability);
        }
    }
}
