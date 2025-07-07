import { createFileRoute } from '@tanstack/react-router';

function Dashboard() {
  return <div>Checklist</div>;
}

export const Route = createFileRoute('/checklist/')({
  component: Dashboard,
});
