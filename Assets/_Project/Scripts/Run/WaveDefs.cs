namespace Survivors
{
    public sealed class WaveDef
    {
        public int Quota;            // enemies spawned this wave
        public float Interval;       // seconds between spawn batches
        public int Batch;            // enemies per batch
        public float SkeletonChance; // chance a spawn is a skeleton instead of a slime
        public bool Boss;            // spawns an elite at wave start
        public float TimeLimit;      // next wave starts anyway after this (all but the last wave)
    }

    public static class StageDefs
    {
        public const float IntermissionSeconds = 3f;

        public static readonly WaveDef[] Stage1 =
        {
            new WaveDef { Quota = 20, Interval = 1.0f,  Batch = 1, SkeletonChance = 0f,    TimeLimit = 70f },
            new WaveDef { Quota = 30, Interval = 0.85f, Batch = 1, SkeletonChance = 0f,    TimeLimit = 70f },
            new WaveDef { Quota = 40, Interval = 0.7f,  Batch = 2, SkeletonChance = 0.25f, TimeLimit = 70f },
            new WaveDef { Quota = 50, Interval = 0.6f,  Batch = 2, SkeletonChance = 0.3f,  TimeLimit = 70f },
            new WaveDef { Quota = 60, Interval = 0.5f,  Batch = 2, SkeletonChance = 0.35f, TimeLimit = 70f },
            new WaveDef { Quota = 45, Interval = 0.5f,  Batch = 3, SkeletonChance = 0.4f,  TimeLimit = 70f, Boss = true },
        };
    }
}
