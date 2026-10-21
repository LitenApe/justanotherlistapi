import axe from "axe-core";
import { expect } from "vitest";

/** The WCAG 2.2 A and AA rules, including colour contrast. */
const WCAG_TAGS = ["wcag2a", "wcag2aa", "wcag21a", "wcag21aa", "wcag22aa"];

/**
 * Browser tests: fails with a readable list of axe-core violations. Runs in a
 * real browser, so colour contrast is checked against the actual styles.
 * Page-level rules (landmarks, one <h1>) belong to the integration tests.
 */
export async function expectNoAxeViolations(context: Element = document.body) {
  // Mid-animation (a dialog fading in) colours are partly transparent, which
  // would fail the contrast check for a state nobody actually reads.
  await Promise.all(document.getAnimations().map((animation) => animation.finished));
  const results = await axe.run(context, { runOnly: { type: "tag", values: WCAG_TAGS } });
  const summary = results.violations.map(
    (violation) =>
      `${violation.id}: ${violation.help} (${violation.nodes
        .map((node) => node.target.join(" "))
        .join(", ")})`,
  );
  expect(summary).toEqual([]);
}
