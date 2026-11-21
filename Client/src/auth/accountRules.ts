/**
 * The account rules the server applies (specifications/accounts.md#rules), so
 * a form can explain a problem next to its field before sending anything. The
 * server checks them again.
 */
export const NAME_MAX_LENGTH = 50;
export const EMAIL_MAX_LENGTH = 254;
export const PASSWORD_MIN_LENGTH = 12;
export const PASSWORD_MAX_LENGTH = 128;

/** A name is trimmed, then 1 to 50 characters. */
export function nameProblem(name: string): string | null {
  const trimmed = name.trim();
  if (!trimmed) return "Enter your name.";
  if (trimmed.length > NAME_MAX_LENGTH) {
    return `Use at most ${NAME_MAX_LENGTH.toString()} characters.`;
  }
  return null;
}

/** One @ with text on both sides, no whitespace, at most 254 characters, once trimmed. */
export function isEmailAddress(value: string): boolean {
  const email = value.trim();
  const at = email.indexOf("@");
  return (
    email.length <= EMAIL_MAX_LENGTH &&
    at > 0 &&
    at < email.length - 1 &&
    !email.includes("@", at + 1) &&
    !/\s/.test(email)
  );
}

export function emailProblem(email: string): string | null {
  if (!email.trim()) return "Enter your email address.";
  return isEmailAddress(email) ? null : "Enter an email address, like name@example.com.";
}

/** A new password is 12 to 128 characters, exactly as typed. */
export function newPasswordProblem(password: string): string | null {
  if (password.length < PASSWORD_MIN_LENGTH) {
    return `Use at least ${PASSWORD_MIN_LENGTH.toString()} characters.`;
  }
  if (password.length > PASSWORD_MAX_LENGTH) {
    return `Use at most ${PASSWORD_MAX_LENGTH.toString()} characters.`;
  }
  return null;
}
