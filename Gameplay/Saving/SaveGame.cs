using System;
using System.IO;
using UnityEngine;

namespace Saga.Gameplay
{
    [Serializable]
    public class SaveGame
    {
        static string FilePath => Path.Combine(Application.persistentDataPath, "save.json");

        // bump when the layout changes, so an old file can be recognised and migrated instead of misread
        public int version = 1;
        public string sceneName;
        public Vector3 playerPosition;
        // yaw only; the body never pitches, and the head's pitch isn't worth restoring
        public float playerYaw;

        // null when there's no save, or it can't be read, or its scene no longer exists
        public static SaveGame Load()
        {
            if (!File.Exists(FilePath)) return null;
            try
            {
                var save = JsonUtility.FromJson<SaveGame>(File.ReadAllText(FilePath));
                // a renamed or removed scene would make Continue a button that does nothing
                if (save == null || !Application.CanStreamedLevelBeLoaded(save.sceneName)) return null;
                return save;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[SaveGame] Couldn't read {FilePath}: {exception.Message}");
                return null;
            }
        }

        public bool Write()
        {
            string temporaryPath = FilePath + ".tmp";
            try
            {
                File.WriteAllText(temporaryPath, JsonUtility.ToJson(this, prettyPrint: true));
                // swapped in whole: a crash mid-write leaves the previous save intact instead of half a file
                if (File.Exists(FilePath)) File.Replace(temporaryPath, FilePath, null);
                else File.Move(temporaryPath, FilePath);
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[SaveGame] Couldn't write {FilePath}: {exception.Message}");
                return false;
            }
        }
    }
}
