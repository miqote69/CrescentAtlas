using System.Globalization;
using CrescentAtlas.Contracts;
using CrescentAtlas.Data;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;

namespace CrescentAtlas.Collection;

public sealed record ObjectTableCollectionOptions
{
    /// <summary>
    /// Event-object Base/Data IDs confirmed to represent carrots.
    /// </summary>
    public IReadOnlySet<uint> CarrotDataIds { get; init; } = new HashSet<uint>();

    /// <summary>
    /// Event IDs confirmed to represent carrots, when exposed by the current Dalamud wrapper.
    /// </summary>
    public IReadOnlySet<uint> CarrotEventIds { get; init; } = new HashSet<uint>();

    /// <summary>
    /// Optional game-version-specific classifier. It runs only for EventObj objects.
    /// </summary>
    public Func<IGameObject, bool>? CarrotPredicate { get; init; }

    /// <summary>
    /// During discovery, retain unclassified EventObj instances as visibly labelled candidates.
    /// Disable this after North Horn identifiers have been confirmed.
    /// </summary>
    public bool IncludeUnclassifiedEventObjects { get; init; } = true;
}

/// <summary>
/// Read-only scanner for currently loaded treasure and carrot-candidate game objects.
/// Territory gating deliberately belongs to the caller because North Horn IDs are not yet confirmed.
/// </summary>
public sealed class ObjectTableCollector(
    IObjectTable objectTable,
    IObservationSink? observationSink = null,
    ObjectTableCollectionOptions? options = null)
{
    private readonly ObjectTableCollectionOptions options = options ?? new ObjectTableCollectionOptions();

    public IReadOnlyList<AtlasMarker> Collect(
        uint territoryId,
        string territoryName,
        DateTimeOffset? observedAtUtc = null)
    {
        var observedAt = (observedAtUtc ?? DateTimeOffset.UtcNow).ToUniversalTime();
        var markers = new List<AtlasMarker>();

        foreach (var gameObject in objectTable)
        {
            if (gameObject is null)
                continue;

            AtlasMarker? marker = gameObject.ObjectKind switch
            {
                ObjectKind.Treasure => CreateTreasureMarker(gameObject, territoryId, observedAt),
                ObjectKind.EventObj => CreateCarrotCandidateMarker(gameObject, territoryId, observedAt),
                _ => null,
            };

            if (marker is null)
                continue;

            markers.Add(marker);
            observationSink?.Record(ToObservation(marker, territoryName));
        }

        return markers
            .GroupBy(static marker => marker.Key, StringComparer.Ordinal)
            .Select(static group => group.First())
            .OrderBy(static marker => marker.Kind)
            .ThenBy(static marker => marker.Key, StringComparer.Ordinal)
            .ToArray();
    }

    private static AtlasMarker CreateTreasureMarker(
        IGameObject gameObject,
        uint territoryId,
        DateTimeOffset observedAt)
    {
        var dataId = gameObject.BaseId;
        var key = ObservationIdentity.PositionKey(
            territoryId,
            "active-treasure",
            dataId,
            eventId: 0,
            gameObject.Position);

        return new AtlasMarker(
            key,
            AtlasMarkerKind.ActiveTreasure,
            DisplayName(gameObject, "Treasure"),
            gameObject.Position,
            observedAt,
            IsActive: true,
            territoryId,
            dataId);
    }

    private AtlasMarker? CreateCarrotCandidateMarker(
        IGameObject gameObject,
        uint territoryId,
        DateTimeOffset observedAt)
    {
        var dataId = gameObject.BaseId;
        var eventId = TryReadEventId(gameObject);
        var confirmed = options.CarrotDataIds.Contains(dataId)
                        || options.CarrotEventIds.Contains(eventId)
                        || options.CarrotPredicate?.Invoke(gameObject) == true;

        if (!confirmed && !options.IncludeUnclassifiedEventObjects)
            return null;

        var key = ObservationIdentity.PositionKey(
            territoryId,
            confirmed ? "carrot" : "carrot-candidate",
            dataId,
            eventId,
            gameObject.Position);

        return new AtlasMarker(
            key,
            AtlasMarkerKind.Carrot,
            confirmed ? DisplayName(gameObject, "Carrot") : DisplayName(gameObject, "EventObj candidate"),
            gameObject.Position,
            observedAt,
            IsActive: true,
            territoryId,
            dataId,
            eventId);
    }

    private static ObservationRecord ToObservation(AtlasMarker marker, string territoryName)
        => new()
        {
            SessionId = string.Empty,
            ObservedAtUtc = marker.ObservedAtUtc,
            Source = ObservationSource.ObjectTable,
            Kind = marker.Kind == AtlasMarkerKind.ActiveTreasure
                ? "active-treasure"
                : marker.Label.Contains("candidate", StringComparison.OrdinalIgnoreCase)
                    ? "carrot-candidate"
                    : "carrot",
            Key = marker.Key,
            TerritoryId = marker.TerritoryId,
            TerritoryName = territoryName,
            DataId = marker.DataId,
            EventId = marker.EventId,
            Name = marker.Label,
            X = marker.Position.X,
            Y = marker.Position.Y,
            Z = marker.Position.Z,
            IsActive = marker.IsActive,
            Properties = new Dictionary<string, string>
            {
                ["objectKind"] = marker.Kind == AtlasMarkerKind.ActiveTreasure
                    ? nameof(ObjectKind.Treasure)
                    : nameof(ObjectKind.EventObj),
            },
        };

    private static string DisplayName(IGameObject gameObject, string fallback)
    {
        var name = gameObject.Name.ToString().Trim();
        return string.IsNullOrWhiteSpace(name) ? fallback : name;
    }

    /// <summary>
    /// EventId has moved between Dalamud wrappers over time. Read it opportunistically without
    /// binding the collector to a native struct or persisting a pointer.
    /// </summary>
    private static uint TryReadEventId(IGameObject gameObject)
    {
        var property = gameObject.GetType().GetProperty("EventId")
                       ?? gameObject.GetType().GetProperty("EventID");
        if (property?.GetValue(gameObject) is not { } value)
            return 0;

        try
        {
            return Convert.ToUInt32(value, CultureInfo.InvariantCulture);
        }
        catch (FormatException)
        {
            return 0;
        }
        catch (InvalidCastException)
        {
            return 0;
        }
        catch (OverflowException)
        {
            return 0;
        }
    }
}
