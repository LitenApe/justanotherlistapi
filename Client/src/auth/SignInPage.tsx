import { useEffect, useId, useRef, useState } from "react";
import { Form, redirect, useNavigation } from "react-router";
import { DocumentTitle } from "../components/DocumentTitle";
import { Icon } from "../components/Icon";
import { useToast } from "../components/Toasts";
import { formText } from "../lib/formText";
import { isGuid, newGuid, normalizeGuid } from "../lib/guid";
import type { Route } from "./+types/SignInPage";
import { safeRedirect } from "./redirects";
import { sessionStore } from "./session";
import { DEFAULT_USER_ID, requestSession } from "./signIn";
import styles from "./SignInPage.module.css";

const LAST_USER_KEY = "jal.lastUserId";

const returnPath = (request: Request) =>
  safeRedirect(new URL(request.url).searchParams.get("redirectTo"));

export function clientLoader({ request }: Route.ClientLoaderArgs) {
  if (sessionStore.getSession()) throw redirect(returnPath(request));
  return {
    expired: sessionStore.getSignOutReason() === "expired",
    lastUserId: readLastUserId(),
  };
}

export async function clientAction({ request }: Route.ClientActionArgs) {
  const userId = formText(await request.formData(), "userId");
  if (!isGuid(userId)) {
    return {
      fieldError: "Enter a user ID in GUID format, like 00000000-0000-0000-0000-000000000001.",
    };
  }

  try {
    const session = await requestSession(normalizeGuid(userId));
    rememberUserId(session.userId);
    sessionStore.setSession(session);
  } catch (error) {
    return { formError: error instanceof Error ? error.message : "Sign-in failed." };
  }
  return redirect(returnPath(request));
}

export default function SignInPage({ loaderData, actionData }: Route.ComponentProps) {
  const navigation = useNavigation();
  const pending = navigation.formData !== undefined;
  const { announce } = useToast();
  const inputRef = useRef<HTMLInputElement>(null);
  const hintId = useId();
  const errorId = useId();

  const [userId, setUserId] = useState(loaderData.lastUserId);
  // Generating a new ID clears the error shown for the previous attempt.
  const [clearedResult, setClearedResult] = useState<typeof actionData>(undefined);
  const result = actionData === clearedResult ? undefined : actionData;
  const fieldError = result && "fieldError" in result ? result.fieldError : null;
  const formError = result && "formError" in result ? result.formError : null;

  useEffect(() => {
    if (actionData && "fieldError" in actionData) inputRef.current?.focus();
  }, [actionData]);

  return (
    <div className={styles.page}>
      <DocumentTitle title="Sign in" />
      <div className={`card ${styles.panel}`}>
        <h1 tabIndex={-1}>Sign in</h1>
        <p className="muted">
          Your user ID is who you are in JustAnotherList. Use the same ID to get back to your lists,
          or a new one to try sharing lists between people. Each browser tab keeps its own sign-in.
        </p>

        {loaderData.expired && (
          <p className="notice" role="status">
            Your session expired. Sign in again to continue.
          </p>
        )}

        {formError && (
          <div className="alert" role="alert">
            <Icon name="alert" />
            <p>{formError}</p>
          </div>
        )}

        <Form
          method="post"
          className={styles.form}
          noValidate
          onSubmit={(event) => {
            if (pending) event.preventDefault();
          }}
        >
          <div className="field">
            <label className="label" htmlFor="user-id">
              User ID
            </label>
            <p className="hint" id={hintId}>
              A GUID, for example {DEFAULT_USER_ID}.
            </p>
            <input
              ref={inputRef}
              id="user-id"
              name="userId"
              className={`input ${styles.mono}`}
              value={userId}
              onChange={(event) => {
                setUserId(event.target.value);
              }}
              autoComplete="username"
              autoCapitalize="none"
              spellCheck={false}
              required
              aria-invalid={fieldError ? true : undefined}
              aria-describedby={fieldError ? `${hintId} ${errorId}` : hintId}
            />
            {fieldError && (
              <p className="field-error" id={errorId}>
                {fieldError}
              </p>
            )}
          </div>

          <button
            type="button"
            className={`button button-quiet ${styles.generate}`}
            onClick={() => {
              setUserId(newGuid());
              setClearedResult(actionData);
              announce("Generated a new user ID.");
            }}
          >
            Generate a new user ID
          </button>

          <button type="submit" className="button button-primary" aria-disabled={pending}>
            {pending ? "Signing in…" : "Sign in"}
          </button>
        </Form>
      </div>
    </div>
  );
}

function readLastUserId(): string {
  try {
    return localStorage.getItem(LAST_USER_KEY) ?? DEFAULT_USER_ID;
  } catch {
    return DEFAULT_USER_ID;
  }
}

function rememberUserId(userId: string): void {
  try {
    localStorage.setItem(LAST_USER_KEY, userId);
  } catch {
    // Not essential.
  }
}
