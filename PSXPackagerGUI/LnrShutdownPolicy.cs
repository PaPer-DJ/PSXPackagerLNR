namespace PSXPackagerGUI
{
    /// <summary>
    /// A separate opt-in policy. A failed, cancelled, or incomplete job,
    /// cleanup warning, or an all-skipped batch must NEVER request shutdown.
    /// </summary>
    public static class LnrShutdownPolicy
    {
        public static bool MayShutdown(bool selected, bool completedNormally,
                                       bool canceled, int errors,
                                       int deletionWarnings, int converted)
            => selected && completedNormally && !canceled &&
               errors == 0 && deletionWarnings == 0 && converted > 0;
    }
}
