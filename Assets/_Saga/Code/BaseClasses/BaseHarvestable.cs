using _Saga.Code.Player;
using UnityEngine;

namespace _Saga.Code.BaseClasses
{
    public class BaseHarvestable : BaseInteractable
    {
        [SerializeField] private GameObject readyToHarvestObject;
        [SerializeField] private GameObject harvestedObject;
        [SerializeField] private string harvestedText;
        [SerializeField] private bool isHarvested = false;
        
        public GameObject ReadyToHarvestObject { get => readyToHarvestObject; set => readyToHarvestObject = value; }
        public GameObject HarvestedObject { get => harvestedObject; set => harvestedObject = value; }
        public string HarvestedText { get => harvestedText; set => harvestedText = value; }
        public bool IsHarvested { get => isHarvested; set => isHarvested = value; }
        
        protected void Start()
        {
            SetHarvestState();
        }
        
        protected virtual void Harvest()
        {
            Debug.Log("Harvest");
            IsHarvested = true;
            InteractionText = HarvestedText;
            InitializeText();
            SetHarvestState();
        }
        
        public override void Interact(PlayerCharacter caller)
        {
            if (!IsInteractable || IsHarvested)
            {
                return;
            }
            Harvest();
        }
        
        protected void SetHarvestState()
        {
            if (readyToHarvestObject != null)
            {
                readyToHarvestObject.SetActive(!isHarvested);
            }
            if (harvestedObject != null)
            {
                harvestedObject.SetActive(isHarvested);
            }
        }
    }
}