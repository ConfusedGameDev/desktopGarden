using System;
using TMPro;
using UnityEngine;

namespace CONFUSEDGAMEDEV.PollenGarden.Flowers
{
    /// <summary>
    /// View component for one petal. Owns nothing durable — its mesh and material belong to the
    /// parent <see cref="FlowerController"/> and arrive through <see cref="Initialize"/>.
    /// </summary>
    /// <remarks>
    /// Also the petal's gameplay anchor: it tracks the clicks remaining, shows them on a small
    /// TextMeshPro label, and raises <see cref="Destroyed"/> when they reach zero. Destruction
    /// itself is the parent flower's job — the petal only reports; the flower owns the list.
    /// </remarks>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class PetalController : MonoBehaviour
    {
        public const string LabelName = "PG_PetalLabel";

        // Geometric plumbing, not game tuning (that lives in FlowerSpeciesData):
        // collider thickness for a flat mesh, and where along the petal the label sits.
        private const float ColliderDepth = 0.05f;
        private const float LabelAnchorAlongLength = 0.55f;
        private const float LabelZOffset = -0.02f;

        // TextMeshPro's world-space font sizing: 10 font points span one world unit, so this
        // renders the label as tall as the petal's half-width.
        private const float FontPointsPerWorldUnit = 10f;

        [SerializeField]
        private int petalIndex;

        [SerializeField]
        private float radialAngleDegrees;

        [SerializeField]
        private int maxHitPoints = 1;

        [SerializeField]
        private int remainingHitPoints = 1;

        private MeshFilter cachedMeshFilter;
        private MeshRenderer cachedMeshRenderer;
        private TextMeshPro label;

        /// <summary>Raised on every hit that lands (including the killing one). Economy listens.</summary>
        public event Action<PetalController> Damaged;

        /// <summary>Raised once, when hit points reach zero. The parent flower destroys the petal.</summary>
        public event Action<PetalController> Destroyed;

        /// <summary>Position in the petal ring, 0-based, ascending clockwise from the ring's start.</summary>
        public int PetalIndex => petalIndex;

        /// <summary>This petal's rotation about the flower's facing axis, in degrees.</summary>
        public float RadialAngleDegrees => radialAngleDegrees;

        public int MaxHitPoints => maxHitPoints;

        public int RemainingHitPoints => remainingHitPoints;

        public bool IsAlive => remainingHitPoints > 0;

        public MeshFilter MeshFilter =>
            cachedMeshFilter != null ? cachedMeshFilter : cachedMeshFilter = GetComponent<MeshFilter>();

        /// <remarks>
        /// When per-petal HP tint lands, it goes through this renderer with a
        /// <see cref="MaterialPropertyBlock"/>. That trades away SRP Batcher compatibility and GPU
        /// Resident Drawer eligibility, which is why the current per-species tint does not use one.
        /// Keep that switch confined to this class.
        /// </remarks>
        public MeshRenderer MeshRenderer =>
            cachedMeshRenderer != null ? cachedMeshRenderer : cachedMeshRenderer = GetComponent<MeshRenderer>();

        public void Initialize(int petalIndex, float radialAngleDegrees,
                               Mesh sharedMesh, Material sharedMaterial, FlowerSpeciesData species)
        {
            this.petalIndex = petalIndex;
            this.radialAngleDegrees = radialAngleDegrees;

            // sharedMesh / sharedMaterial, never .mesh / .material — the latter silently clone a
            // per-renderer copy on every access, which is the classic procedural-mesh leak.
            MeshFilter.sharedMesh = sharedMesh;
            MeshRenderer.sharedMaterial = sharedMaterial;

            maxHitPoints = species.PetalHitPoints;
            remainingHitPoints = maxHitPoints;

            EnsureCollider(sharedMesh);
            EnsureLabel(species);
            UpdateLabel();
        }

        /// <summary>
        /// Removes hit points and raises <see cref="Destroyed"/> when none remain. Damage after
        /// death is ignored so a queued double-click cannot fire the event twice.
        /// </summary>
        public void ApplyDamage(int damage)
        {
            if (!IsAlive || damage <= 0)
            {
                return;
            }

            remainingHitPoints = Mathf.Max(remainingHitPoints - damage, 0);
            UpdateLabel();
            Damaged?.Invoke(this);

            if (remainingHitPoints == 0)
            {
                Destroyed?.Invoke(this);
            }
        }

        /// <summary>
        /// Load path: overwrite remaining hit points with no gameplay side effects — no
        /// <see cref="Damaged"/> (the economy must not earn pollen from a reload) and no
        /// <see cref="Destroyed"/> (a saved petal is by definition alive, hence the min of 1).
        /// </summary>
        public void RestoreHitPoints(int value)
        {
            remainingHitPoints = Mathf.Clamp(value, 1, maxHitPoints);
            UpdateLabel();
        }

        /// <summary>
        /// The mesh is flat (z = 0), so the box gets a little depth for the physics raycast to hit.
        /// Reused across rebuilds, resized in case the species' silhouette changed.
        /// </summary>
        private void EnsureCollider(Mesh sharedMesh)
        {
            BoxCollider box = GetComponent<BoxCollider>();
            if (box == null)
            {
                box = gameObject.AddComponent<BoxCollider>();
            }

            Bounds meshBounds = sharedMesh.bounds;
            box.center = meshBounds.center;
            box.size = new Vector3(meshBounds.size.x, meshBounds.size.y, ColliderDepth);
        }

        private void EnsureLabel(FlowerSpeciesData species)
        {
            if (label == null)
            {
                GameObject labelObject = new GameObject(LabelName) { hideFlags = HideFlags.DontSave };
                labelObject.transform.SetParent(transform, false);
                label = labelObject.AddComponent<TextMeshPro>();
                label.alignment = TextAlignmentOptions.Center;
                label.textWrappingMode = TextWrappingModes.NoWrap;
                label.overflowMode = TextOverflowModes.Overflow;
                label.rectTransform.sizeDelta = Vector2.one;
            }

            PetalShapeParameters shape = species.PetalShape;

            // Mid-outer on the petal, nudged toward the camera so it never z-fights the mesh.
            label.rectTransform.localPosition =
                new Vector3(0f, shape.Length * LabelAnchorAlongLength, LabelZOffset);

            // The petal is rotated around the ring; counter-rotating the label keeps the number
            // upright (relative to the flower root, which does not itself rotate).
            label.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -radialAngleDegrees);

            label.fontSize = shape.HalfWidth * FontPointsPerWorldUnit;
            label.color = species.PetalLabelColor;
        }

        private void UpdateLabel()
        {
            if (label != null)
            {
                label.text = remainingHitPoints.ToString();
            }
        }
    }
}
