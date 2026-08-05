using System.IO;
using UnityEngine;

namespace AgeOfSurvival.Runtime.Persistence
{
    public static class UnitySavePathProvider
    {
        public static string GetRootDirectory()
        {
            return Path.Combine(
                Application.persistentDataPath,
                "Saves");
        }
    }
}
