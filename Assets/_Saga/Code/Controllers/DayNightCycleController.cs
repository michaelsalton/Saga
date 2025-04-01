using _Saga.Code.DataType;
using _Saga.Code.Managers;
using UnityEngine;

namespace _Saga.Code.Controllers
{
    public class DayNightCycleController : MonoBehaviour
    {
        [SerializeField] private Light mainLight;

        private void Start()
        {
            ChronoManager.Instance.OnTimeOfDayChanged += HandleTimeOfDayChanged;
        }

        private void OnDestroy()
        {
            if (ChronoManager.Instance == null)
            {
                return;
            }

            ChronoManager.Instance.OnTimeOfDayChanged -= HandleTimeOfDayChanged;
        }
        
        private void HandleTimeOfDayChanged(TimeOfDay timeOfDay)
        {
            switch (timeOfDay)
            {
                case TimeOfDay.Morning:
                    SetMorningLighting();
                    break;
                case TimeOfDay.Midday:
                    SetMiddayLighting();
                    break;
                case TimeOfDay.Afternoon:
                    SetAfternoonLighting();
                    break;
                case TimeOfDay.Evening:
                    SetEveningLighting();
                    break;
                case TimeOfDay.Night:
                    SetNightLighting();
                    break;
            }
        }
        
        private void SetMorningLighting()
        {
            SetLightingParameters(130000, 4500);
        }

        private void SetMiddayLighting()
        {
            SetLightingParameters(130000, 6000);
        }

        private void SetAfternoonLighting()
        {
            SetLightingParameters(70000, 5300);
        }

        private void SetEveningLighting()
        {
            SetLightingParameters(50000, 3200);
        }

        private void SetNightLighting()
        {
            SetLightingParameters(2, 20000);
        }

        private void SetLightingParameters(float intensity, float temperature)
        {
            mainLight.intensity = intensity;
            mainLight.colorTemperature = temperature;
        }
    }
}