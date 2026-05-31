using System.Text.Json.Serialization;

namespace Prismedia.Contracts.Jellyfin;

/// <summary>Minimal Jellyfin-compatible media source embedded in catalog item DTOs.</summary>
public sealed record JellyfinCatalogMediaSourceDto {
    [JsonPropertyName("Id")]
    public required string Id { get; init; }

    [JsonPropertyName("Path")]
    public required string Path { get; init; }

    [JsonPropertyName("Protocol")]
    public string Protocol { get; init; } = "File";

    [JsonPropertyName("Type")]
    public string Type { get; init; } = "Default";

    [JsonPropertyName("Container")]
    public string? Container { get; init; }

    [JsonPropertyName("Size")]
    public long? Size { get; init; }

    [JsonPropertyName("Name")]
    public string? Name { get; init; }

    [JsonPropertyName("ETag")]
    public string? ETag { get; init; }

    [JsonPropertyName("RunTimeTicks")]
    public long? RunTimeTicks { get; init; }

    [JsonPropertyName("Bitrate")]
    public int? Bitrate { get; init; }

    [JsonPropertyName("VideoType")]
    public string VideoType { get; init; } = "VideoFile";

    [JsonPropertyName("IsRemote")]
    public bool IsRemote { get; init; }

    [JsonPropertyName("ReadAtNativeFramerate")]
    public bool ReadAtNativeFramerate { get; init; }

    [JsonPropertyName("IgnoreDts")]
    public bool IgnoreDts { get; init; }

    [JsonPropertyName("IgnoreIndex")]
    public bool IgnoreIndex { get; init; }

    [JsonPropertyName("GenPtsInput")]
    public bool GenPtsInput { get; init; }

    [JsonPropertyName("SupportsTranscoding")]
    public bool SupportsTranscoding { get; init; } = true;

    [JsonPropertyName("SupportsDirectStream")]
    public bool SupportsDirectStream { get; init; } = true;

    [JsonPropertyName("SupportsDirectPlay")]
    public bool SupportsDirectPlay { get; init; } = true;

    [JsonPropertyName("IsInfiniteStream")]
    public bool IsInfiniteStream { get; init; }

    [JsonPropertyName("UseMostCompatibleTranscodingProfile")]
    public bool UseMostCompatibleTranscodingProfile { get; init; }

    [JsonPropertyName("RequiresOpening")]
    public bool RequiresOpening { get; init; }

    [JsonPropertyName("RequiresClosing")]
    public bool RequiresClosing { get; init; }

    [JsonPropertyName("RequiresLooping")]
    public bool RequiresLooping { get; init; }

    [JsonPropertyName("SupportsProbing")]
    public bool SupportsProbing { get; init; } = true;

    [JsonPropertyName("DefaultAudioStreamIndex")]
    public int? DefaultAudioStreamIndex { get; init; }

    [JsonPropertyName("MediaStreams")]
    public IReadOnlyList<JellyfinCatalogMediaStreamDto> MediaStreams { get; init; } = [];

    [JsonPropertyName("MediaAttachments")]
    public IReadOnlyList<object> MediaAttachments { get; init; } = [];

    [JsonPropertyName("Formats")]
    public IReadOnlyList<object> Formats { get; init; } = [];

    [JsonPropertyName("RequiredHttpHeaders")]
    public IReadOnlyDictionary<string, string> RequiredHttpHeaders { get; init; } = new Dictionary<string, string>();

    [JsonPropertyName("HasSegments")]
    public bool HasSegments { get; init; }
}

/// <summary>Minimal Jellyfin-compatible media stream embedded in catalog item DTOs.</summary>
public sealed record JellyfinCatalogMediaStreamDto {
    [JsonPropertyName("Index")]
    public int Index { get; init; }

    [JsonPropertyName("Type")]
    public required string Type { get; init; }

    [JsonPropertyName("Codec")]
    public string? Codec { get; init; }

    [JsonPropertyName("Language")]
    public string? Language { get; init; }

    [JsonPropertyName("DisplayTitle")]
    public string? DisplayTitle { get; init; }

    [JsonPropertyName("Width")]
    public int? Width { get; init; }

    [JsonPropertyName("Height")]
    public int? Height { get; init; }

    [JsonPropertyName("AverageFrameRate")]
    public double? AverageFrameRate { get; init; }

    [JsonPropertyName("RealFrameRate")]
    public double? RealFrameRate { get; init; }

    [JsonPropertyName("BitRate")]
    public int? BitRate { get; init; }

    [JsonPropertyName("Channels")]
    public int? Channels { get; init; }

    [JsonPropertyName("ChannelLayout")]
    public string? ChannelLayout { get; init; }

    [JsonPropertyName("SampleRate")]
    public int? SampleRate { get; init; }

    [JsonPropertyName("AspectRatio")]
    public string? AspectRatio { get; init; }

    [JsonPropertyName("VideoRange")]
    public string? VideoRange { get; init; }

    [JsonPropertyName("VideoRangeType")]
    public string? VideoRangeType { get; init; }

    [JsonPropertyName("IsDefault")]
    public bool IsDefault { get; init; } = true;

    [JsonPropertyName("IsForced")]
    public bool IsForced { get; init; }

    [JsonPropertyName("IsExternal")]
    public bool IsExternal { get; init; }
}

