using _Saga.Code.BaseClasses;
using UnityEngine;

namespace _Saga.Code.Interactables
{
    public class TreeInteractable : BaseRegrowingHarvestable
    {
        [SerializeField] private GameObject logPrefab;
        [SerializeField] private int numberOfLogsToSpawn = 3;
        [SerializeField] private float logSpawnHeight = 0.3f;
        [SerializeField] private float logSpawnRadius = 0.5f;

        protected override void Harvest() 
        {
            base.Harvest();
            SpawnLogs();
        }
        
        private void SpawnLogs()
        {
            if (logPrefab == null)
            {
                return;
            }
            var basePosition = transform.position;
            for (var i = 0; i < numberOfLogsToSpawn; i++)
            {
                var offsetX = Random.Range(-logSpawnRadius, logSpawnRadius);
                var offsetZ = Random.Range(-logSpawnRadius, logSpawnRadius);
                var spawnPosition = basePosition + new Vector3(offsetX, logSpawnHeight * i, offsetZ);
                if (Physics.Raycast(spawnPosition + Vector3.up * 2f, Vector3.down, out RaycastHit hit, 5f))
                {
                    spawnPosition.y = hit.point.y + logSpawnHeight;
                }
                else
                {
                    spawnPosition.y = basePosition.y + logSpawnHeight;
                }
                var randomRotation = Quaternion.Euler(Random.Range(0f, 360f), Random.Range(0f, 360f), Random.Range(0f, 360f));
                Instantiate(logPrefab, spawnPosition, randomRotation);
            }
        }
    }
}