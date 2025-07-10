import { Link } from '@tanstack/react-router';

type Props = {
  lists: Array<unknown>;
};

export function ChecklistsOverview(props: Props) {
  const { lists } = props;

  if (lists.length === 0) {
    return <p>Empty</p>;
  }

  return (
    <ul>
      {lists.map((item) => (
        <li key={item.id}>
          <Link to={`/checklist/${item.id}`}>{item.name}</Link>
        </li>
      ))}
    </ul>
  );
}
