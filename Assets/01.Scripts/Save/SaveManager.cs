using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using CONFUSEDGAMEDEV.PollenGarden.Core;
using CONFUSEDGAMEDEV.PollenGarden.Economy;
using CONFUSEDGAMEDEV.PollenGarden.Flowers;
using CONFUSEDGAMEDEV.PollenGarden.Helpers;
using Unity.Scripting.LifecycleManagement;
using UnityEngine;

namespace CONFUSEDGAMEDEV.PollenGarden.Save
{
    /// <summary>
    /// Persistence per Plan.md §3: one versioned <see cref="SaveModel"/> as JSON under
    /// <see cref="Application.persistentDataPath"/>, written to two alternating slots (a torn
    /// write can only ever cost the newest save, never both), autosaved on an interval and on
    /// focus loss and quit, loaded once on <c>Start</c>.
    /// </summary>
    /// <remarks>
    /// Capture and apply are public and file-free so EditMode tests can exercise the round trip
    /// without touching disk; the file layer is only <see cref="SaveNow"/> and
    /// <see cref="TryLoadAndApply"/>.
    /// </remarks>
    [DisallowMultipleComponent]
    [AddComponentMenu("Pollen Garden/Save Manager")]
    public sealed class SaveManager : MonoBehaviour
    {
        // Two fixed names, alternated between. Never reassigned at runtime — nothing to clean up.
        [NoAutoStaticsCleanup]
        private static readonly string[] SlotFileNames = { "save_slot_a.json", "save_slot_b.json" };

        [Tooltip("Seconds between autosaves while running (Plan.md: 30).")]
        [SerializeField, Min(5f)]
        private float autosaveIntervalSeconds = 30f;

        [SerializeField]
        private EconomyManager economy;

        [SerializeField]
        private HelperManager helpers;

        [SerializeField]
        private FlowerController tendedFlower;

        [SerializeField]
        private FlowerProgression progression;

        private string saveDirectoryOverride;
        private float nextAutosaveTime;
        private int lastWrittenSlot = -1;

        public EconomyManager Economy { get => economy; set => economy = value; }
        public HelperManager Helpers { get => helpers; set => helpers = value; }
        public FlowerController TendedFlower { get => tendedFlower; set => tendedFlower = value; }
        public FlowerProgression Progression { get => progression; set => progression = value; }

        /// <summary>Tests point this at a temp directory; empty means persistentDataPath.</summary>
        public string SaveDirectoryOverride
        {
            get => saveDirectoryOverride;
            set => saveDirectoryOverride = value;
        }

        private string SaveDirectory => string.IsNullOrEmpty(saveDirectoryOverride)
            ? Application.persistentDataPath
            : saveDirectoryOverride;

        private void Start()
        {
            TryLoadAndApply();
            nextAutosaveTime = Time.unscaledTime + autosaveIntervalSeconds;
        }

        private void Update()
        {
            if (Time.unscaledTime >= nextAutosaveTime)
            {
                SaveNow();
                nextAutosaveTime = Time.unscaledTime + autosaveIntervalSeconds;
            }
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            // Focus loss is the overlay's normal life (every desktop click), so this fires often;
            // the save is small enough that simplicity beats debouncing until profiling says otherwise.
            if (!hasFocus && Application.isPlaying)
            {
                SaveNow();
            }
        }

        private void OnApplicationQuit()
        {
            SaveNow();
        }

        /// <summary>Reads current state from the wired systems. No file access.</summary>
        public SaveModel CaptureModel()
        {
            var model = new SaveModel
            {
                savedAtUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
            };

            if (economy != null)
            {
                model.pollen = economy.Model.Pollen;
                model.nectar = economy.Model.Nectar;
            }

            if (tendedFlower != null && tendedFlower.Species != null)
            {
                model.flowerSpecies = tendedFlower.Species.name;
                foreach (PetalController petal in tendedFlower.Petals)
                {
                    model.petals.Add(new PetalSave
                    {
                        index = petal.PetalIndex,
                        hitPoints = petal.RemainingHitPoints,
                    });
                }
            }

            if (helpers != null)
            {
                foreach (KeyValuePair<HelperData, int> owned in helpers.OwnedCounts)
                {
                    model.helpers.Add(new HelperSave { helperName = owned.Key.name, count = owned.Value });
                }
            }

            return model;
        }

