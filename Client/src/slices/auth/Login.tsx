import { login } from "./api";
import { routes } from "@shared/routes";
import styles from "./Login.module.css";
import { useActionState } from "react";
import { useNavigate } from "react-router";

// ─── Model ────────────────────────────────────────────────────────────────────

interface LoginModel {
  error: string | null;
  isPending: boolean;
  formAction: (payload: FormData) => void;
}

function useLoginModel(): LoginModel {
  const navigate = useNavigate();

  const [state, formAction, isPending] = useActionState<
    { error: string | null },
    FormData
  >(
    async (_prev, formData) => {
      const clientId = formData.get("clientId") as string;
      const clientSecret = formData.get("clientSecret") as string;

      try {
        await login(clientId, clientSecret);
        navigate(routes.home(), { replace: true });
        return { error: null };
      } catch (e) {
        return {
          error: e instanceof Error ? e.message : "Login failed",
        };
      }
    },
    { error: null },
  );

  return { error: state.error, isPending, formAction };
}

// ─── View ─────────────────────────────────────────────────────────────────────

interface LoginViewProps {
  error: string | null;
  isPending: boolean;
  formAction: (payload: FormData) => void;
}

function LoginView({ error, isPending, formAction }: LoginViewProps) {
  return (
    <div className={styles.container}>
      <form action={formAction} className={styles.card}>
        <h1 className={styles.title}>Sign In</h1>

        {error && <p className={styles.error}>{error}</p>}

        <div className={styles.field}>
          <label className={styles.label} htmlFor="clientId">
            Client ID
          </label>
          <input
            id="clientId"
            name="clientId"
            type="text"
            className={styles.input}
            defaultValue="00000000-0000-0000-0000-000000000001"
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
            defaultValue="dev"
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

// ─── Controller ───────────────────────────────────────────────────────────────

export function Login() {
  const model = useLoginModel();
  return <LoginView {...model} />;
}
