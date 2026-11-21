import { useEffect, useId, useRef } from "react";
import { Form, Link, useNavigation } from "react-router";
import { authApi } from "../api/auth";
import { errorMessage } from "../api/http";
import { DocumentTitle } from "../components/DocumentTitle";
import { Icon } from "../components/Icon";
import { formText } from "../lib/formText";
import type { Route } from "./+types/ForgotPasswordPage";
import { emailProblem } from "./accountRules";
import styles from "./AuthPage.module.css";

export async function clientAction({ request }: Route.ClientActionArgs) {
  const email = formText(await request.formData(), "email").trim();
  const fieldError = emailProblem(email);
  if (fieldError) return { fieldError };

  try {
    await authApi.requestPasswordReset(email);
  } catch (error) {
    return { formError: errorMessage(error) };
  }
  return { sentTo: email };
}

export default function ForgotPasswordPage({ actionData }: Route.ComponentProps) {
  const navigation = useNavigation();
  const pending = navigation.formData !== undefined;
  const emailRef = useRef<HTMLInputElement>(null);
  const hintId = useId();
  const errorId = useId();

  const fieldError = actionData && "fieldError" in actionData ? actionData.fieldError : null;
  const formError = actionData && "formError" in actionData ? actionData.formError : null;
  const sentTo = actionData && "sentTo" in actionData ? actionData.sentTo : null;

  useEffect(() => {
    if (actionData && "fieldError" in actionData) emailRef.current?.focus();
  }, [actionData]);

  return (
    <div className={styles.page}>
      <DocumentTitle title="Forgot your password?" />
      <div className={`card ${styles.panel}`}>
        <h1 tabIndex={-1}>Forgot your password?</h1>
        <p className="muted">
          Enter the email you signed up with, and we'll send it a link to choose a new password.
        </p>

        {/* Says the same whether or not an account uses the email, like the server. */}
        <div role="status">
          {sentTo && (
            <p className="notice">
              If an account uses {sentTo}, we've sent it a link to choose a new password. The link
              works for an hour.
            </p>
          )}
        </div>
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
            <label className="label" htmlFor="forgot-email">
              Email
            </label>
            <p className="hint" id={hintId}>
              The link goes to this address.
            </p>
            <input
              ref={emailRef}
              id="forgot-email"
              name="email"
              type="email"
              className="input"
              autoComplete="email"
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
            type="submit"
            className={`button button-primary ${styles.submit}`}
            aria-disabled={pending}
          >
            {pending ? "Sending…" : "Send the link"}
          </button>
        </Form>

        <ul className={styles.links}>
          <li>
            Remembered it? <Link to="/sign-in">Sign in</Link>
          </li>
        </ul>
      </div>
    </div>
  );
}
