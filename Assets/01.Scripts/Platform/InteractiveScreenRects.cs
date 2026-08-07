using System.Collections.Generic;
using Unity.Scripting.LifecycleManagement;
using UnityEngine;

namespace CONFUSEDGAMEDEV.PollenGarden.Platform
{
    /// <summary>
    /// Frame-scoped registry of screen rects the player can interact with (petals; later the HUD
    /// and Expand button). Publishers add rects during <c>Update</c>; the ClickThroughManager
    /// reads and clears them in <c>LateUpdate</c> to decide whether the overlay window should
    /// swallow the next click or let it fall through to the desktop.
    /// </summary>
    /// <remarks>
    /// Any new interactive element MUST publish its rect every frame it is clickable, or clicks
    /// on it will pass straight through to whatever is behind the window.
    /// </remarks>
    [NoAutoStaticsCleanup]
    public static class InteractiveScreenRects
    {
        // Immediate-mode and cleared every frame, so there is no cross-reload state for the
        // automatic statics cleanup to usefully reset — same opt-out as PetalMeshBuilder's buffers.
        [NoAutoStaticsCleanup]
        private static readonly List<Rect> Rects = new List<Rect>(32);

        public static int Count => Rects.Count;

        /// <summary>Rect in screen pixels, origin bottom-left (Unity screen convention).</summary>
        public static void Publish(Rect screenRect)
        {
            Rects.Add(screenRect);
        }

        /// <summary>
        /// Whether the point is inside any published rect, each inflated by
        /// <paramref name="inflateByPixels"/> on every side (the hysteresis margin).
        /// </summary>
        public static bool Contains(Vector2 screenPoint, float inflateByPixels)
        {
            for (int i = 0; i < Rects.Count; i++)
            {
                Rect rect = Rects[i];
                if (inflateByPixels > 0f)
                {
                    rect.xMin -= inflateByPixels;
                    rect.yMin -= inflateByPixels;
                    rect.xMax += inflateByPixels;
                    rect.yMax += inflateByPixels;
                }

                if (rect.Contains(screenPoint))
                {
                    return true;
                }
            }

            return false;
        }

        public static void Clear()
        {
            Rects.Clear();
        }
    }
}
