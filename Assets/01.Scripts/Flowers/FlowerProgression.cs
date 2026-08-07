using System.Collections.Generic;
using UnityEngine;

namespace CONFUSEDGAMEDEV.PollenGarden.Flowers
{
    /// <summary>
    /// Replaces a completed flower with the next species in the configured order. This is the
    /// smallest slice of what GardenManager will eventually own (plots, tending, seeds); when that
    /// lands, this component dissolves into it.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Pollen Garden/Flower Progression")]
    public sealed class FlowerProgression : MonoBehaviour
    {
        [SerializeField]
        private FlowerController flower;

        [Tooltip("Species in unlock order. Completion advances to the entry after the current one.")]
        [SerializeField]
        private List<FlowerSpeciesData> speciesInOrder = new List<FlowerSpeciesData>();

        [Tooltip("Wrap to the first species after the last. Prototype behaviour — the real design " +
                 "re-seeds at the highest unlocked species (Plan.md §2).")]
        [SerializeField]
        private bool loopAfterLast = true;

        public FlowerController Flower
        {
            get => flower;
            set => flower = value;
        }

        public List<FlowerSpeciesData> SpeciesInOrder => speciesInOrder;

        private void OnEnable()
        {
            if (flower != null)
            {
                flower.FlowerCompleted += HandleFlowerCompleted;
            }
        }

        private void OnDisable()
        {
            if (flower != null)
            {
                flower.FlowerCompleted -= HandleFlowerCompleted;
            }
        }

        private void HandleFlowerCompleted(FlowerController completedFlower)
        {
            Advance();
        }

        /// <summary>
        /// Swaps the flower to the species after its current one and rebuilds — the rebuild is
        /// what "destroys" the old flower, since the controller owns all generated children.
        /// </summary>
        public void Advance()
        {
            if (flower == null || speciesInOrder.Count == 0)
            {
                return;
            }

            int currentIndex = speciesInOrder.IndexOf(flower.Species);
            int nextIndex = currentIndex + 1;

            if (nextIndex >= speciesInOrder.Count)
            {
                if (!loopAfterLast)
                {
                    Debug.Log("[FlowerProgression] Last species completed; nothing to advance to.");
                    return;
                }

                nextIndex = 0;
            }

            flower.Species = speciesInOrder[nextIndex];
            flower.Rebuild();
        }
    }
}
