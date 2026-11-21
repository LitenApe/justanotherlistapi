import type { Member } from "../api/types";

/** The start of a user ID, for telling members apart when names don't. */
const shortId = (memberId: string) => `${memberId.slice(0, 8)}…`;

const sameName = (a: string, b: string) =>
  a.localeCompare(b, undefined, { sensitivity: "accent" }) === 0;

/**
 * What to call a member of the list: their name, or the start of their user ID
 * when they have none (members added before accounts existed). Names don't
 * have to be unique, so when another member of the list shares the name,
 * ignoring case, both get the start of their user ID after it:
 * "Alex (3fa85f64…)".
 */
export function memberName(memberId: string, members: readonly Member[]): string {
  const name = members.find((member) => member.id === memberId)?.name;
  if (!name) return shortId(memberId);
  const shared = members.some(
    (member) => member.id !== memberId && member.name !== null && sameName(member.name, name),
  );
  return shared ? `${name} (${shortId(memberId)})` : name;
}

/** How a member is mentioned in a sentence: "you", or their name. */
export function memberLabel(memberId: string, userId: string, members: readonly Member[]): string {
  return memberId === userId ? "you" : memberName(memberId, members);
}

/** You first, then everyone else by name. */
export function sortMembers(members: readonly Member[], userId: string): Member[] {
  return members.toSorted((a, b) => {
    if (a.id === userId) return -1;
    if (b.id === userId) return 1;
    return memberName(a.id, members).localeCompare(memberName(b.id, members));
  });
}
