import { createFileRoute } from '@tanstack/react-router';

function Home() {
  return <h1>Hello</h1>;
}

export const Route = createFileRoute('/')({
  component: Home,
});
