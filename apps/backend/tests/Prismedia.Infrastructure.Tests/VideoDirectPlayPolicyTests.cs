using Prismedia.Application.Videos;

namespace Prismedia.Infrastructure.Tests;

public sealed class VideoDirectPlayPolicyTests {
    // A client (Infuse / Apple TV class) that can directly play HEVC/H.264 and EAC3/Atmos in MKV.
    private static readonly ClientPlaybackProfile CapableClient = new(
        MaxStreamingBitrate: 200_000_000,
        DirectPlayProfiles:
        [
            new ClientDirectPlayProfile("Video", "mkv,mp4,ts,mov", "hevc,h264,av1", "eac3,ac3,aac,truehd,dts")
        ]);

    private static VideoSourceFile Source(
        string path,
        string container,
        string videoCodec,
        string? audioCodec,
        bool directPlayable = false,
        int? bitRate = null) =>
        new(
            Guid.NewGuid(),
            path,
            "video/x-matroska",
            directPlayable,
            Container: container,
            BitRate: bitRate,
            VideoCodec: videoCodec,
            AudioCodec: audioCodec);

    [Fact]
    public void CapableClientDirectPlaysDolbyVisionHevcAtmosMkv() {
        var source = Source("/media/e02.mkv", "matroska", "hevc", "eac3");

        var decision = VideoDirectPlayPolicy.Decide(
            source,
            selectedAudioCodec: "eac3",
            range: VideoPlaybackRange.Dovi,
            profile: CapableClient,
            supportedVideoRangeTypes: ["DOVI", "DOVIWithHDR10", "HDR10"],
            directPlayAllowed: true,
            directStreamAllowed: true,
            transcodingAllowed: true);

        Assert.Equal(VideoPlaybackMethod.DirectPlay, decision.Method);
    }

    [Fact]
    public void DolbyVisionRemuxesToAnHevcClientEvenWithoutAdvertisedRangeSupport() {
        var source = Source("/media/e02.mkv", "matroska", "hevc", "eac3");

        // A Dolby Vision MKV to a client that decodes HEVC: the container (mkv) is not directly
        // playable, but the video is copied unchanged into fMP4 — no tone map — so the client renders
        // the HDR/DV stream itself. The client need not advertise the dynamic range for a copy.
        var decision = VideoDirectPlayPolicy.Decide(
            source,
            selectedAudioCodec: "eac3",
            range: VideoPlaybackRange.Dovi,
            profile: CapableClient,
            supportedVideoRangeTypes: ["SDR", "HDR10"],
            directPlayAllowed: true,
            directStreamAllowed: true,
            transcodingAllowed: true);

        Assert.Equal(VideoPlaybackMethod.Remux, decision.Method);
    }

    [Fact]
    public void DolbyVisionTranscodesWhenClientCannotDecodeTheVideoCodec() {
        var source = Source("/media/e02.mkv", "matroska", "hevc", "eac3");

        // A browser-class client that can only decode H.264 cannot copy the HEVC stream, so the only
        // option is a full transcode (which tone-maps the HDR/DV down to SDR H.264).
        var h264OnlyClient = new ClientPlaybackProfile(
            MaxStreamingBitrate: null,
            DirectPlayProfiles: [new ClientDirectPlayProfile("Video", "mp4", "h264", "aac")]);

        var decision = VideoDirectPlayPolicy.Decide(
            source,
            selectedAudioCodec: "eac3",
            range: VideoPlaybackRange.Dovi,
            profile: h264OnlyClient,
            supportedVideoRangeTypes: ["SDR", "HDR10"],
            directPlayAllowed: true,
            directStreamAllowed: true,
            transcodingAllowed: true);

        Assert.Equal(VideoPlaybackMethod.Transcode, decision.Method);
    }

    [Fact]
    public void RemuxesWhenVideoCodecPlayableButContainerUnsupported() {
        // Client plays HEVC only in mp4 and only AAC audio.
        var profile = new ClientPlaybackProfile(
            MaxStreamingBitrate: null,
            DirectPlayProfiles: [new ClientDirectPlayProfile("Video", "mp4", "hevc", "aac")]);
        var source = Source("/media/clip.mkv", "matroska", "hevc", "eac3");

        var decision = VideoDirectPlayPolicy.Decide(
            source,
            selectedAudioCodec: "eac3",
            range: VideoPlaybackRange.Sdr,
            profile: profile,
            supportedVideoRangeTypes: null,
            directPlayAllowed: true,
            directStreamAllowed: true,
            transcodingAllowed: true);

        Assert.Equal(VideoPlaybackMethod.Remux, decision.Method);
        Assert.Equal("mp4", decision.RemuxContainer);
        Assert.False(decision.CopyAudio); // EAC3 not in the client's audio list, so audio is transcoded
    }

    [Fact]
    public void RemuxCopiesAudioWhenClientAlsoSupportsTheAudioCodec() {
        var profile = new ClientPlaybackProfile(
            MaxStreamingBitrate: null,
            DirectPlayProfiles: [new ClientDirectPlayProfile("Video", "mp4", "hevc", "aac,eac3")]);
        var source = Source("/media/clip.mkv", "matroska", "hevc", "eac3");

        var decision = VideoDirectPlayPolicy.Decide(
            source,
            selectedAudioCodec: "eac3",
            range: VideoPlaybackRange.Sdr,
            profile: profile,
            supportedVideoRangeTypes: null,
            directPlayAllowed: true,
            directStreamAllowed: true,
            transcodingAllowed: true);

        Assert.Equal(VideoPlaybackMethod.Remux, decision.Method);
        Assert.True(decision.CopyAudio);
    }

    [Fact]
    public void BitrateCeilingForcesTranscode() {
        var profile = new ClientPlaybackProfile(
            MaxStreamingBitrate: 8_000_000,
            DirectPlayProfiles: [new ClientDirectPlayProfile("Video", "mkv", "hevc", "eac3")]);
        var source = Source("/media/e02.mkv", "matroska", "hevc", "eac3", bitRate: 40_000_000);

        var decision = VideoDirectPlayPolicy.Decide(
            source,
            selectedAudioCodec: "eac3",
            range: VideoPlaybackRange.Sdr,
            profile: profile,
            supportedVideoRangeTypes: null,
            directPlayAllowed: true,
            directStreamAllowed: true,
            transcodingAllowed: true);

        Assert.Equal(VideoPlaybackMethod.Transcode, decision.Method);
    }

    [Theory]
    [InlineData(true, VideoPlaybackMethod.DirectPlay)]
    [InlineData(false, VideoPlaybackMethod.Transcode)]
    public void WithoutDeviceProfileFallsBackToContainerExtensionHeuristic(
        bool directPlayable,
        VideoPlaybackMethod expected) {
        var source = Source("/media/clip.ext", "matroska", "h264", "aac", directPlayable: directPlayable);

        var decision = VideoDirectPlayPolicy.Decide(
            source,
            selectedAudioCodec: "aac",
            range: VideoPlaybackRange.Sdr,
            profile: null,
            supportedVideoRangeTypes: null,
            directPlayAllowed: true,
            directStreamAllowed: true,
            transcodingAllowed: true);

        Assert.Equal(expected, decision.Method);
    }
}
