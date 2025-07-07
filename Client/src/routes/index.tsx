import { Link, createFileRoute } from '@tanstack/react-router';

function Home() {
  return (
    <>
      <h1>Hello</h1>
      <ul>
        <li>
          <Link to="/auth/registration">Registration</Link>
        </li>
        <li>
          <Link to="/auth/signin">Sign in</Link>
        </li>
      </ul>
    </>
  );
}

export const Route = createFileRoute('/')({
  component: Home,
});
