import { CreateNewItem } from '../components/CreateNewItem';

type Props = {
  list: unknown;
};

export function ChecklistPage(props: Props) {
  const { list } = props;

  if (list == null) {
    return <p>loading</p>;
  }

  const { name, items } = list;

  if (items.length === 0) {
    return (
      <>
        <h1>{name}</h1>
        <p>Empty</p>
        <CreateNewItem list={list} />
      </>
    );
  }

  return (
    <>
      <h1>{name}</h1>
      <ul>
        {items.map((item) => (
          <li key={`${item.id}`}>{item.name}</li>
        ))}
      </ul>
      <CreateNewItem list={list} />
    </>
  );
}
