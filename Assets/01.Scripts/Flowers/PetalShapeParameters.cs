using System;
using UnityEngine;

namespace CONFUSEDGAMEDEV.PollenGarden.Flowers
{
    /// <summary>
    /// Silhouette description for a single flat, stylized petal.
    /// Pure math with no <see cref="Mesh"/> or scene dependency, so it stays EditMode-testable.
    /// </summary>
    /// <remarks>
    /// The half-width along the petal is a normalized Beta curve:
    /// <code>
    /// w(t) = halfWidth * t^p * (1-t)^q / peak,   peak = tPeak^p * (1-tPeak)^q,   tPeak = p/(p+q)
    /// </code>
    /// which guarantees <c>w(0) == w(1) == 0</c> and makes <see cref="HalfWidth"/> literally the
    /// widest half-width in local units. <c>q &lt; 1</c> gives a blunt, rounded tip (daisy);
    /// <c>q &gt; 1</c> gives a sharp point. <c>p &lt; 1</c> narrows the base so it tucks under the disc.
    /// </remarks>
    [Serializable]
    public struct PetalShapeParameters
    {
        public const int MinLengthSegments = 2;
        public const int MaxLengthSegments = 64;
        public const float MinExponent = 0.05f;
        public const float MaxExponent = 4f;
        public const float MinSize = 0.001f;

        [SerializeField, Min(MinSize)]
        private float length;

        [SerializeField, Min(MinSize)]
        private float halfWidth;

        [SerializeField, Range(MinLengthSegments, MaxLengthSegments)]
        private int lengthSegments;

        [SerializeField, Range(MinExponent, MaxExponent)]
        private float baseRoundness;

        [SerializeField, Range(MinExponent, MaxExponent)]
        private float tipSharpness;

        public PetalShapeParameters(float length, float halfWidth, int lengthSegments,
                                    float baseRoundness, float tipSharpness)
        {
            this.length = length;
            this.halfWidth = halfWidth;
            this.lengthSegments = lengthSegments;
            this.baseRoundness = baseRoundness;
            this.tipSharpness = tipSharpness;
        }

        /// <summary>Blunt-tipped daisy petal: widest at ~57% of its length.</summary>
        public static PetalShapeParameters Default =>
            new PetalShapeParameters(1.2f, 0.3f, 12, 0.8f, 0.6f);

        public float Length => length;
        public float HalfWidth => halfWidth;
        public int LengthSegments => lengthSegments;

        /// <summary>Beta exponent <c>p</c>. Below 1 the base tapers; above 1 it swells.</summary>
        public float BaseRoundness => baseRoundness;

        /// <summary>Beta exponent <c>q</c>. Below 1 the tip is blunt; above 1 it comes to a point.</summary>
        public float TipSharpness => tipSharpness;

        /// <summary>Normalized position along the petal where it is widest.</summary>
        public float WidestPointT => baseRoundness / (baseRoundness + tipSharpness);

        public int VertexCount => 2 * lengthSegments + 1;

        public int TriangleCount => 2 * lengthSegments - 1;

        /// <summary>Returns a copy with every field forced into its valid range.</summary>
        public PetalShapeParameters Clamped()
        {
            return new PetalShapeParameters(
                Mathf.Max(length, MinSize),
                Mathf.Max(halfWidth, MinSize),
                Mathf.Clamp(lengthSegments, MinLengthSegments, MaxLengthSegments),
                Mathf.Clamp(baseRoundness, MinExponent, MaxExponent),
                Mathf.Clamp(tipSharpness, MinExponent, MaxExponent));
        }

        /// <summary>Half-width at normalized length <paramref name="t"/>, in local units.</summary>
        public float HalfWidthAt(float t)
        {
            if (t <= 0f || t >= 1f)
            {
                return 0f;
            }

            float p = Mathf.Clamp(baseRoundness, MinExponent, MaxExponent);
            float q = Mathf.Clamp(tipSharpness, MinExponent, MaxExponent);
            float peakT = p / (p + q);
            float peak = Mathf.Pow(peakT, p) * Mathf.Pow(1f - peakT, q);

            if (peak <= Mathf.Epsilon)
            {
                return 0f;
            }

            return halfWidth * Mathf.Pow(t, p) * Mathf.Pow(1f - t, q) / peak;
        }
    }
}
