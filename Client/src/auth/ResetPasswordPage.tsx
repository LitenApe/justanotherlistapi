import { useEffect, useRef } from "react";
import { Form, Link, redirect, useNavigation } from "react-router";
import { authApi } from "../api/auth";
import { ApiError, errorMessage } from "../api/http";
import { DocumentTitle } from "../components/DocumentTitle";
import { Icon } from "../components/Icon";
import { PasswordField } from "../components/PasswordField";
import { formText } from "../lib/formText";
import type { Route } from "./+types/ResetPasswordPage";
import { newPasswordProblem, PASSWORD_MIN_LENGTH } from "./accountRules";
import styles from "./AuthPage.module.css";
import { sessionStore } from "./session";

/** The emailed link: /reset-password?user=<user ID>&code=<code>. */
function linkOf(request: Request): { userId: string; code: string } | null {
  const params = new URL(request.url).searchParams;
  const userId = params.get("user");
  const code = params.get("code");
  return userId && code ? { userId, code } : null;
}

export function clientLoader({ request }: Route.ClientLoaderArgs) {
  return { complete: linkOf(request) !== null };
}

export async function clientAction({ request }: Route.ClientActionArgs) {
  const link = linkOf(request);
  if (!link) return { linkProblem: "This link is incomplete." };
  const password = formText(await request.formData(), "password");
  const fieldError = newPasswordProblem(password);
  if (fieldError) return { fieldError };

  try {
    await authApi.resetPassword({ ...link, password });
  } catch (error) {
    if (error instanceof ApiError && error.status === 400) {
      return { linkProblem: error.message };
    }
    return { formError: errorMessage(error) };
  }
  // Sign this tab out, so the next sign-in is with the new password.
  sessionStore.signOut("user");
  return redirect("/sign-in?passwordChanged=1");
}

export default function ResetPasswordPage({ loaderData, actionData }: Route.ComponentProps) {
  const navigation = useNavigation();
  const pending = navigation.formData !== undefined;
  const passwordRef = useRef<HTMLInputElement>(null);

  const fieldError = actionData && "fieldError" in actionData ? actionData.fieldError : null;
  const formError = actionData && "formError" in actionData ? actionData.formError : null;
  const linkProblem = loaderData.complete
    ? actionData && "linkProblem" in actionData
      ? actionData.linkProblem
      : null
    : "This link is incomplete.";

  useEffect(() => {
    if (actionData && "fieldError" in actionData) passwordRef.current?.focus();
  }, [actionData]);

  return (
    <div className={styles.page}>
      <DocumentTitle title="Choose a new password" />
      <div className={`card ${styles.panel}`}>
        <h1 tabIndex={-1}>Choose a new password</h1>

        {linkProblem && (
          <div className="alert" role="alert">
            <Icon name="alert" />
            <p>
              {linkProblem} <Link to="/forgot-password">Ask for a new link</Link>.
            </p>
          </div>
        )}
        {formError && (
          <div className="alert" role="alert">
            <Icon name="alert" />
            <p>{formError}</p>
          </div>
        )}

        {loaderData.complete && (
          <Form
            method="post"
            className={styles.form}
            noValidate
            onSubmit={(event) => {
              if (pending) event.preventDefault();
            }}
          >
            <PasswordField
              label="New password"
              name="password"
              autoComplete="new-password"
              hint={`At least ${PASSWORD_MIN_LENGTH.toString()} characters. A few words in a row make a strong one.`}
              error={fieldError}
              inputRef={passwordRef}
            />
            <button
              type="submit"
              className={`button button-primary ${styles.submit}`}
              aria-disabled={pending}
            >
              {pending ? "Saving…" : "Save the new password"}
            </button>
          </Form>
        )}
      </div>
    </div>
  );
}
