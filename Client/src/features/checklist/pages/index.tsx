import { CreateNewChecklist } from '../../auth/components/CreateNewChecklist';

type Props = {
  data: Array<unknown> | null;
};

export function ChecklistsPage(props: Props) {
  const { data } = props;

  return (
    <>
      <h1>Lists</h1>
      {data == null && <p>Loading</p>}
      {data != null && data.length === 0 && <p>Empty</p>}
      {data != null && data.length > 0 && <p>{JSON.stringify(data)}</p>}
      <CreateNewChecklist />
    </>
  );
}
