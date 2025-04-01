using _Saga.Code.Components;
using _Saga.Code.DataType;
using UnityEngine;
using UnityEngine.UI;

namespace _Saga.Code.UI
{
    public class UIStatBarController : MonoBehaviour
    {
        [Header("Statline Component (Player Object)")] 
        [SerializeField] private StatlineComponent statline;

        [Header("Stat Bars")] 
        [SerializeField] private Slider healthSlider;
        [SerializeField] private Slider staminaSlider;
        [SerializeField] private Slider hungerSlider;
        [SerializeField] private Slider thirstSlider;

        private void OnEnable()
        {
            statline.OnStatChanged += HandleStatChange;
        }

        private void Disable()
        {
            statline.OnStatChanged -= HandleStatChange;
        }

        private void HandleStatChange(PlayerStat stat, float value)
        {
            switch (stat)
            {
                case PlayerStat.Health:
                    healthSlider.value =  statline.GetStat(stat).GetCurrent();
                    return;
                case PlayerStat.Stamina:
                    staminaSlider.value = statline.GetStat(stat).GetCurrent();
                    return;
                case PlayerStat.Hunger:
                    hungerSlider.value = statline.GetStat(stat).GetCurrent();
                    return;
                case PlayerStat.Thirst:
                    thirstSlider.value = statline.GetStat(stat).GetCurrent();
                    return;
                default:
                    break;
            }
        }
    }
}
