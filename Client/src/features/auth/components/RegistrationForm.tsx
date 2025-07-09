import { useState, type FormEvent } from 'react';
import { register } from '../services/registration.service';
import { useNavigate } from '@tanstack/react-router';

export function RegistrationForm() {
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
          autoComplete="new-password"
          required
        />
      </label>
      <label>
        Repeat password
        <input
          type="password"
          name="repeat-password"
          autoComplete="new-password"
          required
        />
      </label>
      <button type="submit">Register</button>
      {errors != null && typeof errors === 'object' && (
        <p>{JSON.stringify(errors)}</p>
      )}
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

    register(email, password)
      .then((response) => {
        if (response != null && 'errors' in response) {
          setErrors(response.errors);
          return;
        }

        go({ to: '/auth/signin', search: { type: 'successful-registration' } });
      })
      .catch(console.error);
  }

  return {
    errors,
    onSubmitHandler,
  };
}
