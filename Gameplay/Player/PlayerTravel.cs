using System;
using System.Collections;
using UnityEngine;

namespace Saga.Gameplay
{
    [RequireComponent(typeof(PlayerMotor))]
    [DisallowMultipleComponent]
    public class PlayerTravel : MonoBehaviour
    {
        [Range(0.05f, 2f)]
        [SerializeField] float fadeDuration = 0.35f;
        // a beat of full black so the cut never shows, including the camera's one-frame lag behind the teleport
        [Range(0f, 1f)]
        [SerializeField] float holdDuration = 0.1f;

        // 0 is clear, 1 is fully black; the HUD reads this, gameplay never touches the UI
        public float Blackness { get; private set; }
        public bool IsTraveling { get; private set; }
        // raised at the destination while the screen is still black
        public event Action Arrived;

        PlayerMotor motor;
        bool motorWasEnabled;

        void Awake() => motor = GetComponent<PlayerMotor>();

        void OnDisable()
        {
            // deactivating the object kills the coroutine mid-fade; without this the screen stays black
            if (!IsTraveling) return;
            StopAllCoroutines();
            Finish();
        }

        public void Travel(Vector3 position, Quaternion rotation) => Travel(null, () => new Pose(position, rotation));

        // the destination is resolved after whileBlack finishes, since it may live in a scene that isn't loaded yet
        public void Travel(IEnumerator whileBlack, Func<Pose?> findDestination)
        {
            if (IsTraveling) return;
            StartCoroutine(TravelRoutine(whileBlack, findDestination));
        }

        IEnumerator TravelRoutine(IEnumerator whileBlack, Func<Pose?> findDestination)
        {
            IsTraveling = true;
            motorWasEnabled = motor.enabled;
            // frozen so you can't walk away from the door while the screen goes dark
            motor.enabled = false;
            yield return Fade(0f, 1f);
            // runs to completion as a nested coroutine, hidden behind the black
            if (whileBlack != null) yield return whileBlack;
            Pose? destination = findDestination?.Invoke();
            if (destination.HasValue) motor.Teleport(destination.Value.position, destination.Value.rotation);
            Arrived?.Invoke();
            // scaled time, so pausing during a transition freezes it instead of finishing behind the menu
            yield return new WaitForSeconds(holdDuration);
            motor.enabled = motorWasEnabled;
            yield return Fade(1f, 0f);
            Finish();
        }

        IEnumerator Fade(float from, float to)
        {
            for (float elapsed = 0f; elapsed < fadeDuration; elapsed += Time.deltaTime)
            {
                // eases at both ends; a linear ramp visibly kicks in and stops dead
                Blackness = Mathf.SmoothStep(from, to, elapsed / fadeDuration);
                yield return null;
            }
            Blackness = to;
        }

        void Finish()
        {
            motor.enabled = motorWasEnabled;
            Blackness = 0f;
            IsTraveling = false;
        }
    }
}
