import { ChecklistsOverview } from '../components/ChecklistsOverview';
import { CreateNewChecklist } from '../components/CreateNewChecklist';

type Props = {
  data: Array<unknown> | null;
};

export function ChecklistsPage(props: Props) {
  const { data } = props;

  return (
    <>
      <h1>Lists</h1>
      {data == null && <p>Loading</p>}
      {data != null && <ChecklistsOverview lists={data} />}
      <CreateNewChecklist />
    </>
  );
}
