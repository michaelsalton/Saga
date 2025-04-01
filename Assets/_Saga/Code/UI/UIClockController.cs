using _Saga.Code.DataType;
using _Saga.Code.Managers;
using TMPro;
using UnityEngine;

namespace _Saga.Code.UI
{
    public class UIClockController : MonoBehaviour
    {
        [SerializeField] private TMP_Text timeText;
        
        private void Start()
        {
            var chronoManager = ChronoManager.Instance;
            if (chronoManager == null)
            {
                return;
            }
            chronoManager.OnTimeChanged += UpdateClockDisplay;
            UpdateClockDisplay(chronoManager.GetCurrentDateTime());
        }
        
        private void OnDestroy()
        {
            var chronoManager = ChronoManager.Instance;
            if (chronoManager == null)
            {
                return;
            }
            chronoManager.OnTimeChanged -= UpdateClockDisplay;
        }
        
        private void UpdateClockDisplay(TimeData timeData)
        {
            if (timeText == null)
            {
                return;
            }
            timeText.text = $"{timeData.Year:D4}-{timeData.Month:D2}-{timeData.Day:D2} {timeData.Hour:D2}:{timeData.Minute:D2}";
        }
    }
}