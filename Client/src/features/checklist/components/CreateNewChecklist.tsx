import type { FormEvent } from 'react';
import { createChecklist } from '../../checklist/services/checklists.service';

export function CreateNewChecklist() {
  const { onSubmitHandler } = useViewController();

  return (
    <form onSubmit={onSubmitHandler}>
      <label>
        Name
        <input type="text" name="name" required />
      </label>
      <button type="submit">Create</button>
    </form>
  );
}

function useViewController() {
  function onSubmitHandler(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();

    const data = new FormData(event.target as HTMLFormElement);
    const name = data.get('name')?.toString();

    if (name == null) {
      return;
    }

    createChecklist(name).then(console.log);
  }

  return {
    onSubmitHandler,
  };
}
