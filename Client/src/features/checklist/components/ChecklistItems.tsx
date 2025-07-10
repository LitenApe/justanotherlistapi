type Props = {
  items: Array<unknown>;
};

export function ChecklistItems(props: Props) {
  const { items } = props;

  <ul>
    {items.map((item) => (
      <li key={item.id}>
        <p>{item.name}</p>
      </li>
    ))}
  </ul>;
}
