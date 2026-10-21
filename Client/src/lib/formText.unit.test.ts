import { describe, expect, it } from "vitest";
import { formText } from "./formText";

describe("formText", () => {
  it("reads text fields and treats missing fields and files as empty", () => {
    const form = new FormData();
    form.set("name", "Groceries");
    form.set("upload", new File(["x"], "x.txt"));

    expect(formText(form, "name")).toBe("Groceries");
    expect(formText(form, "missing")).toBe("");
    expect(formText(form, "upload")).toBe("");
  });
});
