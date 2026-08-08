namespace CONFUSEDGAMEDEV.PollenGarden.Core
{
    public enum ScreenEdge
    {
        Left = 0,
        Right = 1,
        Bottom = 2,
        Top = 3,
    }

    /// <summary>Where helper agents may enter and leave the screen.</summary>
    public enum HelperEntryMode
    {
        /// <summary>Only the two edges nearest the flower — visitors come from the neighbourhood.</summary>
        ClosestTwoEdges = 0,

        /// <summary>Any of the four edges.</summary>
        AllEdges = 1,
    }

    /// <summary>Pure edge geometry, shared by the spawn logic and its tests.</summary>
    public static class ScreenEdges
    {
        /// <summary>
        /// The two edges closest to a viewport point: the nearer of left/right and the nearer of
        /// bottom/top. For a flower parked near a corner (the normal case) these are exactly the
        /// two edges forming that corner.
        /// </summary>
        public static (ScreenEdge, ScreenEdge) ClosestTwo(double viewportX, double viewportY)
        {
            ScreenEdge horizontal = viewportX < 0.5 ? ScreenEdge.Left : ScreenEdge.Right;
            ScreenEdge vertical = viewportY < 0.5 ? ScreenEdge.Bottom : ScreenEdge.Top;
            return (horizontal, vertical);
        }
    }
}
