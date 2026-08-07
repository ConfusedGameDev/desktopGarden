using System.IO;
using CONFUSEDGAMEDEV.PollenGarden.Core;
using CONFUSEDGAMEDEV.PollenGarden.Economy;
using CONFUSEDGAMEDEV.PollenGarden.Flowers;
using CONFUSEDGAMEDEV.PollenGarden.Helpers;
using CONFUSEDGAMEDEV.PollenGarden.Save;
using NUnit.Framework;
using UnityEngine;

namespace CONFUSEDGAMEDEV.PollenGarden.Tests
{
    /// <summary>
    /// The persistence contract: capture→apply round-trips every saved fact (balances, species,
    /// per-petal HP, destroyed petals, helper ownership); the file layer alternates two slots and
    /// survives one of them being corrupt.
    /// </summary>
    public sealed class SaveSystemTests
    {
        private GameObject root;
        private FlowerSpeciesData species;
        private HelperData helper;
        private string tempDirectory;

        private SaveManager save;
        private FlowerController flower;
        private EconomyManager economy;
        private HelperManager helpers;

        [SetUp]
        public void SetUp()
        {
            species = ScriptableObject.CreateInstance<FlowerSpeciesData>();
            species.name = "SaveTestSpecies";
            helper = ScriptableObject.CreateInstance<HelperData>();
            helper.name = "Helper_SaveTest";

            root = new GameObject("SaveTest");
            flower = root.AddComponent<FlowerController>();
            flower.Species = species;
            flower.Rebuild();

            economy = root.AddComponent<EconomyManager>();
            helpers = root.AddComponent<HelperManager>();
            helpers.Economy = economy;
            helpers.AvailableHelpers.Add(helper);

            var progression = root.AddComponent<FlowerProgression>();
            progression.Flower = flower;
            progression.SpeciesInOrder.Add(species);

            save = root.AddComponent<SaveManager>();
            save.Economy = economy;
            save.Helpers = helpers;
            save.TendedFlower = flower;
            save.Progression = progression;
        }

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

            if (helper != null)
            {
                Object.DestroyImmediate(helper);
            }

            if (!string.IsNullOrEmpty(tempDirectory) && Directory.Exists(tempDirectory))
            {
                Directory.Delete(tempDirectory, true);
            }

            root = null;
            species = null;
            helper = null;
            tempDirectory = null;
        }

        private void UseTempDirectory()
        {
            tempDirectory = Path.Combine(Path.GetTempPath(), "pg_save_tests_" + System.Guid.NewGuid().ToString("N"));
            save.SaveDirectoryOverride = tempDirectory;
        }

        [Test]
        public void CaptureApply_RoundTripsEverything()
        {
            economy.Model.AddPollen(123);
            economy.Model.AddNectar(4);
            helpers.RestoreOwnedCount(helper, 2);
            flower.Petals[0].ApplyDamage(3);
            PetalController doomed = flower.Petals[1];
            doomed.ApplyDamage(doomed.RemainingHitPoints);
            int expectedPetalCount = flower.PetalCount;
            int expectedHitPoints = flower.Petals[0].RemainingHitPoints;

            SaveModel model = save.CaptureModel();

            // Wreck the live state, then restore from the snapshot.
            economy.Model.Restore(0, 0);
            helpers.RestoreOwnedCount(helper, 0);
            flower.Rebuild();
            save.ApplyModel(model);

            Assert.AreEqual(123, economy.Model.Pollen);
            Assert.AreEqual(4, economy.Model.Nectar);
            Assert.AreEqual(2, helpers.GetOwnedCount(helper));
            Assert.AreEqual(expectedPetalCount, flower.PetalCount);
            Assert.AreEqual(expectedHitPoints, flower.Petals[0].RemainingHitPoints);
            foreach (PetalController petal in flower.Petals)
            {
                Assert.AreNotEqual(doomed.PetalIndex, petal.PetalIndex,
                    "Destroyed petal came back from the dead.");
            }
        }

        [Test]
        public void ApplyModel_WrongVersion_IsIgnored()
        {
            economy.Model.AddPollen(50);
            SaveModel model = save.CaptureModel();
            model.version = SaveModel.CurrentVersion + 1;
            model.pollen = 999;

            save.ApplyModel(model);

            Assert.AreEqual(50, economy.Model.Pollen);
        }

        [Test]
        public void SaveNow_AlternatesSlots_AndLoadPicksNewest()
        {
            UseTempDirectory();

            economy.Model.AddPollen(10);
            save.SaveNow();
            economy.Model.AddPollen(15); // 25 total
            save.SaveNow();

            Assert.AreEqual(2, Directory.GetFiles(tempDirectory, "save_slot_*.json").Length,
                "Two saves should occupy two distinct slots.");

            economy.Model.Restore(0, 0);
            Assert.IsTrue(save.TryLoadAndApply());
            Assert.AreEqual(25, economy.Model.Pollen);
        }

        [Test]
        public void CorruptNewestSlot_FallsBackToOlderSlot()
        {
            UseTempDirectory();

            economy.Model.AddPollen(10);
            save.SaveNow(); // slot A
            economy.Model.AddPollen(15);
            save.SaveNow(); // slot B (newest)

            File.WriteAllText(Path.Combine(tempDirectory, "save_slot_b.json"), "{ not json");

            economy.Model.Restore(0, 0);
            Assert.IsTrue(save.TryLoadAndApply());
            Assert.AreEqual(10, economy.Model.Pollen, "Should fall back to the intact older slot.");
        }

        [Test]
        public void TryLoadAndApply_WithNoFiles_ReturnsFalse()
        {
            UseTempDirectory();
            Assert.IsFalse(save.TryLoadAndApply());
        }
    }
}
