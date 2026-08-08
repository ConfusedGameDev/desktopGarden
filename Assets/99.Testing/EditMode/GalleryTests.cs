using CONFUSEDGAMEDEV.PollenGarden.Flowers;
using NUnit.Framework;
using TMPro;
using UnityEngine;

namespace CONFUSEDGAMEDEV.PollenGarden.Tests
{
    /// <summary>
    /// The gallery's foundations: completions are recorded exactly once and restorable by name,
    /// and a decorative flower is a pure specimen — geometry only, nothing clickable, no labels.
    /// </summary>
    public sealed class GalleryTests
    {
        private GameObject root;
        private FlowerSpeciesData species;

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

            root = null;
            species = null;
        }

        [Test]
        public void RecordCompletion_ListsASpeciesOnlyOnce()
        {
            root = new GameObject("GalleryTest");
            species = ScriptableObject.CreateInstance<FlowerSpeciesData>();
            var progression = root.AddComponent<FlowerProgression>();
            int changes = 0;
            progression.CollectionChanged += () => changes++;

            progression.RecordCompletion(species);
            progression.RecordCompletion(species);
            progression.RecordCompletion(null);

            Assert.AreEqual(1, progression.CompletedSpecies.Count);
            Assert.AreSame(species, progression.CompletedSpecies[0]);
            Assert.AreEqual(1, changes);
        }

        [Test]
        public void RestoreCompletedSpecies_RebuildsFromNames_IgnoringUnknowns()
        {
            root = new GameObject("GalleryTest");
            species = ScriptableObject.CreateInstance<FlowerSpeciesData>();
            species.name = "GallerySpecies";
            var progression = root.AddComponent<FlowerProgression>();
            progression.SpeciesInOrder.Add(species);

            progression.RestoreCompletedSpecies(new[] { "GallerySpecies", "NoSuchSpecies" });

            Assert.AreEqual(1, progression.CompletedSpecies.Count);
            Assert.AreSame(species, progression.CompletedSpecies[0]);
        }

        [Test]
        public void DecorativeFlower_HasNoCollidersAndNoLabels()
        {
            root = new GameObject("GalleryTest");
            species = ScriptableObject.CreateInstance<FlowerSpeciesData>();
            var flower = root.AddComponent<FlowerController>();
            flower.Species = species;
            flower.Decorative = true;
            flower.Rebuild();

            Assert.Greater(flower.PetalCount, 0);
            Assert.AreEqual(0, root.GetComponentsInChildren<Collider>().Length,
                "A gallery specimen must not be clickable.");
            Assert.AreEqual(0, root.GetComponentsInChildren<TextMeshPro>().Length,
                "A gallery specimen must not show HP labels.");
            Assert.AreEqual(0, root.GetComponentsInChildren<FlowerCenterButton>().Length,
                "A gallery specimen must not open the menu.");
        }
    }
}
