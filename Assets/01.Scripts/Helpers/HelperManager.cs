using System;
using System.Collections.Generic;
using CONFUSEDGAMEDEV.PollenGarden.Core;
using CONFUSEDGAMEDEV.PollenGarden.Economy;
using CONFUSEDGAMEDEV.PollenGarden.Flowers;
using UnityEngine;

namespace CONFUSEDGAMEDEV.PollenGarden.Helpers
{
    /// <summary>
    /// Ownership ledger, purchase path, and visit engine for helpers. Owned helpers tick
    /// <see cref="HelperVisitAccumulator"/>s (the schedule math lives in Core); each due visit
    /// spawns a pooled <see cref="HelperAgent"/>, and when the agent reaches its petal the
    /// harvest happens here in <see cref="ExecuteVisit"/> — bees and butterflies damage through
    /// the normal click path (earning pollen), hummingbirds damage silently and bank nectar.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Pollen Garden/Helper Manager")]
    public sealed class HelperManager : MonoBehaviour
    {
        [SerializeField]
        private EconomyManager economy;

        [SerializeField]
        private FlowerController tendedFlower;

        [Tooltip("What the shop offers, in display order.")]
        [SerializeField]
        private List<HelperData> availableHelpers = new List<HelperData>();

        private readonly Dictionary<HelperData, int> ownedCounts = new Dictionary<HelperData, int>();
        private readonly Dictionary<HelperData, HelperVisitAccumulator> visitClocks =
            new Dictionary<HelperData, HelperVisitAccumulator>();
        private readonly Dictionary<HelperData, Material> agentMaterials =
            new Dictionary<HelperData, Material>();
        private readonly Stack<HelperAgent> agentPool = new Stack<HelperAgent>();

        private Mesh agentMesh;

        /// <summary>Raised when a purchase changes the ownership ledger.</summary>
        public event Action Changed;

        public EconomyManager Economy
        {
            get => economy;
            set => economy = value;
        }

        public FlowerController TendedFlower
        {
            get => tendedFlower;
            set => tendedFlower = value;
        }

        public List<HelperData> AvailableHelpers => availableHelpers;

        /// <summary>Snapshot for the save system; only owned helpers appear.</summary>
        public IReadOnlyDictionary<HelperData, int> OwnedCounts => ownedCounts;

        public int GetOwnedCount(HelperData helper)
        {
            return helper != null && ownedCounts.TryGetValue(helper, out int count) ? count : 0;
        }

        public bool CanAfford(HelperData helper)
        {
            return helper != null && economy != null && economy.Model.Nectar >= helper.NectarCost;
        }

        /// <summary>Load path: set an owned count directly — no nectar changes hands.</summary>
        public void RestoreOwnedCount(HelperData helper, int count)
        {
            if (helper == null)
            {
                return;
            }

            if (count <= 0)
            {
                ownedCounts.Remove(helper);
            }
            else
            {
                ownedCounts[helper] = count;
            }

            Changed?.Invoke();
        }

        public bool TryPurchase(HelperData helper)
        {
            if (helper == null || economy == null || !economy.Model.TrySpendNectar(helper.NectarCost))
            {
                return false;
            }

            ownedCounts[helper] = GetOwnedCount(helper) + 1;
            Changed?.Invoke();
            return true;
        }

        private void Update()
        {
            // No living petals → hold the clocks entirely; helpers should not bank visits
            // against a flower that is mid-replacement.
            if (tendedFlower == null || tendedFlower.PetalCount == 0)
            {
                return;
            }

            foreach (HelperData helper in availableHelpers)
            {
                if (helper == null)
                {
                    continue;
                }

                int count = GetOwnedCount(helper);
                if (count <= 0)
                {
                    continue;
                }

                if (!visitClocks.TryGetValue(helper, out HelperVisitAccumulator clock))
                {
                    clock = new HelperVisitAccumulator(helper.VisitIntervalSeconds);
                    visitClocks[helper] = clock;
                }

                int visitsDue = clock.Advance(Time.deltaTime, count);
                for (int i = 0; i < visitsDue; i++)
                {
                    SpawnAgent(helper);
                }
            }
        }

