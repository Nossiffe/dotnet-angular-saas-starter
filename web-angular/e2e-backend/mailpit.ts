import { expect, type APIRequestContext } from '@playwright/test';

/** Mailpit's web API: the port of docker-compose.yml (MAILPIT_UI_PORT, 8025 by default). */
const MAILPIT_URL = 'http://localhost:8025';

interface MessageSummary {
  ID: string;
  Subject: string;
}

/**
 * The link of the e-mail sent to `recipient` whose path is `path` (`/confirm-email`,
 * `/accept-invitation`). The confirmation is sent during the registration request; the
 * invitation goes through the outbox, read every 5 seconds, and arrives a few seconds after the
 * API has answered. Mailpit is polled until the e-mail arrives, 30 seconds at most, never with a
 * fixed wait.
 */
export async function linkFromEmail(
  request: APIRequestContext,
  recipient: string,
  path: string,
): Promise<string> {
  let messages: MessageSummary[] = [];
  await expect
    .poll(
      async () => {
        const search = await request.get(`${MAILPIT_URL}/api/v1/search`, {
          params: { query: `to:"${recipient}"` },
        });
        expect(search.ok()).toBe(true);
        messages = ((await search.json()) as { messages: MessageSummary[] }).messages;
        return messages.length;
      },
      { message: `an e-mail to ${recipient} in Mailpit`, timeout: 30_000 },
    )
    .toBeGreaterThan(0);

  const message = await request.get(`${MAILPIT_URL}/api/v1/message/${messages[0]!.ID}`);
  expect(message.ok()).toBe(true);
  const { Text } = (await message.json()) as { Text: string };
  // The text part gives the link in clear, between parentheses: « Bouton ( http://… ) ».
  const link = new RegExp(`https?://\\S+${path}\\?[^\\s)]+`).exec(Text)?.[0];
  expect(link, `a ${path} link in the e-mail to ${recipient}`).toBeDefined();
  return link!;
}
