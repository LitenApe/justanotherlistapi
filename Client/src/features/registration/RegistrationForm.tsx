'use client';

import { startTransition, useActionState, useEffect } from 'react';

import { registerNewUser, RegistrationResult } from './actions';
import { useRouter } from 'next/navigation';

export function RegistrationForm() {
  const { state, formAction, pending } = useViewController();

  return (
    <form action={formAction}>
      <label>
        Email
        <input
          type="email"
          name="email"
          defaultValue={state.values.email}
          autoComplete="email webauthn"
          required
        />
      </label>

      <label>
        Password
        <input
          type="password"
          name="password"
          defaultValue={state.values.password}
          autoComplete="new-password webauthn"
          required
        />
      </label>

      <label>
        Repeat password
        <input
          type="password"
          name="repeat-password"
          defaultValue={state.values['repeat-password']}
          autoComplete="new-password webauthn"
          required
        />
      </label>

      {state.formError != null && (
        <p style={{ color: 'red' }}>{state.formError}</p>
      )}

      {state.fieldErrors?.username != null && (
        <p style={{ color: 'red' }}>{state.fieldErrors.username}</p>
      )}

      {state.fieldErrors?.password != null && (
        <p style={{ color: 'red' }}>{state.fieldErrors.password}</p>
      )}

      <button type="submit" disabled={pending}>
        {pending ? 'Registering…' : 'Register'}
      </button>
    </form>
  );
}

function useViewController() {
  const [state, formAction, pending] = useActionState(
    registerNewUser,
    initialState,
  );

  const router = useRouter();
  useEffect(() => {
    if (state.ok) {
      startTransition(() => {
        router.push('/auth/login');
      });
    }
  }, [state, router]);

  return {
    state,
    formAction,
    pending,
  };
}

export const initialState = {
  values: {
    email: '',
    password: '',
    'repeat-password': '',
  },
  ok: false,
} satisfies RegistrationResult;
