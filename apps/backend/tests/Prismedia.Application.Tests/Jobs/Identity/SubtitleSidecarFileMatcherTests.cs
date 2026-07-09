using Prismedia.Application.Jobs.Handlers.Identity;

namespace Prismedia.Application.Tests.Jobs.Identity;

/// <summary>
/// Covers sidecar subtitle file discovery: matching "Movie.srt" / "Movie.en.srt" /
/// "Movie.pt-BR.srt" / "Movie.ai_translated.srt" beside "Movie.mkv", and resolving each
/// tag to a language (via .NET's own ISO 639/BCP-47 culture database) or a free-text label.
/// </summary>
public sealed class SubtitleSidecarFileMatcherTests : IDisposable {
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"prismedia-sidecar-matcher-{Guid.NewGuid():N}");

    public SubtitleSidecarFileMatcherTests() {
        Directory.CreateDirectory(_dir);
    }

    public void Dispose() {
        if (Directory.Exists(_dir)) {
            Directory.Delete(_dir, recursive: true);
        }
    }

    [Fact]
    public void PlainCoNamedFileHasNoLanguageOrLabel() {
        var video = CreateFile("Movie.mkv");
        CreateFile("Movie.srt");

        var candidate = Assert.Single(SubtitleSidecarFileMatcher.FindCandidates(video));
        Assert.Equal("srt", candidate.Extension);
        Assert.Equal("und", candidate.Language);
        Assert.Null(candidate.Label);
    }

    [Theory]
    [InlineData("Movie.en.srt", "en")]
    [InlineData("Movie.eng.srt", "en")] // 3-letter ISO 639-2, common from ffmpeg/mkvmerge tagging.
    [InlineData("Movie.pt-BR.srt", "pt-BR")]
    [InlineData("Movie.pt_br.srt", "pt-BR")] // underscore variant normalizes the same as a hyphen.
    [InlineData("Movie.FR.srt", "fr")] // case-insensitive.
    public void RecognizedLanguageTagsResolveToTheirCultureCode(string fileName, string expectedLanguage) {
        var video = CreateFile("Movie.mkv");
        CreateFile(fileName);

        var candidate = Assert.Single(SubtitleSidecarFileMatcher.FindCandidates(video));
        Assert.Equal(expectedLanguage, candidate.Language);
        Assert.Null(candidate.Label);
    }

    [Fact]
    public void UnrecognizedTagBecomesAHumanizedLabelInsteadOfALanguage() {
        var video = CreateFile("Movie.mkv");
        CreateFile("Movie.ai_translated.srt");

        var candidate = Assert.Single(SubtitleSidecarFileMatcher.FindCandidates(video));
        Assert.Equal("und", candidate.Language);
        Assert.Equal("Ai Translated", candidate.Label);
    }

    [Fact]
    public void CombinedLanguageAndDescriptiveTagsResolveBoth() {
        var video = CreateFile("Movie.mkv");
        CreateFile("Movie.pt-BR.ai_translated.srt");

        var candidate = Assert.Single(SubtitleSidecarFileMatcher.FindCandidates(video));
        Assert.Equal("pt-BR", candidate.Language);
        Assert.Equal("Ai Translated", candidate.Label);
    }

    [Fact]
    public void SimilarlyNamedFilesWithoutADotSeparatorAreNotMatched() {
        var video = CreateFile("Movie.mkv");
        CreateFile("Movie 2.srt");

        Assert.Empty(SubtitleSidecarFileMatcher.FindCandidates(video));
    }

    [Fact]
    public void UnsupportedExtensionsAreIgnored() {
        var video = CreateFile("Movie.mkv");
        CreateFile("Movie.nfo");
        CreateFile("Movie.jpg");

        Assert.Empty(SubtitleSidecarFileMatcher.FindCandidates(video));
    }

    [Fact]
    public void AllSupportedExtensionsAreDiscoveredCaseInsensitively() {
        var video = CreateFile("Movie.mkv");
        CreateFile("Movie.en.SRT");
        CreateFile("Movie.fr.vtt");
        CreateFile("Movie.de.ass");
        CreateFile("Movie.es.ssa");

        var candidates = SubtitleSidecarFileMatcher.FindCandidates(video);
        Assert.Equal(4, candidates.Count);
        Assert.Equal(["ass", "srt", "ssa", "vtt"], candidates.Select(c => c.Extension).OrderBy(e => e));
    }

    private string CreateFile(string name) {
        var path = Path.Combine(_dir, name);
        File.WriteAllText(path, "content");
        return path;
    }
}
