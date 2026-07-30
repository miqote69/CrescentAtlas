using CrescentAtlas.Contracts;
using CrescentAtlas.Data;

namespace CrescentAtlas.Events;

/// <summary>
/// A side-effect-free description of newly observed events.
/// The caller decides whether and how to persist or notify about the observations.
/// </summary>
public sealed record EventDetectionBatch(
    IReadOnlyList<AtlasMarker> Markers,
    IReadOnlyList<ObservationRecord> Observations)
{
    public static EventDetectionBatch Empty { get; } =
        new(Array.Empty<AtlasMarker>(), Array.Empty<ObservationRecord>());
}
