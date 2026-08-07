using System;
using System.Collections.Generic;
using CONFUSEDGAMEDEV.PollenGarden.Platform;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace CONFUSEDGAMEDEV.PollenGarden.Flowers
{
    /// <summary>
    /// Builds a flower from a <see cref="FlowerSpeciesData"/>: one centre disc plus N petals
    /// arranged radially, sharing one procedural mesh and one material each.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This component is the <b>sole owner</b> of the two <see cref="Mesh"/> and two
    /// <see cref="Material"/> instances it creates. Generated children are marked
    /// <see cref="HideFlags.DontSave"/>, so they never dirty the scene and are regenerated on load.
    /// </para>
    /// <para>
    /// <see cref="ExecuteAlways"/> means the same <see cref="Rebuild"/> runs in Edit mode and at
    /// runtime — there is no separate editor-only builder to drift out of sync. It also makes
    /// domain reload self-healing: every recompile fires <c>OnEnable</c>, which sweeps orphaned
    /// children (whose references the reload dropped) and rebuilds.
    /// </para>
    /// </remarks>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [AddComponentMenu("Pollen Garden/Flower Controller")]
    public sealed class FlowerController : MonoBehaviour
    {
        public const string GeneratedPetalPrefix = "PG_Petal_";
        public const string GeneratedCenterName = "PG_Center";
        public const string UnlitShaderName = "Universal Render Pipeline/Unlit";

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int CullId = Shader.PropertyToID("_Cull");

        // InlineEditor exposes the species asset's fields directly on the controller, so tuning a
        // flower does not require bouncing between the GameObject and the .asset in the Project.
        [SerializeField]
        [InlineEditor(Expanded = true)]
        private FlowerSpeciesData species;

        [Tooltip("Cloned once per flower. Falls back to a fresh URP/Unlit material when unset.")]
        [SerializeField]
        private Material baseMaterial;

        private Mesh petalMesh;
        private Mesh centerMesh;
        private Material petalMaterial;
        private Material centerMaterial;

        private readonly List<PetalController> petals = new List<PetalController>();

        /// <summary>
        /// Raised when the last living petal is destroyed. Whoever replaces the flower (see
        /// <see cref="FlowerProgression"/>) listens here; this class only reports.
        /// </summary>
        public event Action<FlowerController> FlowerCompleted;

        /// <summary>Assigning does not rebuild; call <see cref="Rebuild"/> afterwards.</summary>
        public FlowerSpeciesData Species
        {
            get => species;
            set => species = value;
        }

        public Material BaseMaterial
        {
            get => baseMaterial;
            set => baseMaterial = value;
        }

        public int PetalCount => petals.Count;

        public IReadOnlyList<PetalController> Petals => petals;

        /// <summary>Extent of the assembled flower in this transform's local space.</summary>
        public Bounds LocalBounds { get; private set; }

        private void OnEnable()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                // Creating GameObjects during a scene-load OnEnable trips
                // "SendMessage cannot be called during Awake/OnEnable". Defer one tick.
                EditorApplication.delayCall += DeferredRebuild;
                return;
            }
#endif
            Rebuild();
        }

        // Deliberately not OnDisable — that would tear down on every domain reload and scene-view
        // toggle, fighting the rebuild rather than complementing it.
        private void OnDestroy()
        {
            Teardown();
        }

#if UNITY_EDITOR
        private void DeferredRebuild()
        {
            if (this == null || !isActiveAndEnabled)
            {
                return;
            }

            Rebuild();
        }
