using UnityEngine;

namespace CONFUSEDGAMEDEV.PollenGarden.Helpers
{
    /// <summary>What a helper's visit deposits (Plan.md §2 helper table).</summary>
    public enum HelperYieldType
    {
        /// <summary>One click's worth of pollen, via the same event path as a player click.</summary>
        PollenPerClick = 0,

        /// <summary>Nectar credited directly; the damage grants no pollen.</summary>
        NectarPerVisit = 1,
    }

    /// <summary>
    /// One helper species (bee, butterfly, hummingbird): shop cost plus the visit behaviour and
    /// agent presentation. Authored per the no-magic-numbers rule.
    /// </summary>
    [CreateAssetMenu(fileName = "Helper_", menuName = "Pollen Garden/Helper Data")]
    public sealed class HelperData : ScriptableObject
    {
        [Header("Identity & shop")]
        [SerializeField]
        private string displayName = "Bee";

        [SerializeField, Min(1)]
        private int nectarCost = 10;

        [Header("Visits")]
        [SerializeField, Min(1f)]
        private float visitIntervalSeconds = 6f;

        [SerializeField, Min(1)]
        private int petalDamagePerVisit = 2;

        [SerializeField]
        private HelperYieldType yieldType = HelperYieldType.PollenPerClick;

        [Tooltip("Nectar deposited per visit. Only used when Yield Type is Nectar Per Visit.")]
        [SerializeField, Min(1)]
        private int nectarPerVisit = 1;

        [Header("Agent presentation")]
        [SerializeField]
        private Color agentColor = new Color(1f, 0.72f, 0.1f, 1f);

        [Tooltip("Diameter of the agent's disc, in world units (petals are ~0.6 wide).")]
        [SerializeField, Min(0.01f)]
        private float agentDiameter = 0.2f;

        [SerializeField, Min(0.1f)]
        private float flyDurationSeconds = 1.5f;

        [SerializeField, Min(0.1f)]
        private float collectDurationSeconds = 0.8f;

        public string DisplayName => displayName;
        public int NectarCost => Mathf.Max(nectarCost, 1);
        public float VisitIntervalSeconds => Mathf.Max(visitIntervalSeconds, 1f);
        public int PetalDamagePerVisit => Mathf.Max(petalDamagePerVisit, 1);
        public HelperYieldType YieldType => yieldType;
        public int NectarPerVisit => Mathf.Max(nectarPerVisit, 1);
        public Color AgentColor => agentColor;
        public float AgentDiameter => Mathf.Max(agentDiameter, 0.01f);
        public float FlyDurationSeconds => Mathf.Max(flyDurationSeconds, 0.1f);
        public float CollectDurationSeconds => Mathf.Max(collectDurationSeconds, 0.1f);
    }
}
