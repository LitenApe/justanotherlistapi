import styles from "./Login.module.css";

export interface LoginViewProps {
  error: string | null;
  isPending: boolean;
  formAction: (payload: FormData) => void;
}

export function LoginView({ error, isPending, formAction }: LoginViewProps) {
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
