import { expect } from "@playwright/test";

/**
 * The emails Api sent during the run, read from the Mailpit inbox the
 * end-to-end stack starts (its address comes from globalSetup).
 */

interface MailpitSummary {
  ID: string;
}

interface MailpitMessage {
  Subject: string;
  Text: string;
}

function mailUrl(): string {
  const url = process.env.E2E_MAIL_URL;
  if (!url) throw new Error("No mail server: run the tests with npm run test:e2e.");
  return url;
}

/** The newest email to `address`, or null when none has arrived yet. */
async function newestEmailTo(address: string): Promise<MailpitMessage | null> {
  const query = encodeURIComponent(`to:"${address}"`);
  const search = await fetch(`${mailUrl()}/api/v1/search?query=${query}&limit=1`);
  const { messages } = (await search.json()) as { messages: MailpitSummary[] };
  const newest = messages[0];
  if (!newest) return null;
  const message = await fetch(`${mailUrl()}/api/v1/message/${newest.ID}`);
  return (await message.json()) as MailpitMessage;
}

/** Waits for the password reset email to `address` and returns the link in it. */
export async function resetLinkSentTo(address: string): Promise<string> {
  let link: string | undefined;
  await expect
    .poll(
      async () => {
        const email = await newestEmailTo(address);
        link = /https?:\/\/\S+\/reset-password\?\S+/.exec(email?.Text ?? "")?.[0];
        return link;
      },
      { message: `a reset email to ${address}` },
    )
    .toBeDefined();
  return link ?? "";
}
