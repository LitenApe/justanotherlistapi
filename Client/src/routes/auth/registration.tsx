import { createFileRoute } from '@tanstack/react-router';

function Registration() {
  return (
    <>
      <h1>Registration</h1>
      <form>
        <label>
          E-mail
          <input type="email" autoComplete="username" inputMode="email" />
        </label>
        <label>
          Password
          <input type="password" autoComplete="new-password" />
        </label>
        <label>
          Repeat password
          <input type="password" autoComplete="new-password" />
        </label>
        <button type="submit">Register</button>
      </form>
    </>
  );
}

export const Route = createFileRoute('/auth/registration')({
  component: Registration,
});
