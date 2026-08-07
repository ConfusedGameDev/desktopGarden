using UnityEngine;

namespace CONFUSEDGAMEDEV.PollenGarden.Economy
{
    /// <summary>
    /// Economy-wide tuning. Per the no-magic-numbers rule, anything a designer might retune from
    /// telemetry (trade rate now; offline cap and pacing constants when M3 lands) belongs here.
    /// </summary>
    [CreateAssetMenu(fileName = "EconomyConfig", menuName = "Pollen Garden/Economy Config")]
    public sealed class EconomyConfig : ScriptableObject
    {
        public const int DefaultTradePollenPerNectar = 100;

        [Tooltip("Pollen consumed to mint one nectar (Plan.md §2: 100:1).")]
        [SerializeField, Min(1)]
        private int tradePollenPerNectar = DefaultTradePollenPerNectar;

        public int TradePollenPerNectar => Mathf.Max(tradePollenPerNectar, 1);

        private void OnValidate()
        {
            tradePollenPerNectar = Mathf.Max(tradePollenPerNectar, 1);
        }
    }
}
