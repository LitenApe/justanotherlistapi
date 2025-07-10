import type { FormEvent } from 'react';
import { createItem } from '../services/checklists.service';

type Props = {
  list: unknown;
};

export function CreateNewItem(props: Props) {
  const { onSubmitHandler } = useViewController(props);

  return (
    <form onSubmit={onSubmitHandler}>
      <p>Create new item</p>
      <label>
        Name
        <input type="text" name="name" required />
      </label>
      <label>
        Description
        <textarea name="description" />
      </label>
      <label>
        Completed
        <input type="checkbox" name="completed" />
      </label>
      <button type="submit">Create</button>
    </form>
  );
}

function useViewController(props: Props) {
  const { list } = props;

  function onSubmitHandler(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();

    const data = new FormData(event.target as HTMLFormElement);
    const name = data.get('name')?.toString();
    const description = data.get('description')?.toString();
    const completed = data.get('completed')?.toString() === 'on';

    console.log(list, name, description, completed);

    if (name == null || description == null) {
      return;
    }

    createItem(list.id, name, description, completed).then(console.log);
  }

  return {
    onSubmitHandler,
  };
}