#endif

        /// <summary>
        /// Destroys any previously generated children and rebuilds the flower from
        /// <see cref="Species"/>. Idempotent, and allocates no new meshes after the first call.
        /// </summary>
        [ContextMenu("Rebuild Flower")]
        [Button("Rebuild Flower", ButtonSizes.Large)]
        public void Rebuild()
        {
            SweepGeneratedChildren();
            petals.Clear();

            if (species == null)
            {
                LocalBounds = new Bounds(Vector3.zero, Vector3.zero);
                return;
            }

            EnsureMeshes();
            EnsureMaterials();
            BuildCenter();
            BuildPetals();
            RecalculateLocalBounds();
        }

        /// <summary>Destroys the generated children and every mesh and material this flower owns.</summary>
        public void Teardown()
        {
            SweepGeneratedChildren();
            petals.Clear();

            DestroySafe(petalMesh);
            DestroySafe(centerMesh);
            DestroySafe(petalMaterial);
            DestroySafe(centerMaterial);

            petalMesh = null;
            centerMesh = null;
            petalMaterial = null;
            centerMaterial = null;
        }

        /// <summary>
        /// Recovers ownership from the scene graph rather than from fields. A
        /// <see cref="HideFlags.DontSave"/> object cannot be serialized, so a field pointing at one
        /// is not reliably restored after a domain reload — but the child GameObjects survive and
        /// still hold the references. The hierarchy is the durable ledger.
        /// </summary>
        private void SweepGeneratedChildren()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Transform child = transform.GetChild(i);
                if (child == null)
                {
                    continue;
                }

                if (!child.name.StartsWith(GeneratedPetalPrefix, StringComparison.Ordinal)
                    && !string.Equals(child.name, GeneratedCenterName, StringComparison.Ordinal))
                {
                    continue;
                }

                MeshFilter filter = child.GetComponent<MeshFilter>();
                if (filter != null)
                {
                    DestroyIfOrphaned(filter.sharedMesh);
                }

                MeshRenderer renderer = child.GetComponent<MeshRenderer>();
                if (renderer != null)
                {
                    DestroyIfOrphaned(renderer.sharedMaterial);
                }

                DestroySafe(child.gameObject);
            }
        }

        /// <summary>
        /// Destroys <paramref name="target"/> only if it is neither one of the four instances this
        /// flower currently owns (those get reused) nor an on-disk asset.
        /// </summary>
        private void DestroyIfOrphaned(Object target)
        {
            if (target == null)
            {
                return;
            }

            if (target == petalMesh || target == centerMesh
                || target == petalMaterial || target == centerMaterial)
            {
                return;
            }

            if (target == baseMaterial)
            {
                return;
            }

#if UNITY_EDITOR
            if (EditorUtility.IsPersistent(target))
            {
                return;
            }
#endif
            DestroySafe(target);
        }

        private void EnsureMeshes()
        {
            if (petalMesh == null)
            {
                petalMesh = new Mesh { name = PetalMeshBuilder.PetalMeshName, hideFlags = HideFlags.DontSave };
            }

            if (centerMesh == null)
            {
                centerMesh = new Mesh { name = PetalMeshBuilder.CenterMeshName, hideFlags = HideFlags.DontSave };
            }

            PetalMeshBuilder.WritePetal(petalMesh, species.PetalShape);
            PetalMeshBuilder.WriteCenterDisc(centerMesh, species.CenterRadius, species.CenterSegments);
        }

        private void EnsureMaterials()
        {
            petalMaterial = EnsureMaterial(petalMaterial, "PG_PetalMaterial", species.PetalColor);
            centerMaterial = EnsureMaterial(centerMaterial, "PG_CenterMaterial", species.CenterColor);
        }

        private Material EnsureMaterial(Material existing, string materialName, Color color)
        {
            Material material = existing;

            if (material == null)
            {
                material = baseMaterial != null
                    ? new Material(baseMaterial)
                    : new Material(Shader.Find(UnlitShaderName));
                material.hideFlags = HideFlags.DontSave;
            }

            material.name = materialName;
            material.SetColor(BaseColorId, color);

            // Double-sided. Costs nothing (it is a shader state driven by a material float) and
            // keeps petals visible once M1's droop and sway rotate them past the view plane.
            if (material.HasProperty(CullId))
            {
                material.SetFloat(CullId, (float)CullMode.Off);
            }

            return material;
        }

        private void BuildCenter()
        {
            GameObject centerObject = CreateGeneratedChild(GeneratedCenterName);
            centerObject.transform.localPosition = new Vector3(0f, 0f, -species.CenterDepthOffset);
            centerObject.transform.localRotation = Quaternion.identity;

            centerObject.AddComponent<MeshFilter>().sharedMesh = centerMesh;
            MeshRenderer renderer = centerObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = centerMaterial;
            ConfigureRenderer(renderer);
        }

        private void BuildPetals()
        {
            int petalCount = species.PetalCount;
            float angleStep = 360f / petalCount;
            float baseRadius = species.PetalBaseRadius;

            for (int i = 0; i < petalCount; i++)
            {
                float angleDegrees = i * angleStep + species.PetalAngleOffset;
                Quaternion rotation = Quaternion.Euler(0f, 0f, angleDegrees);

                GameObject petalObject = CreateGeneratedChild(GeneratedPetalPrefix + i.ToString("D2"));

                // The mesh grows along +Y from its own base, so rotating then pushing out along the
                // rotated +Y puts each petal's hinge on the ring at the correct angle.
                petalObject.transform.localRotation = rotation;
                petalObject.transform.localPosition = rotation * (Vector3.up * baseRadius);

                petalObject.AddComponent<MeshFilter>();
                MeshRenderer renderer = petalObject.AddComponent<MeshRenderer>();
                ConfigureRenderer(renderer);

                PetalController petal = petalObject.AddComponent<PetalController>();
                petal.Initialize(i, angleDegrees, petalMesh, petalMaterial, species);
                petal.Destroyed += HandlePetalDestroyed;
                petals.Add(petal);
            }
        }

        private void HandlePetalDestroyed(PetalController petal)
        {
            petal.Destroyed -= HandlePetalDestroyed;
            petals.Remove(petal);
            DestroySafe(petal.gameObject);

            if (petals.Count == 0)
            {
                FlowerCompleted?.Invoke(this);
            }
        }

        /// <summary>
        /// Publishes each living petal's screen rect so the platform layer can decide, per frame,
        /// whether the cursor is over something clickable. Without this the overlay window either
        /// swallows every desktop click or never receives any — see Plan.md §3 click-through.
        /// </summary>
        private void Update()
        {
            if (!Application.isPlaying || petals.Count == 0)
            {
                return;
            }

            Camera cam = Camera.main;
            if (cam == null)
            {
                return;
            }

            for (int i = 0; i < petals.Count; i++)
            {
                if (TryGetScreenRect(cam, petals[i].MeshRenderer.bounds, out Rect screenRect))
                {
                    InteractiveScreenRects.Publish(screenRect);
                }
            }
        }

        private static bool TryGetScreenRect(Camera cam, Bounds worldBounds, out Rect screenRect)
        {
            Vector3 min = worldBounds.min;
            Vector3 max = worldBounds.max;

            float minX = float.MaxValue, minY = float.MaxValue;
            float maxX = float.MinValue, maxY = float.MinValue;

            for (int corner = 0; corner < 8; corner++)
            {
                Vector3 world = new Vector3(
                    (corner & 1) == 0 ? min.x : max.x,
                    (corner & 2) == 0 ? min.y : max.y,
                    (corner & 4) == 0 ? min.z : max.z);

                Vector3 screen = cam.WorldToScreenPoint(world);
                if (screen.z <= 0f)
                {
                    // Behind the camera — the projection is meaningless, drop the whole rect.
                    screenRect = default;
                    return false;
                }

                minX = Mathf.Min(minX, screen.x);
                minY = Mathf.Min(minY, screen.y);
                maxX = Mathf.Max(maxX, screen.x);
                maxY = Mathf.Max(maxY, screen.y);
            }

            screenRect = Rect.MinMaxRect(minX, minY, maxX, maxY);
            return true;
        }

        private GameObject CreateGeneratedChild(string childName)
        {
            GameObject child = new GameObject(childName) { hideFlags = HideFlags.DontSave };
            child.transform.SetParent(transform, false);
            return child;
        }

        /// <summary>Strips everything an unlit overlay flower never needs from the render path.</summary>
        private static void ConfigureRenderer(MeshRenderer renderer)
        {
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            renderer.allowOcclusionWhenDynamic = false;
        }

        private void RecalculateLocalBounds()
        {
            float reach = Mathf.Max(
                species.PetalBaseRadius + species.PetalShape.Length,
                species.CenterRadius);

            LocalBounds = new Bounds(
                Vector3.zero,
                new Vector3(reach * 2f, reach * 2f, species.CenterDepthOffset));
        }

        /// <summary>
        /// <see cref="Object.Destroy(Object)"/> silently no-ops in Edit mode, which leaks.
        /// </summary>
        private static void DestroySafe(Object target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }
        }
    }
}
