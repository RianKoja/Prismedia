import { describe, expect, it } from "vitest";
import { bookReaderCommand, hrefWithoutBookReaderCommand } from "./book-reader-route";

describe("book reader route helpers", () => {
  it("recognizes only supported reader auto-open commands", () => {
    expect(bookReaderCommand(new URL("http://localhost/books/1/chapters/2?reader=resume"))).toBe(
      "resume",
    );
    expect(
      bookReaderCommand(new URL("http://localhost/books/1/chapters/2?reader=start-over")),
    ).toBe("start-over");
    expect(
      bookReaderCommand(new URL("http://localhost/books/1/chapters/2?reader=close")),
    ).toBeNull();
  });

  it("removes the reader auto-open command without dropping other URL state", () => {
    expect(
      hrefWithoutBookReaderCommand(
        new URL("http://localhost/books/1/chapters/2?reader=resume&tab=details#pages"),
      ),
    ).toBe("/books/1/chapters/2?tab=details#pages");
    expect(hrefWithoutBookReaderCommand(new URL("http://localhost/books/1/chapters/2"))).toBeNull();
  });
});
