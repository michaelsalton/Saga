using UnityEngine;

namespace Saga.Rendering
{
    // publishes one world-space sphere that grass blades bend away from; put it on whatever walks through the field
    [ExecuteAlways]
    [AddComponentMenu("Saga/Rendering/Grass Interactor")]
    [DisallowMultipleComponent]
    public class SagaGrassInteractor : MonoBehaviour
    {
        [Tooltip("Metres around this transform's position that blades bend away from.")]
        [Range(0.1f, 3f)]
        [SerializeField] float radius = 0.6f;

        [Tooltip("Push at the centre, in blade heights. Around 2 lays blades nearly flat.")]
        [Range(0f, 4f)]
        [SerializeField] float strength = 2f;

        static readonly int PositionRadiusID = Shader.PropertyToID("_GrassInteractorPositionRadius");
        static readonly int StrengthID = Shader.PropertyToID("_GrassInteractorStrength");

        // zero strength leaves every blade where the wind put it
        void OnDisable() => Shader.SetGlobalFloat(StrengthID, 0f);

        // after every Update, so the motor has already moved us this frame
        void LateUpdate()
        {
            Vector3 position = transform.position;
            Shader.SetGlobalVector(PositionRadiusID, new Vector4(position.x, position.y, position.z, radius));
            Shader.SetGlobalFloat(StrengthID, strength);
        }
    }
}
