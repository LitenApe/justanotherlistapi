import { login } from "./api";
import styles from "./Login.module.css";
import { useActionState } from "react";
import { useNavigate } from "react-router";

interface LoginState {
  error: string | null;
}

export function Login() {
  const navigate = useNavigate();

  const [state, formAction, isPending] = useActionState<LoginState, FormData>(
    async (_prev, formData) => {
      const clientId = formData.get("clientId") as string;
      const clientSecret = formData.get("clientSecret") as string;

      try {
        await login(clientId, clientSecret);
        navigate("/", { replace: true });
        return { error: null };
      } catch (e) {
        return {
          error: e instanceof Error ? e.message : "Login failed",
        };
      }
    },
    { error: null },
  );

  return (
    <div className={styles.container}>
      <form action={formAction} className={styles.card}>
        <h1 className={styles.title}>Sign In</h1>

        {state.error && <p className={styles.error}>{state.error}</p>}

        <div className={styles.field}>
          <label className={styles.label} htmlFor="clientId">
            Client ID
          </label>
          <input
            id="clientId"
            name="clientId"
            type="text"
            className={styles.input}
            defaultValue="default-client"
            required
          />
        </div>

        <div className={styles.field}>
          <label className={styles.label} htmlFor="clientSecret">
            Client Secret
          </label>
          <input
            id="clientSecret"
            name="clientSecret"
            type="password"
            className={styles.input}
            defaultValue="default-secret"
            required
          />
        </div>

        <button type="submit" className={styles.button} disabled={isPending}>
          {isPending ? "Signing in…" : "Sign In"}
        </button>
      </form>
    </div>
  );
}
