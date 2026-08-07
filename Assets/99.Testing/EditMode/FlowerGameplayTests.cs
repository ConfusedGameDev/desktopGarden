using CONFUSEDGAMEDEV.PollenGarden.Flowers;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace CONFUSEDGAMEDEV.PollenGarden.Tests
{
    /// <summary>
    /// The click loop's state machine: petal hit points count down, the label mirrors them, dead
    /// petals leave the flower, an empty flower reports completion, and the progression swaps in
    /// the next species. All synchronous in EditMode because destruction goes through
    /// DestroyImmediate there.
    /// </summary>
    public sealed class FlowerGameplayTests
    {
        private GameObject root;
        private FlowerSpeciesData species;
        private FlowerSpeciesData secondSpecies;

        [TearDown]
        public void TearDown()
        {
            if (root != null)
            {
                Object.DestroyImmediate(root);
            }

            if (species != null)
            {
                Object.DestroyImmediate(species);
            }

            if (secondSpecies != null)
            {
                Object.DestroyImmediate(secondSpecies);
            }

            root = null;
            species = null;
            secondSpecies = null;
        }

        private FlowerController CreateFlower()
        {
            species = ScriptableObject.CreateInstance<FlowerSpeciesData>();

            root = new GameObject("GameplayTestFlower");
            FlowerController flower = root.AddComponent<FlowerController>();
            flower.Species = species;
            flower.Rebuild();
            return flower;
        }

        [Test]
        public void Rebuild_PetalsCarrySpeciesHitPointsColliderAndLabel()
        {
            FlowerController flower = CreateFlower();

            Assert.AreEqual(species.PetalCount, flower.PetalCount);

            foreach (PetalController petal in flower.Petals)
            {
                Assert.AreEqual(species.PetalHitPoints, petal.MaxHitPoints);
                Assert.AreEqual(species.PetalHitPoints, petal.RemainingHitPoints);
                Assert.IsTrue(petal.IsAlive);
                Assert.IsNotNull(petal.GetComponent<BoxCollider>(),
                    "Petal has no collider; the click raycast can never hit it.");

                TextMeshPro label = petal.GetComponentInChildren<TextMeshPro>();
                Assert.IsNotNull(label, "Petal has no clicks-remaining label.");
                Assert.AreEqual(species.PetalHitPoints.ToString(), label.text);
            }
        }

        [Test]
        public void ApplyDamage_CountsDownAndUpdatesLabel()
        {
            FlowerController flower = CreateFlower();
            PetalController petal = flower.Petals[0];

            petal.ApplyDamage(1);

            int expected = species.PetalHitPoints - 1;
            Assert.AreEqual(expected, petal.RemainingHitPoints);
            Assert.AreEqual(expected.ToString(), petal.GetComponentInChildren<TextMeshPro>().text);
        }

        [Test]
        public void ApplyDamage_ToZero_RemovesThePetalFromTheFlower()
        {
            FlowerController flower = CreateFlower();
            int initialCount = flower.PetalCount;
            PetalController petal = flower.Petals[0];

            petal.ApplyDamage(petal.RemainingHitPoints);

            Assert.AreEqual(initialCount - 1, flower.PetalCount);
            Assert.IsTrue(petal == null, "Dead petal's GameObject should be destroyed.");
        }

        [Test]
        public void DestroyingEveryPetal_FiresFlowerCompletedExactlyOnce()
        {
            FlowerController flower = CreateFlower();
            int completedCount = 0;
            flower.FlowerCompleted += _ => completedCount++;

            while (flower.PetalCount > 0)
            {
                PetalController petal = flower.Petals[0];
                petal.ApplyDamage(petal.RemainingHitPoints);
            }

            Assert.AreEqual(1, completedCount);
        }

        [Test]
        public void ApplyDamage_OnDeadPetal_IsIgnored()
        {
            FlowerController flower = CreateFlower();
            PetalController petal = flower.Petals[0];
            int destroyedEvents = 0;
            petal.Destroyed += _ => destroyedEvents++;

            petal.ApplyDamage(petal.RemainingHitPoints);
            petal.ApplyDamage(1);

            Assert.AreEqual(1, destroyedEvents);
        }

        [Test]
        public void Advance_SwapsToTheNextSpeciesAndWrapsAround()
        {
            FlowerController flower = CreateFlower();
            secondSpecies = ScriptableObject.CreateInstance<FlowerSpeciesData>();

            FlowerProgression progression = root.AddComponent<FlowerProgression>();
            progression.Flower = flower;
            progression.SpeciesInOrder.Add(species);
            progression.SpeciesInOrder.Add(secondSpecies);

            progression.Advance();
            Assert.AreSame(secondSpecies, flower.Species);
            Assert.AreEqual(secondSpecies.PetalCount, flower.PetalCount);

            progression.Advance();
            Assert.AreSame(species, flower.Species, "Progression should wrap after the last species.");
        }

        [Test]
        public void SpeciesAssets_CarryThePlannedHitPointCurve()
        {
            // Guards the hand-edited .asset values: Poppy's 220 differs from the field default,
            // so this failing means the YAML append did not survive import.
            var poppy = AssetDatabase.LoadAssetAtPath<FlowerSpeciesData>(
                "Assets/05.Data/Flowers/FlowerSpecies_Poppy.asset");

            Assert.IsNotNull(poppy);
            Assert.AreEqual(220, poppy.PetalHitPoints);
        }
    }
}
