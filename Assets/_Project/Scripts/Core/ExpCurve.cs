namespace Survivors
{
    public static class ExpCurve
    {
        // Run-level experience needed to go from `level` to `level + 1`.
        public static int Required(int level)
        {
            if (level < 1) level = 1;
            return 4 + 3 * level + (level * level) / 2;
        }
    }
}
