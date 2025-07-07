import { createFileRoute } from '@tanstack/react-router';

function SignIn() {
  return (
    <>
      <h1>Sign In</h1>
      <form>
        <label>
          E-mail
          <input type="email" autoComplete="username" inputMode="email" />
        </label>
        <label>
          Password
          <input type="password" autoComplete="current-password" />
        </label>
        <button type="submit">Sign in</button>
      </form>
    </>
  );
}

export const Route = createFileRoute('/auth/signin')({
  component: SignIn,
});
