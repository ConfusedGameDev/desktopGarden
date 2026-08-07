using UnityEngine;

namespace CONFUSEDGAMEDEV.PollenGarden.Helpers
{
    /// <summary>
    /// One helper species (bee, butterfly, hummingbird): shop cost plus the visit behaviour M2's
    /// HelperAgent will consume. Authored per the no-magic-numbers rule; the M2 fields exist now
    /// so the shop can already show honest stats.
    /// </summary>
    [CreateAssetMenu(fileName = "Helper_", menuName = "Pollen Garden/Helper Data")]
    public sealed class HelperData : ScriptableObject
    {
        [Header("Identity & shop")]
        [SerializeField]
        private string displayName = "Bee";

        [SerializeField, Min(1)]
        private int nectarCost = 10;

        [Header("Visits (consumed by M2's HelperAgent)")]
        [SerializeField, Min(1f)]
        private float visitIntervalSeconds = 6f;

        [SerializeField, Min(1)]
        private int petalDamagePerVisit = 2;

        public string DisplayName => displayName;
        public int NectarCost => Mathf.Max(nectarCost, 1);
        public float VisitIntervalSeconds => Mathf.Max(visitIntervalSeconds, 1f);
        public int PetalDamagePerVisit => Mathf.Max(petalDamagePerVisit, 1);
    }
}
