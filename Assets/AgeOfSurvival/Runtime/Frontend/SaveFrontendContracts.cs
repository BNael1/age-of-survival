using System.Collections.Generic;
using AgeOfSurvival.Runtime.Persistence;

namespace AgeOfSurvival.Runtime.Frontend
{
    public interface ISaveMainMenuActions
    {
        IReadOnlyList<SaveSlotView> SaveSlots { get; }
        bool ContinueMostRecent();
        bool StartNewGameInSlot(int slotIndex);
        bool LoadGameFromSlot(int slotIndex);
    }

    public interface ISavePauseMenuActions
    {
        string SaveStatus { get; }
        bool CanQuitWithoutSaving { get; }
        void SaveGame();
        void SaveAndReturnToMainMenu();
        void SaveAndQuit();
        void QuitWithoutSaving();
    }
}
