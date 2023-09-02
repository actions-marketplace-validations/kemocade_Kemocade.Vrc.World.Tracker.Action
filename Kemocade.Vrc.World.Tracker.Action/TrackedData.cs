namespace Kemocade.Vrc.World.Tracker.Action;

internal record TrackedData
{
    public required long FileTimeUtc { get; init; }
    public required Dictionary<string, TrackedVrcWorld> VrcWorldsById { get; init; }

    internal record TrackedVrcWorld
    {
        public required string Name { get; init; }
        public required int Visits { get; init; }
        public required int Favorites { get; init; }
        public required int Occupants { get; init; }
    }
}
