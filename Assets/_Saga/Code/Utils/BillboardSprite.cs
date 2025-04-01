using UnityEngine;

namespace _Saga.Code.Utils
{
    public class BillboardSprite : MonoBehaviour
    {
        [SerializeField] private bool freezeXZAxis = false;
        
        private Transform _cameraTransform;

        private void Awake()
        {
            _cameraTransform = Camera.main?.transform;
        }

        private void LateUpdate()
        {
            if (_cameraTransform == null) return;
            if (freezeXZAxis)
            {
                var forward = _cameraTransform.forward;
                forward.y = 0;
                transform.rotation = Quaternion.LookRotation(forward);
            }
            else
            {
                transform.rotation = _cameraTransform.rotation;
            }
        }
    }
}