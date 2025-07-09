import { useEffect, useState } from 'react';

import { ChecklistsPage } from '../../features/checklist/pages';
import { createFileRoute } from '@tanstack/react-router';
import { getChecklists } from '../../features/checklist/services/checklists.service';

function Page() {
  const data = useViewController();

  return <ChecklistsPage data={data} />;
}

function useViewController() {
  const [data, setData] = useState(null);

  useEffect(() => {
    getChecklists().then(setData).catch(console.error);
  }, []);

  return data;
}

export const Route = createFileRoute('/checklist/')({
  component: Page,
});
