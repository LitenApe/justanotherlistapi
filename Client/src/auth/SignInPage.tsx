import { useId, useRef, useState, type SubmitEvent } from "react";
import { Navigate, useLocation, useNavigate } from "react-router";
import { Icon } from "../components/Icon";
import { useToast } from "../components/Toasts";
import { useDocumentTitle } from "../components/useDocumentTitle";
import { isGuid, newGuid, normalizeGuid } from "../lib/guid";
import { sessionStore } from "./session";
import { DEFAULT_USER_ID, requestSession } from "./signIn";
import { useSession } from "./useSession";
import styles from "./SignInPage.module.css";

const LAST_USER_KEY = "jal.lastUserId";

export function SignInPage() {
  useDocumentTitle("Sign in");
  const session = useSession();
  const location = useLocation();
  const navigate = useNavigate();
  const { announce } = useToast();
  const inputRef = useRef<HTMLInputElement>(null);
  const hintId = useId();
  const errorId = useId();

  const [userId, setUserId] = useState(readLastUserId);
  const [fieldError, setFieldError] = useState<string | null>(null);
  const [formError, setFormError] = useState<string | null>(null);
  const [pending, setPending] = useState(false);
  const [expired] = useState(() => sessionStore.getSignOutReason() === "expired");

  const from = (location.state as { from?: string } | null)?.from ?? "/";

  if (session) return <Navigate to={from} replace />;

  async function handleSubmit(event: SubmitEvent<HTMLFormElement>) {
    event.preventDefault();
    if (pending) return;

    if (!isGuid(userId)) {
      setFieldError("Enter a user ID in GUID format, like 00000000-0000-0000-0000-000000000001.");
      inputRef.current?.focus();
      return;
    }

    setFieldError(null);
    setFormError(null);
    setPending(true);
    try {
      const next = await requestSession(normalizeGuid(userId));
      rememberUserId(next.userId);
      sessionStore.setSession(next);
      void navigate(from, { replace: true });
    } catch (error) {
      setFormError(error instanceof Error ? error.message : "Sign-in failed.");
      setPending(false);
    }
  }

  return (
    <div className={styles.page}>
      <div className={`card ${styles.panel}`}>
        <h1 tabIndex={-1}>Sign in</h1>
        <p className="muted">
          Your user ID is who you are in JustAnotherList. Use the same ID to get back to your lists,
          or a new one to try sharing lists between people. Each browser tab keeps its own sign-in.
        </p>

        {expired && (
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

        <form
          className={styles.form}
          noValidate
          onSubmit={(event) => {
            void handleSubmit(event);
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
              setFieldError(null);
              announce("Generated a new user ID.");
            }}
          >
            Generate a new user ID
          </button>

          <button type="submit" className="button button-primary" aria-disabled={pending}>
            {pending ? "Signing in…" : "Sign in"}
          </button>
        </form>
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
