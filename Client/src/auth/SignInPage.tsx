import { useEffect, useId, useRef } from "react";
import { Form, Link, redirect, useNavigation } from "react-router";
import { authApi } from "../api/auth";
import { errorMessage } from "../api/http";
import { DocumentTitle } from "../components/DocumentTitle";
import { Icon } from "../components/Icon";
import { PasswordField } from "../components/PasswordField";
import { formText } from "../lib/formText";
import type { Route } from "./+types/SignInPage";
import { emailProblem } from "./accountRules";
import styles from "./AuthPage.module.css";
import { returnPathOf, withReturnTo } from "./redirects";
import { sessionStore } from "./session";

export function clientLoader({ request }: Route.ClientLoaderArgs) {
  const returnTo = returnPathOf(request);
  if (sessionStore.getSession()) throw redirect(returnTo);
  return {
    expired: sessionStore.getSignOutReason() === "expired",
    passwordChanged: new URL(request.url).searchParams.has("passwordChanged"),
    returnTo,
  };
}

export async function clientAction({ request }: Route.ClientActionArgs) {
  const form = await request.formData();
  const email = formText(form, "email");
  const password = formText(form, "password");
  const fieldErrors = {
    email: emailProblem(email),
    password: password ? null : "Enter your password.",
  };
  if (fieldErrors.email || fieldErrors.password) return { fieldErrors };

  try {
    sessionStore.setSession(await authApi.signIn({ email: email.trim(), password }));
  } catch (error) {
    return { formError: errorMessage(error) };
  }
  return redirect(returnPathOf(request));
}

export default function SignInPage({ loaderData, actionData }: Route.ComponentProps) {
  const navigation = useNavigation();
  const pending = navigation.formData !== undefined;
  const emailRef = useRef<HTMLInputElement>(null);
  const passwordRef = useRef<HTMLInputElement>(null);
  const emailErrorId = useId();

  const fieldErrors = actionData && "fieldErrors" in actionData ? actionData.fieldErrors : null;
  const formError = actionData && "formError" in actionData ? actionData.formError : null;

  useEffect(() => {
    if (!fieldErrors) return;
    (fieldErrors.email ? emailRef : passwordRef).current?.focus();
  }, [fieldErrors]);

  return (
    <div className={styles.page}>
      <DocumentTitle title="Sign in" />
      <div className={`card ${styles.panel}`}>
        <h1 tabIndex={-1}>Sign in</h1>

        {loaderData.expired && (
          <p className="notice" role="status">
            Your session expired. Sign in again to continue.
          </p>
        )}
        {loaderData.passwordChanged && (
          <p className="notice" role="status">
            Your password is changed. Sign in with the new one.
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
            <label className="label" htmlFor="sign-in-email">
              Email
            </label>
            <input
              ref={emailRef}
              id="sign-in-email"
              name="email"
              type="email"
              className="input"
              autoComplete="username"
              autoCapitalize="none"
              spellCheck={false}
              required
              aria-invalid={fieldErrors?.email ? true : undefined}
              aria-describedby={fieldErrors?.email ? emailErrorId : undefined}
            />
            {fieldErrors?.email && (
              <p className="field-error" id={emailErrorId}>
                {fieldErrors.email}
              </p>
            )}
          </div>

          <PasswordField
            label="Password"
            name="password"
            autoComplete="current-password"
            error={fieldErrors?.password}
            inputRef={passwordRef}
          />

          <button
            type="submit"
            className={`button button-primary ${styles.submit}`}
            aria-disabled={pending}
          >
            {pending ? "Signing in…" : "Sign in"}
          </button>
        </Form>

        <ul className={styles.links}>
          <li>
            <Link to="/forgot-password">Forgot your password?</Link>
          </li>
          <li>
            New here? <Link to={withReturnTo("/sign-up", loaderData.returnTo)}>Sign up</Link>
          </li>
        </ul>
      </div>
    </div>
  );
}
