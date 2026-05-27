import { describe, expect, it } from "vitest";

import type { EntitySearchCandidate } from "$lib/api/generated/model";

import {
	identifyCandidateKey,
	identifyCandidateToThumbnailCard,
} from "./identify-candidate-card";

describe("identify candidate cards", () => {
	it("maps search candidates into list thumbnail cards with poster and provider details", () => {
		const candidate: EntitySearchCandidate = {
			externalIds: {
				imdb: "tt1234567",
				tmdb: "271267",
			},
			overview: "A family man investigates a far-reaching conspiracy.",
			popularity: 42.84,
			posterUrl: "https://image.tmdb.org/t/p/w500/poster.jpg",
			title: "The Chair Company",
			year: 2025,
		};

		const card = identifyCandidateToThumbnailCard(candidate, "video-series", 0);

		expect(card.entity.id).toBe("tmdb:271267");
		expect(card.entity.kind).toBe("video-series");
		expect(card.cover?.src).toBe(candidate.posterUrl);
		expect(card.cover?.alt).toBe(candidate.title);
		expect(card.aspectRatio).toBe("poster");
		expect(card.subtitle).toBe("2025");
		expect(card.meta?.map((item) => item.label)).toEqual([
			"tmdb: 271267",
			"imdb: tt1234567",
			"pop 42.8",
		]);
	});

	it("falls back to a stable candidate key when provider ids are missing", () => {
		const candidate: EntitySearchCandidate = {
			externalIds: {},
			overview: null,
			popularity: null,
			posterUrl: null,
			title: "Friendship",
			year: 2025,
		};

		expect(identifyCandidateKey(candidate, 3)).toBe("candidate:friendship:2025:3");
	});
});
