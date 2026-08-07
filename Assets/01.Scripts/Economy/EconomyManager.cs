using CONFUSEDGAMEDEV.PollenGarden.Core;
using CONFUSEDGAMEDEV.PollenGarden.Flowers;
using UnityEngine;

namespace CONFUSEDGAMEDEV.PollenGarden.Economy
{
    /// <summary>
    /// Scene-side owner of the <see cref="EconomyModel"/>: feeds petal clicks into it as pollen
    /// and exposes it to the UI. All rules live in the model (pure C#, tested in EditMode); this
    /// class is only the wiring.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Pollen Garden/Economy Manager")]
    public sealed class EconomyManager : MonoBehaviour
    {
        [SerializeField]
        private EconomyConfig config;

        [SerializeField]
        private FlowerController tendedFlower;

        private EconomyModel model;

        /// <summary>Lazy so EditMode tests and Awake-order-sensitive callers never see null.</summary>
        public EconomyModel Model => model ??= new EconomyModel(
            config != null ? config.TradePollenPerNectar : EconomyConfig.DefaultTradePollenPerNectar);

        public EconomyConfig Config
        {
            get => config;
            set => config = value;
        }

        public FlowerController TendedFlower
        {
            get => tendedFlower;
            set => tendedFlower = value;
        }

        private void OnEnable()
        {
            if (tendedFlower != null)
            {
                tendedFlower.PetalDamaged += HandlePetalDamaged;
            }
        }

        private void OnDisable()
        {
            if (tendedFlower != null)
            {
                tendedFlower.PetalDamaged -= HandlePetalDamaged;
            }
        }

        private void HandlePetalDamaged(PetalController petal)
        {
            if (tendedFlower != null && tendedFlower.Species != null)
            {
                Model.AddPollen(tendedFlower.Species.PollenPerClick);
            }
        }
    }
}