        /// <summary>Pushes a snapshot back into the wired systems. No file access.</summary>
        public void ApplyModel(SaveModel model)
        {
            if (model == null || model.version != SaveModel.CurrentVersion)
            {
                return;
            }

            if (economy != null)
            {
                economy.Model.Restore(model.pollen, model.nectar);
            }

            if (helpers != null)
            {
                foreach (HelperSave saved in model.helpers)
                {
                    HelperData data = helpers.AvailableHelpers.Find(
                        h => h != null && h.name == saved.helperName);
                    if (data != null)
                    {
                        helpers.RestoreOwnedCount(data, saved.count);
                    }
                }
            }

            RestoreFlower(model);
        }

        public void SaveNow()
        {
            SaveModel model = CaptureModel();
            string json = JsonUtility.ToJson(model, true);
            int slot = PickSlotToWrite();

            try
            {
                Directory.CreateDirectory(SaveDirectory);
                File.WriteAllText(SlotPath(slot), json);
                lastWrittenSlot = slot;
            }
            catch (Exception exception)
            {
                Debug.LogError($"[SaveManager] Save failed: {exception.Message}");
            }
        }

        /// <summary>Loads the newest valid slot, if any. A corrupt slot falls back to the other.</summary>
        public bool TryLoadAndApply()
        {
            SaveModel newest = null;
            DateTime newestTime = DateTime.MinValue;
            int newestSlot = -1;

            for (int slot = 0; slot < SlotFileNames.Length; slot++)
            {
                if (TryReadSlot(slot, out SaveModel model, out DateTime savedAt)
                    && (newest == null || savedAt > newestTime))
                {
                    newest = model;
                    newestTime = savedAt;
                    newestSlot = slot;
                }
            }

            if (newest == null)
            {
                return false;
            }

            ApplyModel(newest);
            lastWrittenSlot = newestSlot; // next write goes to the other slot
            Debug.Log($"[SaveManager] Loaded {SlotFileNames[newestSlot]} (saved {newest.savedAtUtc}).");
            return true;
        }

        private void RestoreFlower(SaveModel model)
        {
            if (tendedFlower == null || string.IsNullOrEmpty(model.flowerSpecies)
                || model.petals.Count == 0)
            {
                return;
            }

            FlowerSpeciesData species = progression != null
                ? progression.SpeciesInOrder.Find(s => s != null && s.name == model.flowerSpecies)
                : null;
            if (species == null)
            {
                // Renamed or removed species asset: keep the fresh flower rather than guessing.
                Debug.LogWarning($"[SaveManager] Unknown species '{model.flowerSpecies}' in save; skipping flower restore.");
                return;
            }

            tendedFlower.Species = species;
            tendedFlower.Rebuild();

            var savedByIndex = new Dictionary<int, int>(model.petals.Count);
            foreach (PetalSave petal in model.petals)
            {
                savedByIndex[petal.index] = petal.hitPoints;
            }

            // Snapshot the list: removals mutate the flower's collection while we walk it.
            var currentPetals = new List<PetalController>(tendedFlower.Petals);
            foreach (PetalController petal in currentPetals)
            {
                if (savedByIndex.TryGetValue(petal.PetalIndex, out int hitPoints) && hitPoints > 0)
                {
                    petal.RestoreHitPoints(hitPoints);
                }
                else
                {
                    tendedFlower.RemovePetal(petal);
                }
            }
        }

        private string SlotPath(int slot)
        {
            return Path.Combine(SaveDirectory, SlotFileNames[slot]);
        }

        private int PickSlotToWrite()
        {
            if (lastWrittenSlot >= 0)
            {
                return 1 - lastWrittenSlot;
            }

            // First write of this run: overwrite the older (or missing/corrupt) slot so the
            // newest surviving save is never the one at risk.
            bool slotAValid = TryReadSlot(0, out _, out DateTime savedA);
            bool slotBValid = TryReadSlot(1, out _, out DateTime savedB);

            if (!slotAValid)
            {
                return 0;
            }

            if (!slotBValid)
            {
                return 1;
            }

            return savedA <= savedB ? 0 : 1;
        }

        private bool TryReadSlot(int slot, out SaveModel model, out DateTime savedAt)
        {
            model = null;
            savedAt = DateTime.MinValue;
            string path = SlotPath(slot);

            try
            {
                if (!File.Exists(path))
                {
                    return false;
                }

                model = JsonUtility.FromJson<SaveModel>(File.ReadAllText(path));
                if (model == null || model.version != SaveModel.CurrentVersion)
                {
                    model = null;
                    return false;
                }

                if (!DateTime.TryParse(model.savedAtUtc, CultureInfo.InvariantCulture,
                        DateTimeStyles.RoundtripKind, out savedAt))
                {
                    savedAt = DateTime.MinValue;
                }

                return true;
            }
            catch (Exception)
            {
                // Torn write or hand-edited file: treat as absent, the other slot survives.
                model = null;
                return false;
            }
        }
    }
}
