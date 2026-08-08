using UnityEngine;

namespace CONFUSEDGAMEDEV.PollenGarden.Flowers
{
    /// <summary>
    /// Everything needed to build one species of flower. No magic numbers live in the flower code —
    /// all geometry and colour tuning is authored here.
    /// </summary>
    /// <remarks>
    /// Remaining gameplay fields (pollen per click, unlock cost) deliberately do not exist yet.
    /// They append alongside the economy without disturbing anything in this file.
    /// </remarks>
    [CreateAssetMenu(fileName = "FlowerSpecies_", menuName = "Pollen Garden/Flower Species Data")]
    public sealed class FlowerSpeciesData : ScriptableObject
    {
        public const int MinPetalCount = 3;
        public const int MaxPetalCount = 24;

        [Header("Identity")]
        [SerializeField]
        private string displayName = "Daisy";

        [SerializeField, Min(0)]
        private int unlockOrder;

        [Header("Petals")]
        [SerializeField, Range(MinPetalCount, MaxPetalCount)]
        private int petalCount = 6;

        [Tooltip("Distance from the flower centre to each petal's base, in local units.")]
        [SerializeField, Min(0f)]
        private float petalBaseRadius = 0.34f;

        [Tooltip("Rotates the whole petal ring, for species that should not start at 12 o'clock.")]
        [SerializeField, Range(-180f, 180f)]
        private float petalAngleOffset;

        [SerializeField]
        private PetalShapeParameters petalShape = PetalShapeParameters.Default;

        [Header("Centre disc")]
        [SerializeField, Min(PetalShapeParameters.MinSize)]
        private float centerRadius = 0.42f;

        [SerializeField, Range(PetalMeshBuilder.MinCenterSegments, 32)]
        private int centerSegments = 16;

        [Tooltip("Pulls the disc toward the camera so it does not z-fight the coplanar petal bases.")]
        [SerializeField, Min(0f)]
        private float centerDepthOffset = 0.002f;

        [Header("Colours")]
        [SerializeField]
        private Color petalColor = new Color(0.98f, 0.98f, 0.94f, 1f);

        [SerializeField]
        private Color centerColor = new Color(1f, 0.82f, 0.18f, 1f);

        [Header("Gameplay")]
        [Tooltip("Clicks needed to destroy one petal. The per-petal label counts down from this.")]
        [SerializeField, Min(1)]
        private int petalHitPoints = 100;

        [Tooltip("Colour of the clicks-remaining label; pick for contrast against Petal Color.")]
        [SerializeField]
        private Color petalLabelColor = new Color(0.16f, 0.13f, 0.1f, 1f);

        [Tooltip("Pollen earned per click on this species' petals.")]
        [SerializeField, Min(0)]
        private int pollenPerClick = 5;

        [Header("Botanical placard (gallery)")]
        [SerializeField]
        private string scientificName = "Bellis perennis";

        [Tooltip("Where the plant grows in the real world.")]
        [SerializeField]
        private string nativeRange = "";

        [SerializeField, TextArea(3, 6)]
        private string placardDescription = "";

        public string DisplayName => displayName;
        public int UnlockOrder => unlockOrder;

        public int PetalCount => Mathf.Clamp(petalCount, MinPetalCount, MaxPetalCount);
        public float PetalBaseRadius => Mathf.Max(petalBaseRadius, 0f);
        public float PetalAngleOffset => petalAngleOffset;
        public PetalShapeParameters PetalShape => petalShape.Clamped();

        public float CenterRadius => Mathf.Max(centerRadius, PetalShapeParameters.MinSize);
        public int CenterSegments => Mathf.Clamp(centerSegments,
            PetalMeshBuilder.MinCenterSegments, PetalMeshBuilder.MaxCenterSegments);
        public float CenterDepthOffset => Mathf.Max(centerDepthOffset, 0f);

        public Color PetalColor => petalColor;
        public Color CenterColor => centerColor;

        public int PetalHitPoints => Mathf.Max(petalHitPoints, 1);
        public Color PetalLabelColor => petalLabelColor;
        public int PollenPerClick => Mathf.Max(pollenPerClick, 0);

        public string ScientificName => scientificName;
        public string NativeRange => nativeRange;
        public string PlacardDescription => placardDescription;

        private void OnValidate()
        {
            petalHitPoints = Mathf.Max(petalHitPoints, 1);
            pollenPerClick = Mathf.Max(pollenPerClick, 0);
            petalCount = Mathf.Clamp(petalCount, MinPetalCount, MaxPetalCount);
            petalBaseRadius = Mathf.Max(petalBaseRadius, 0f);
            centerRadius = Mathf.Max(centerRadius, PetalShapeParameters.MinSize);
            centerSegments = Mathf.Clamp(centerSegments,
                PetalMeshBuilder.MinCenterSegments, PetalMeshBuilder.MaxCenterSegments);
            centerDepthOffset = Mathf.Max(centerDepthOffset, 0f);
            petalShape = petalShape.Clamped();
        }
    }
}
