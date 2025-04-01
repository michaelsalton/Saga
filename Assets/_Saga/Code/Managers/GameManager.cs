using UnityEngine;

namespace _Saga.Code.Managers
{
    public class GameManager : MonoBehaviour
    {
        [SerializeField] private float destroyObjectThreshold = -5f;
    
        private void Update()
        {
            DestroyObjectsBelowY();
        }

        private void DestroyObjectsBelowY()
        {
            GameObject[] destructibleObjects = GameObject.FindGameObjectsWithTag("Destructible");

            foreach (var obj in destructibleObjects)
            {
                if (obj.transform.position.y < destroyObjectThreshold)
                {
                    Destroy(obj);
                }
            }
        }
    }
}