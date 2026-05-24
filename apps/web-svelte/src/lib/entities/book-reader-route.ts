export type BookReaderCommand = "resume" | "start-over";

export function bookReaderCommand(url: URL): BookReaderCommand | null {
  const command = url.searchParams.get("reader");
  return command === "resume" || command === "start-over" ? command : null;
}

export function hrefWithoutBookReaderCommand(url: URL): string | null {
  if (!url.searchParams.has("reader")) return null;

  const searchParams = new URLSearchParams(url.searchParams);
  searchParams.delete("reader");
  const search = searchParams.toString();

  return `${url.pathname}${search ? `?${search}` : ""}${url.hash}`;
}
