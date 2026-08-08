using System;
using System.Collections.Generic;

namespace CONFUSEDGAMEDEV.PollenGarden.Core
{
    /// <summary>
    /// Everything that survives a quit, as one versioned snapshot. Plain serializable fields —
    /// the engine's JsonUtility does the (de)serialization in the Save layer, so this class stays
    /// engine-free and EditMode-testable. M3's offline progress will be derived from
    /// <see cref="savedAtUtc"/>.
    /// </summary>
    /// <remarks>
    /// Versioning: bump <see cref="CurrentVersion"/> on any breaking shape change and migrate (or
    /// deliberately discard) older saves in the Save layer. Assets are referenced by their asset
    /// <c>name</c> — renaming a species or helper asset orphans that part of old saves by design.
    /// </remarks>
    [Serializable]
    public sealed class SaveModel
    {
        public const int CurrentVersion = 1;

        public int version = CurrentVersion;

        /// <summary>ISO-8601 round-trip UTC timestamp; also decides which slot is newest.</summary>
        public string savedAtUtc = string.Empty;

        public int pollen;
        public int nectar;

        /// <summary>Asset name of the tended flower's species.</summary>
        public string flowerSpecies = string.Empty;

        /// <summary>Living petals only — a petal absent from this list was destroyed.</summary>
        public List<PetalSave> petals = new List<PetalSave>();

        public List<HelperSave> helpers = new List<HelperSave>();

        /// <summary>Asset names of species completed at least once, in first-completion order.</summary>
        public List<string> completedSpecies = new List<string>();

        /// <summary>
        /// Overlay-mode viewport anchor of the tended flower (the player can drag it anywhere).
        /// -1 means "not recorded" — saves from before this field keep their default position.
        /// </summary>
        public float overlayAnchorX = -1f;

        public float overlayAnchorY = -1f;

        /// <summary>Cast of <see cref="HelperEntryMode"/>; -1 means "not recorded".</summary>
        public int helperEntryMode = -1;
    }

    [Serializable]
    public sealed class PetalSave
    {
        /// <summary>The petal's ring index (<c>PetalController.PetalIndex</c>).</summary>
        public int index;

        public int hitPoints;
    }

    [Serializable]
    public sealed class HelperSave
    {
        /// <summary>Asset name of the helper (stable ID, not the display name).</summary>
        public string helperName = string.Empty;

        public int count;
    }
}
