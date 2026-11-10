using UnityEngine;

namespace Saga.Gameplay
{
    [DisallowMultipleComponent]
    public class SaveController : MonoBehaviour
    {
        [SerializeField] PlayerMotor player;
        [SerializeField] PlayerTravel travel;

        // Start, not Awake: the player's own Awake has to have run before it can be moved
        void Start()
        {
            // null when the scene was opened directly in the editor or via New Game; the player stays where it was placed
            SaveGame pending = GameSession.TakePendingLoad();
            if (pending != null) player.Teleport(pending.playerPosition, Quaternion.Euler(0f, pending.playerYaw, 0f));
        }

        void OnEnable() => travel.Arrived += OnArrived;

        void OnDisable() => travel.Arrived -= OnArrived;

        // door destinations are hand-placed safe spots, so arriving is always a good moment to save
        void OnArrived() => Save();

        public bool Save()
        {
            var save = new SaveGame
            {
                sceneName = gameObject.scene.name,
                playerPosition = player.transform.position,
                playerYaw = player.transform.eulerAngles.y,
            };
            return save.Write();
        }
    }
}
