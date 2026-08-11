using AgeOfSurvival.Core.Inventory;

namespace AgeOfSurvival.Runtime.Inventory
{
    public sealed partial class InventoryPrototypeSession
    {
        /// <summary>
        /// Registers an already prepared ground container in the canonical Runtime
        /// collection used by streaming, rendering and timed transfers.
        /// </summary>
        public bool TryRegisterGroundContainer(GroundContainerState ground)
        {
            if (!CanRegisterGroundContainer(ground)) return false;
            _groundContainers.Add(ground);
            return true;
        }

        public bool CanRegisterGroundContainer(GroundContainerState ground)
        {
            if (ground == null || ground.Container == null || ground.IsEmpty)
                return false;

            for (int index = 0; index < _groundContainers.Count; index++)
            {
                GroundContainerState existing = _groundContainers[index];
                if (existing.Id.Equals(ground.Id)
                    || existing.Container.Id.Equals(ground.Container.Id))
                {
                    return false;
                }
            }

            return true;
        }

        public bool TryUnregisterGroundContainer(GroundContainerState ground)
        {
            return ground != null && _groundContainers.Remove(ground);
        }
    }
}
