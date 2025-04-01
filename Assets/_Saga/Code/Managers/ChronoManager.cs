using System;
using _Saga.Code.DataType;
using UnityEngine;

namespace _Saga.Code.Managers
{
    public class ChronoManager : MonoBehaviour
    {
        public static ChronoManager Instance  { get; private set; }

        [Header("Time Settings")]
        [SerializeField] private bool useDayNightCycle = true;
        [SerializeField] private float dayLengthInMinutes = 10f;
        
        [Header("Initial Date and Time")]
        [SerializeField] private int initialYear = 2025;
        [SerializeField] private int initialMonth = 3;
        [SerializeField] private int initialDay = 28;
        [SerializeField] private int initialHour = 14;
        [SerializeField] private int initialMinute = 41;

        private TimeData _currentTime;
        private TimeOfDay _currentTimeOfDay;
        private float _timeDecay;
        private float _minuteLength;
        
        public event Action<TimeData> OnTimeChanged;
        public event Action<TimeOfDay> OnTimeOfDayChanged;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
        }
        
        private void Start()
        {
            _currentTime = new TimeData(initialYear, initialMonth, initialDay, initialHour, initialMinute);
            CalculateDayLength();
            InvokeRepeating(nameof(AdvanceMinute), _minuteLength, _minuteLength);
        }

        private void Update()
        {
            if (!useDayNightCycle)
            {
                return;
            }
            UpdateTime(Time.deltaTime);
        }

        private void UpdateTime(float deltaTime)
        {
            _timeDecay -= deltaTime;
            if (_timeDecay > 0)
            {
                return;
            }
            _timeDecay += _minuteLength;
            AdvanceMinute();
            OnTimeChanged?.Invoke(_currentTime);
            UpdateDayNightCycle();
        }
        
        private void UpdateDayNightCycle()
        {
            var newTimeOfDay = GetTimeOfDay(_currentTime.Hour);
            if (_currentTimeOfDay == newTimeOfDay)
            {
                return;
            }
            _currentTimeOfDay = newTimeOfDay;
            OnTimeOfDayChanged?.Invoke(_currentTimeOfDay);
        }
        
        private TimeOfDay GetTimeOfDay(int hour)
        {
            return hour switch
            {
                >= 5 and < 9  => TimeOfDay.Morning,
                >= 9 and < 16 => TimeOfDay.Midday,
                >= 16 and < 19 => TimeOfDay.Afternoon,
                >= 19 and < 22 => TimeOfDay.Evening,
                _ => TimeOfDay.Night
            };
        }
        
        private void AdvanceMinute()
        {
            _currentTime.Minute++;
            if (_currentTime.Minute < 60)
            {
                return;
            }
            _currentTime.Minute = 0;
            AdvanceHour();
        }

        private void AdvanceHour()
        {
            _currentTime.Hour++;
            if (_currentTime.Hour < 24)
            {
                return;
            }
            _currentTime.Hour = 0;
            AdvanceDay();
        }

        private void AdvanceDay()
        {
            _currentTime.Day++;
            if (_currentTime.Day > DateTime.DaysInMonth(_currentTime.Year, _currentTime.Month))
            {
                _currentTime.Day = 1;
                AdvanceMonth();
            }
            _currentTime.DayOfYear = TimeData.CalculateDayOfYear(_currentTime.Year, _currentTime.Month, _currentTime.Day);
        }

        private void AdvanceMonth()
        {
            _currentTime.Month++;
            if (_currentTime.Month > 12)
            {
                _currentTime.Month = 1;
                AdvanceYear();
            }
            _currentTime.DayOfYear = TimeData.CalculateDayOfYear(_currentTime.Year, _currentTime.Month, _currentTime.Day);
        }

        private void AdvanceYear()
        {
            _currentTime.Year++;
            _currentTime.DayOfYear = TimeData.CalculateDayOfYear(_currentTime.Year, _currentTime.Month, _currentTime.Day);
            OnTimeChanged?.Invoke(_currentTime);
        }
        
        private void CalculateDayLength()
        {
            _minuteLength = dayLengthInMinutes * 60f / 1440f;
            _timeDecay = _minuteLength;
        }

        public TimeData GetCurrentDateTime()
        {
            return _currentTime;
        }
    }
}