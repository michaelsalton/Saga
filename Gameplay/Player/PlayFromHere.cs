#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Saga.Gameplay
{
    // Saga > Play From Here: enters play mode with the player standing under the Scene view camera
    public static class PlayFromHere
    {
        const string ActiveKey = "Saga.PlayFromHere.Active";
        const string PositionKey = "Saga.PlayFromHere.Position";
        const string YawKey = "Saga.PlayFromHere.Yaw";

        // long enough to reach the ground from a high overview shot
        const float MaxDropDistance = 1000f;

        [MenuItem("Saga/Play From Here")]
        static void Begin()
        {
            SceneView sceneView = SceneView.lastActiveSceneView;
            if (sceneView == null)
            {
                Debug.LogWarning("Play From Here: no Scene view open.");
                return;
            }

            Transform cameraTransform = sceneView.camera.transform;
            // entering play reloads the script domain and wipes statics; SessionState lives in the editor process and survives it
            SessionState.SetVector3(PositionKey, cameraTransform.position);
            SessionState.SetFloat(YawKey, cameraTransform.eulerAngles.y);
            SessionState.SetBool(ActiveKey, true);
            EditorApplication.EnterPlaymode();
        }

        [MenuItem("Saga/Play From Here", true)]
        static bool CanBegin() => !EditorApplication.isPlayingOrWillChangePlaymode;

        // after every Awake, so the motor already has its CharacterController, and before any Start or first frame
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Apply()
        {
            if (!SessionState.GetBool(ActiveKey, false)) return;
            // one-shot, so the next ordinary Play starts at the spawn point
            SessionState.EraseBool(ActiveKey);

            PlayerMotor player = Object.FindAnyObjectByType<PlayerMotor>();
            if (player == null)
            {
                Debug.LogWarning("Play From Here: no PlayerMotor in the loaded scene.");
                return;
            }

            Vector3 cameraPosition = SessionState.GetVector3(PositionKey, Vector3.zero);
            if (!TryFindFloor(cameraPosition, player.transform, out Vector3 feetPosition))
            {
                Debug.LogWarning("Play From Here: nothing to stand on below the Scene view camera; starting at the spawn point.");
                return;
            }

            // yaw only: pitch belongs to FirstPersonLook, which levels the view when it enables
            float yaw = SessionState.GetFloat(YawKey, 0f);
            player.Teleport(feetPosition, Quaternion.Euler(0f, yaw, 0f));
        }

        static bool TryFindFloor(Vector3 origin, Transform player, out Vector3 floorPoint)
        {
            floorPoint = default;
            float nearestDistance = float.MaxValue;
            bool found = false;

            RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, MaxDropDistance, ~0, QueryTriggerInteraction.Ignore);
            foreach (RaycastHit hit in hits)
            {
                // the player's own capsule may be standing under the camera
                if (hit.collider.transform.IsChildOf(player)) continue;
                // RaycastAll returns hits in no particular order
                if (hit.distance >= nearestDistance) continue;
                nearestDistance = hit.distance;
                floorPoint = hit.point;
                found = true;
            }
            return found;
        }
    }
}
#endif
