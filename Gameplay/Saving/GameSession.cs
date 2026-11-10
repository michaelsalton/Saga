using UnityEngine;
using UnityEngine.SceneManagement;

namespace Saga.Gameplay
{
    public static class GameSession
    {
        // a scene load destroys every object, so the save being loaded rides across in a static
        static SaveGame pendingLoad;

        // with domain reload off in Enter Play Mode settings, statics survive from the last play session
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => pendingLoad = null;

        public static void NewGame(string sceneName)
        {
            pendingLoad = null;
            SceneManager.LoadSceneAsync(sceneName);
        }

        public static void Continue(SaveGame save)
        {
            pendingLoad = save;
            SceneManager.LoadSceneAsync(save.sceneName);
        }

        // cleared on read, so reloading the scene later starts fresh instead of re-applying an old save
        public static SaveGame TakePendingLoad()
        {
            var save = pendingLoad;
            pendingLoad = null;
            return save;
        }
    }
}
