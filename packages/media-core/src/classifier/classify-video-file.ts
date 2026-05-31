import path from "node:path";
import { parseEpisodeFilename } from "../parsing/parse-episode-filename";
import { parseSeasonFolder } from "../parsing/parse-season-folder";
import type {
  LibraryClassificationConfig,
  VideoClassification,
} from "./types";

/**
 * Classify a video file into a typed entity destination based on its
 * depth under the library root and the library's scan toggles.
 *
 * This function is pure and synchronous. It reads no files and makes
 * no database calls. Movie folder detection depends on the caller providing
 * the precomputed same-named, single-file folder paths in `movieFolderPaths`.
 */
export function classifyVideoFile(
  filePath: string,
  config: LibraryClassificationConfig,
): VideoClassification {
  const normalizedRoot = path.resolve(config.libraryRootPath);
  const normalizedFile = path.resolve(filePath);

  const relative = path.relative(normalizedRoot, normalizedFile);
  if (relative.startsWith("..") || path.isAbsolute(relative)) {
    return {
      kind: "rejected",
      filePath,
      reason: "file is not under the library root path",
    };
  }

  const segments = relative.split(path.sep).filter((s) => s.length > 0);
  // segments.length === 1 → depth 0 (at root)
  // segments.length === 2 → depth 1 (inside series folder)
  // segments.length === 3 → depth 2 (inside season folder)
  // segments.length >= 4 → depth 3+ (rejected)
  const depth = segments.length - 1;

  if (depth >= 3) {
    return {
      kind: "rejected",
      filePath,
      reason: `file depth is ${depth} folders deep; the maximum is 2 (library → series → season → file)`,
    };
  }

  if (depth === 0) {
    return {
      kind: "video",
      filePath: normalizedFile,
      libraryRootPath: normalizedRoot,
    };
  }

  const seriesFolderName = segments[0];
  const seriesFolderPath = path.join(normalizedRoot, seriesFolderName);

  if (depth === 1) {
    const movieFolderPaths = new Set(
      (config.movieFolderPaths ?? []).map((folderPath) => path.resolve(folderPath)),
    );
    if (
      movieFolderPaths.has(seriesFolderPath) &&
      looksLikeMovieFileForFolder(
        seriesFolderName,
        path.basename(normalizedFile, path.extname(normalizedFile)),
      )
    ) {
      return {
        kind: "movie",
        filePath: normalizedFile,
        libraryRootPath: normalizedRoot,
        movieFolderPath: seriesFolderPath,
        movieFolderName: seriesFolderName,
      };
    }

    return {
      kind: "episode",
      filePath: normalizedFile,
      libraryRootPath: normalizedRoot,
      seriesFolderPath,
      seriesFolderName,
      seasonFolderPath: null,
      seasonFolderName: null,
      placementSeasonNumber: 0,
    };
  }

  // depth === 2
  const seasonFolderName = segments[1];
  const seasonFolderPath = path.join(
    normalizedRoot,
    seriesFolderName,
    seasonFolderName,
  );
  const parsed = parseSeasonFolder(seasonFolderName);
  const placementSeasonNumber = parsed.seasonNumber ?? 0;

  return {
    kind: "episode",
    filePath: normalizedFile,
    libraryRootPath: normalizedRoot,
    seriesFolderPath,
    seriesFolderName,
    seasonFolderPath,
    seasonFolderName,
    placementSeasonNumber,
  };
}

function looksLikeMovieFileForFolder(folderName: string, fileTitle: string): boolean {
  if (parseEpisodeFilename(fileTitle).seasonNumber != null) {
    return false;
  }

  if (fileTitle.toLowerCase() === folderName.toLowerCase()) {
    return true;
  }

  if (!fileTitle.toLowerCase().startsWith(folderName.toLowerCase())) {
    return false;
  }

  const suffix = fileTitle.slice(folderName.length);
  return suffix.length > 0 && (["-", "_", "."].includes(suffix[0]) || /^\s/.test(suffix));
}
