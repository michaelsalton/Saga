using UnityEngine;
using System.Collections.Generic;
using _Saga.Code.DataType;

namespace _Saga.Code.Components
{
    public class StatlineComponent : MonoBehaviour
    {
        [System.Serializable]
        public class StatEntry
        {
            [SerializeField] public PlayerStat statType;
            [SerializeField] public float current = 100;
            [SerializeField] public float max = 100;
            [SerializeField] public float perSecondTick = 0;
        }
        
        public delegate void StatChangedHandler(PlayerStat statType, float newValue);
        public event StatChangedHandler OnStatChanged;

        [Header("Stats")]
        [SerializeField] private List<StatEntry> statEntries = new List<StatEntry>();
        
        [Header("Status")]
        [SerializeField] private float sprintCostMultiplier = 2;
        [SerializeField] private float walkSpeed = 10;
        [SerializeField] private float sprintSpeed = 15;
        [SerializeField] private float sneakSpeed = 7;
        [SerializeField] private float jumpCost = 7;
        [SerializeField] private float jumpHeight = 0.25f;
        [SerializeField] private float secondsForStaminaExhaustion = 5;
        [SerializeField] private float starvingDamagePerSecond = 1;
        [SerializeField] private float dehydratedDamagePerSecond = 1;
        
        private CharacterController _characterController;
        private Dictionary<PlayerStat, PlayerStatData> _stats = new Dictionary<PlayerStat, PlayerStatData>();
        private float _currentStaminaExhaustion = 0;
        private bool _isSprinting = false;
        private bool _isSneaking = false;
        
        private void Awake()
        {
            _characterController = GetComponent<CharacterController>();
            foreach (var entry in statEntries)
            {
                _stats[entry.statType] = new PlayerStatData(entry.current, entry.max, entry.perSecondTick);
            }
        }

        private void Update()
        {
            TickStamina();
            TickHunger();
            TickThirst();
            TickHealth();
        }

        private void TickStamina()
        {
            var stamina = GetStat(PlayerStat.Stamina);
            if (_currentStaminaExhaustion > 0.0)
            {
                _currentStaminaExhaustion -= Time.deltaTime;
                return;
            }

            if (_isSprinting && IsValidSprinting())
            {
                GetStat(PlayerStat.Stamina).TickStat(0 - Time.deltaTime * sprintCostMultiplier);
                UpdateStatInUI(PlayerStat.Stamina);
                if (stamina.GetCurrent() > 0)
                {
                    return;
                }
                SetSprinting(false);
                _currentStaminaExhaustion = secondsForStaminaExhaustion;
                return;
            }
            GetStat(PlayerStat.Stamina).TickStat(Time.deltaTime);
            UpdateStatInUI(PlayerStat.Stamina);
        }

        private void TickHunger()
        {
            if (IsStarving())
            {
                GetStat(PlayerStat.Health).Adjust(0 - Mathf.Abs(starvingDamagePerSecond * Time.deltaTime));
                UpdateStatInUI(PlayerStat.Health);
                return;
            }
            GetStat(PlayerStat.Hunger).TickStat(Time.deltaTime);
            UpdateStatInUI(PlayerStat.Hunger);
        }

        private void TickThirst()
        {
            if (IsDehydrated())
            {
                GetStat(PlayerStat.Health).Adjust(0 - Mathf.Abs(dehydratedDamagePerSecond * Time.deltaTime));
                UpdateStatInUI(PlayerStat.Health);
                return;
            }
            GetStat(PlayerStat.Thirst).TickStat(Time.deltaTime);
            UpdateStatInUI(PlayerStat.Thirst);
        }
        
        private void TickHealth()
        {
            if (IsDehydrated() || IsStarving())
            {
                return;
            }
            GetStat(PlayerStat.Health).TickStat(Time.deltaTime);
            UpdateStatInUI(PlayerStat.Health);
        }

        private void UpdateStatInUI(PlayerStat stat)
        {
            switch (stat)
            {
                case PlayerStat.Health:
                    OnStatChanged?.Invoke(PlayerStat.Health, GetStat(PlayerStat.Health).GetCurrent());
                    break;
                case PlayerStat.Stamina:
                    OnStatChanged?.Invoke(PlayerStat.Stamina, GetStat(PlayerStat.Stamina).GetCurrent());
                    break;
                case PlayerStat.Hunger:
                    OnStatChanged?.Invoke(PlayerStat.Hunger, GetStat(PlayerStat.Hunger).GetCurrent());
                    break;
                case PlayerStat.Thirst:
                    OnStatChanged?.Invoke(PlayerStat.Thirst, GetStat(PlayerStat.Thirst).GetCurrent());
                    break;
                default:
                    break;
            }
        }

        private bool IsStarving()
        {
            return GetStat(PlayerStat.Hunger).GetCurrent() <= 0.0;
        }

        private bool IsDehydrated()
        {
            return GetStat(PlayerStat.Thirst).GetCurrent() <= 0.0;
        }

        public bool HasEnoughStaminaToJump()
        {
            return GetStat(PlayerStat.Stamina).GetCurrent() >= jumpCost;;
        }

        public void HasJumped()
        {
            GetStat(PlayerStat.Stamina).Adjust(0 - jumpCost);
        }

        public bool HasEnoughStaminaToSprint()
        {
            return GetStat(PlayerStat.Stamina).GetCurrent() > 0.0;
        }

        private bool IsValidSprinting()
        {
            return _characterController.velocity.magnitude > walkSpeed && _characterController.isGrounded;
        }

        public void SetSprinting(bool isSprinting)
        {
            _isSprinting = isSprinting;
            if (_isSneaking && !_isSprinting)
            {
                return;
            }
            _isSneaking = false;
        }

        public void SetSneaking(bool isSneaking)
        {
            _isSneaking = isSneaking;
            if (_isSprinting && !_isSneaking)
            {
                return;
            }
            _isSprinting = false;
        }

        public bool GetIsSprinting()
        {
            return _isSprinting;
        }

        public bool GetIsSneaking()
        {
            return _isSneaking;
        }

        public float GetWalkSpeed()
        {
            return walkSpeed;
        }

        public float GetSprintSpeed()
        {
            return sprintSpeed;
        }

        public float GetSneakSpeed()
        {
            return sneakSpeed;
        }

        public float GetJumpHeight()
        {
            return jumpHeight;
        }
        
        public PlayerStatData GetStat(PlayerStat playerStat)
        {
            _stats.TryGetValue(playerStat, out var statData);
            return statData;
        }
    }
}