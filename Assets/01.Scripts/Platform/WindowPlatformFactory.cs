namespace CONFUSEDGAMEDEV.PollenGarden.Platform
{
    /// <summary>Picks the <see cref="IWindowPlatform"/> for the current runtime.</summary>
    public static class WindowPlatformFactory
    {
        public static IWindowPlatform Create()
        {
#if UNITY_EDITOR
            // Never restyle the editor's own window, no matter the active build target.
            return new OpaquePlatform();
#elif UNITY_STANDALONE_OSX
            return new MacWindowPlatform();
#elif UNITY_STANDALONE_WIN
            return new WindowsWindowPlatform();
#else
            return new OpaquePlatform();
#endif
        }
    }
}
