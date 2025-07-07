import { Link, createFileRoute } from '@tanstack/react-router';

export const Route = createFileRoute('/404')({
  component: NotFound,
});

export function NotFound() {
  const { title, message } = getMessage();

  return (
    <div>
      <h1>{title}</h1>
      <p>{message}</p>
      <Link to="/">Home</Link>
      <Link to="/checklist">Dashboard</Link>
    </div>
  );
}

function getMessage() {
  return messages[Math.floor(Math.random() * messages.length)];
}

const messages = [
  {
    title: '404: Oops! Page on the run',
    message:
      "Looks like the page you're looking for went out for coffee... and never came back. Let's get you back home before you start missing it too.",
  },
  {
    title: '404: Page abducted by aliens',
    message:
      'We swear it was here a second ago... While we investigate the UFO activity, you might want to head back to safety.',
  },
  {
    title: '404: Not all who wander are lost... But this page is definitely is',
    message:
      "You've wandered into the unknown, brave traveler. Use this magic portal to return to the realm of sanity",
  },
  {
    title: '404: We sent the dog to fetch the page... Still waiting',
    message:
      'Our good boy os doing his best! In the meantime, you can go back to where the treats are.',
  },
];
