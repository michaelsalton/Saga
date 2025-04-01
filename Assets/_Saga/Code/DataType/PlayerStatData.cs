using UnityEngine;

namespace _Saga.Code.DataType
{
    [System.Serializable]
    public class PlayerStatData
    {
        [SerializeField] private float current;
        [SerializeField] private float max;
        [SerializeField] private float perSecondTick;

        public PlayerStatData(float current, float max, float perSecondTick)
        {
            this.current = current;
            this.max = max;
            this.perSecondTick = perSecondTick;
        }

        public void TickStat(float deltaTime)
        {
            current = Mathf.Clamp(current + perSecondTick * deltaTime, 0.0f, max);
        }

        public void Adjust(float amount)
        {
            current = Mathf.Clamp(current + amount, 0.0f, max);
        }

        public float Percentile()
        {
            return current / max;
        }

        public void AdjustTick(float tickRate)
        {
            perSecondTick = tickRate;
        }
        
        public float GetCurrent()
        {
            return current;
        }
    }
}