        private void OnDestroy()
        {
            if (agentMesh != null)
            {
                Destroy(agentMesh);
            }

            foreach (Material material in agentMaterials.Values)
            {
                Destroy(material);
            }

            agentMaterials.Clear();
        }

        /// <summary>
        /// The harvest itself, separated from the flight so it is EditMode-testable. A dead
        /// target retargets to any living petal; no petals at all fizzles the visit.
        /// </summary>
        public void ExecuteVisit(HelperData helper, PetalController petal)
        {
            if (helper == null || tendedFlower == null)
            {
                return;
            }

            if (petal == null)
            {
                petal = RandomLivingPetal();
            }

            if (petal == null)
            {
                return;
            }

            if (helper.YieldType == HelperYieldType.NectarPerVisit)
            {
                // Silent damage: the Damaged event is the pollen path, and this visit pays nectar.
                petal.ApplyDamage(helper.PetalDamagePerVisit, raiseDamagedEvent: false);
                economy?.Model.AddNectar(helper.NectarPerVisit);
            }
            else
            {
                petal.ApplyDamage(helper.PetalDamagePerVisit);
            }
        }

        private void SpawnAgent(HelperData helper)
        {
            PetalController target = RandomLivingPetal();
            Camera cam = Camera.main;
            if (target == null || cam == null)
            {
                return;
            }

            HelperAgent agent = agentPool.Count > 0 ? agentPool.Pop() : CreateAgent();
            agent.GetComponent<MeshRenderer>().sharedMaterial = MaterialFor(helper);
            agent.Launch(helper, target,
                OffscreenPoint(cam), OffscreenPoint(cam),
                ExecuteVisit, ReturnToPool);
        }

        private HelperAgent CreateAgent()
        {
            if (agentMesh == null)
            {
                // Radius 0.5 → unit diameter; HelperData.AgentDiameter scales the transform.
                agentMesh = PetalMeshBuilder.CreateCenterDisc(0.5f, 20);
                agentMesh.name = "PG_HelperDisc";
            }

            var agentObject = new GameObject("PG_HelperAgent",
                typeof(MeshFilter), typeof(MeshRenderer), typeof(HelperAgent));
            agentObject.transform.SetParent(transform, false);
            agentObject.GetComponent<MeshFilter>().sharedMesh = agentMesh;

            MeshRenderer renderer = agentObject.GetComponent<MeshRenderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;

            return agentObject.GetComponent<HelperAgent>();
        }

        private Material MaterialFor(HelperData helper)
        {
            if (!agentMaterials.TryGetValue(helper, out Material material))
            {
                material = new Material(Shader.Find(FlowerController.UnlitShaderName))
                {
                    name = "PG_HelperMaterial_" + helper.name,
                };
                material.SetColor("_BaseColor", helper.AgentColor);
                agentMaterials[helper] = material;
            }

            return material;
        }

        private void ReturnToPool(HelperAgent agent)
        {
            agent.gameObject.SetActive(false);
            agentPool.Push(agent);
        }

        private PetalController RandomLivingPetal()
        {
            if (tendedFlower == null || tendedFlower.PetalCount == 0)
            {
                return null;
            }

            return tendedFlower.Petals[UnityEngine.Random.Range(0, tendedFlower.PetalCount)];
        }

        /// <summary>A point just outside a random screen edge, on the flower's depth plane.</summary>
        private Vector3 OffscreenPoint(Camera cam)
        {
            const float Margin = 0.08f;
            float along = UnityEngine.Random.value;
            Vector2 viewport = UnityEngine.Random.Range(0, 4) switch
            {
                0 => new Vector2(-Margin, along),
                1 => new Vector2(1f + Margin, along),
                2 => new Vector2(along, -Margin),
                _ => new Vector2(along, 1f + Margin),
            };

            Vector3 flowerPosition = tendedFlower != null ? tendedFlower.transform.position : Vector3.zero;
            float depth = Vector3.Dot(flowerPosition - cam.transform.position, cam.transform.forward);
            return cam.ViewportToWorldPoint(new Vector3(viewport.x, viewport.y, depth));
        }
    }
}
