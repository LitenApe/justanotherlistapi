import { useEffect, useId, useRef } from "react";
import { Form, Link, redirect, useNavigation } from "react-router";
import { authApi } from "../api/auth";
import { ApiError, errorMessage } from "../api/http";
import { DocumentTitle } from "../components/DocumentTitle";
import { Icon } from "../components/Icon";
import { PasswordField } from "../components/PasswordField";
import { formText } from "../lib/formText";
import type { Route } from "./+types/SignUpPage";
import { emailProblem, nameProblem, newPasswordProblem, PASSWORD_MIN_LENGTH } from "./accountRules";
import styles from "./AuthPage.module.css";
import { returnPathOf, withReturnTo } from "./redirects";
import { sessionStore } from "./session";

export function clientLoader({ request }: Route.ClientLoaderArgs) {
  const returnTo = returnPathOf(request);
  if (sessionStore.getSession()) throw redirect(returnTo);
  return { returnTo };
}

export async function clientAction({ request }: Route.ClientActionArgs) {
  const form = await request.formData();
  const name = formText(form, "name");
  const email = formText(form, "email");
  const password = formText(form, "password");
  const fieldErrors = {
    name: nameProblem(name),
    email: emailProblem(email),
    password: newPasswordProblem(password),
  };
  if (fieldErrors.name || fieldErrors.email || fieldErrors.password) return { fieldErrors };

  try {
    sessionStore.setSession(
      await authApi.signUp({ name: name.trim(), email: email.trim(), password }),
    );
  } catch (error) {
    if (error instanceof ApiError && error.status === 409) return { emailTaken: true };
    return { formError: errorMessage(error) };
  }
  return redirect(returnPathOf(request));
}

export default function SignUpPage({ loaderData, actionData }: Route.ComponentProps) {
  const navigation = useNavigation();
  const pending = navigation.formData !== undefined;
  const nameRef = useRef<HTMLInputElement>(null);
  const emailRef = useRef<HTMLInputElement>(null);
  const passwordRef = useRef<HTMLInputElement>(null);
  const nameHintId = useId();
  const nameErrorId = useId();
  const emailHintId = useId();
  const emailErrorId = useId();

  const fieldErrors = actionData && "fieldErrors" in actionData ? actionData.fieldErrors : null;
  const formError = actionData && "formError" in actionData ? actionData.formError : null;
  const emailTaken = actionData && "emailTaken" in actionData;
  const signIn = withReturnTo("/sign-in", loaderData.returnTo);

  useEffect(() => {
    if (!fieldErrors) return;
    const firstInvalid = fieldErrors.name ? nameRef : fieldErrors.email ? emailRef : passwordRef;
    firstInvalid.current?.focus();
  }, [fieldErrors]);

  return (
    <div className={styles.page}>
      <DocumentTitle title="Sign up" />
      <div className={`card ${styles.panel}`}>
        <h1 tabIndex={-1}>Sign up</h1>
        <p className="muted">
          Make an account to keep lists and share them. Each browser tab keeps its own sign-in, so
          you can try sharing with a second account in another tab.
        </p>

        {emailTaken && (
          <div className="alert" role="alert">
            <Icon name="alert" />
            <p>
              An account already uses that email. <Link to={signIn}>Sign in instead</Link>, or{" "}
              <Link to="/forgot-password">choose a new password</Link> if you forgot it.
            </p>
          </div>
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
            <label className="label" htmlFor="sign-up-name">
              Name
            </label>
            <p className="hint" id={nameHintId}>
              Other members of your lists see it.
            </p>
            <input
              ref={nameRef}
              id="sign-up-name"
              name="name"
              className="input"
              autoComplete="name"
              required
              aria-invalid={fieldErrors?.name ? true : undefined}
              aria-describedby={fieldErrors?.name ? `${nameHintId} ${nameErrorId}` : nameHintId}
            />
            {fieldErrors?.name && (
              <p className="field-error" id={nameErrorId}>
                {fieldErrors.name}
              </p>
            )}
          </div>

          <div className="field">
            <label className="label" htmlFor="sign-up-email">
              Email
            </label>
            <p className="hint" id={emailHintId}>
              You sign in with it. Nobody else sees it.
            </p>
            <input
              ref={emailRef}
              id="sign-up-email"
              name="email"
              type="email"
              className="input"
              autoComplete="email"
              autoCapitalize="none"
              spellCheck={false}
              required
              aria-invalid={fieldErrors?.email ? true : undefined}
              aria-describedby={fieldErrors?.email ? `${emailHintId} ${emailErrorId}` : emailHintId}
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
            autoComplete="new-password"
            hint={`At least ${PASSWORD_MIN_LENGTH.toString()} characters. A few words in a row make a strong one.`}
            error={fieldErrors?.password}
            inputRef={passwordRef}
          />

          <button
            type="submit"
            className={`button button-primary ${styles.submit}`}
            aria-disabled={pending}
          >
            {pending ? "Signing up…" : "Sign up"}
          </button>
        </Form>

        <ul className={styles.links}>
          <li>
            Already have an account? <Link to={signIn}>Sign in</Link>
          </li>
        </ul>
      </div>
    </div>
  );
}
