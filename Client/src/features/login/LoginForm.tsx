'use client';

import { AuthResult, authenticateUser } from './actions';
import { startTransition, useActionState, useEffect } from 'react';

import { useRouter } from 'next/navigation';

export function LoginForm() {
  const { state, formAction, pending } = useViewController();

  return (
    <form action={formAction}>
      <label>
        Username
        <input
          type="email"
          name="email"
          defaultValue={state.values.email}
          autoComplete="email webauthn"
          aria-invalid={state.fieldErrors?.email != null}
          required
          autoFocus
        />
      </label>

      <label>
        Password
        <input
          type="password"
          name="password"
          defaultValue={state.values.password}
          autoComplete="current-password webauthn"
          aria-invalid={state.fieldErrors?.password != null}
          required
        />
      </label>

      {state.formError != null && (
        <p role="alert" aria-live="assertive" style={{ color: 'red' }}>
          {state.formError}
        </p>
      )}

      <button type="submit" disabled={pending} aria-busy={pending}>
        {pending ? 'Logging in…' : 'Login'}
      </button>
    </form>
  );
}

function useViewController() {
  const [state, formAction, pending] = useActionState(
    authenticateUser,
    initialState,
  );

  const router = useRouter();
  useEffect(() => {
    if (state.ok) {
      startTransition(() => {
        router.push('/dashboard');
      });
    }
  }, [state.ok, router]);

  return {
    state,
    formAction,
    pending,
  };
}

const initialState = {
  values: {
    email: '',
    password: '',
  },
  ok: false,
} satisfies AuthResult;
