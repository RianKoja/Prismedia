import { readdirSync, readFileSync, statSync } from "node:fs";
import { join } from "node:path";
import { describe, expect, it } from "vitest";

function svelteFiles(root: string): string[] {
  const entries = readdirSync(root);
  const files: string[] = [];

  for (const entry of entries) {
    const path = join(root, entry);
    if (statSync(path).isDirectory()) {
      files.push(...svelteFiles(path));
    } else if (path.endsWith(".svelte")) {
      files.push(path);
    }
  }

  return files;
}

describe("EntityGrid selection affordance", () => {
  it("keeps full EntityGrid surfaces selectable by default", () => {
    const roots = ["src/routes", "src/lib/components"];
    const gridOptOuts = roots
      .flatMap(svelteFiles)
      .filter((path) => !path.endsWith("EntityGrid.svelte"))
      .flatMap((path) => {
        const source = readFileSync(path, "utf8");
        const grids = source.match(/<EntityGrid\b[\s\S]*?(?:\/>|<\/EntityGrid>)/g) ?? [];
        return grids
          .filter((grid) => /\bselectable\s*=\s*\{(?!true\s*\})/.test(grid))
          .map((grid) => `${path}: ${grid.split("\n")[0].trim()}`);
      });

    expect(gridOptOuts).toEqual([]);
  });
});
