using UnityEngine;

namespace AgeOfSurvival.Runtime.Frontend
{
    /// <summary>
    /// Process-local gate for physical gameplay input and fixed-tick advancement.
    /// Frontend overlays own the gate; the pure simulation remains unaware of menus.
    /// </summary>
    public static class GameplayInputGate
    {
        public static bool IsBlocked { get; private set; }

        public static void SetBlocked(bool blocked)
        {
            IsBlocked = blocked;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetForPlayMode()
        {
            IsBlocked = false;
        }
    }
}
