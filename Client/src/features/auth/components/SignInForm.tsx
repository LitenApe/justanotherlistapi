import { useState, type FormEvent } from 'react';
import { signIn } from '../services/signin.services';
import { useNavigate } from '@tanstack/react-router';

export function SignInForm() {
  const { errors, onSubmitHandler } = useViewController();

  return (
    <form onSubmit={onSubmitHandler}>
      <label>
        E-mail
        <input
          type="email"
          name="email"
          autoComplete="email"
          inputMode="email"
          required
        />
      </label>
      <label>
        Password
        <input
          type="password"
          name="password"
          autoComplete="current-password"
          required
        />
      </label>
      <button type="submit">Sign in</button>
      {errors != null && typeof errors === 'string' && <p>{errors}</p>}
    </form>
  );
}

function useViewController() {
  const [errors, setErrors] = useState<unknown>();
  const go = useNavigate();
  function onSubmitHandler(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();

    const data = new FormData(event.target as HTMLFormElement);
    const email = data.get('email')?.toString();
    const password = data.get('password')?.toString();

    if (email == null || password == null) {
      return;
    }

    signIn(email, password).then((response) => {
      if (response?.detail === 'Failed') {
        setErrors(response.detail);
        return;
      }

      go({ to: '/checklist' });
    });
  }

  return {
    errors,
    onSubmitHandler,
  };
}
