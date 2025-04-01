using _Saga.Code.DataType;
using _Saga.Code.Managers;
using UnityEngine;

namespace _Saga.Code.BaseClasses
{
    public class BaseRegrowingHarvestable : BaseHarvestable
    {
        [SerializeField] private float daysToRegrow = 1f;

        private TimeData _dayOfLastHarvest;
        
        public float DaysToRegrow { get => daysToRegrow; set => daysToRegrow = value; }
        public TimeData DayOfLastHarvest { get => _dayOfLastHarvest; set => _dayOfLastHarvest = value; }
        
        private void Start()
        {
            DayOfLastHarvest = ChronoManager.Instance.GetCurrentDateTime();
            base.Start();
        }

        private void Update()
        {
            if (!IsHarvested)
            {
                return;
            }
            var currentDate = ChronoManager.Instance.GetCurrentDateTime();
            if (IsTimeToRegrow(currentDate))
            {
                Regrow();
            }
        }
        
        protected override void Harvest()
        {
            Debug.Log("Harvest A");
            DayOfLastHarvest = ChronoManager.Instance.GetCurrentDateTime();
            base.Harvest();
        }
        
        private bool IsTimeToRegrow(TimeData currentDate)
        {
            if (currentDate.Year > _dayOfLastHarvest.Year)
            {
                return true;
            }
            if (currentDate.Year != _dayOfLastHarvest.Year)
            {
                return false;
            }
            return currentDate.DayOfYear > _dayOfLastHarvest.DayOfYear;;
        }
        
        private void Regrow()
        {
            IsHarvested = false;
            InitializeText();
            SetHarvestState();
        }
    }
}
    
