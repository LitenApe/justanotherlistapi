import { createFileRoute, useParams } from '@tanstack/react-router';
import { useEffect, useState } from 'react';

import { ChecklistPage } from '../../features/checklist/pages/Checklist.page';
import { getChecklist } from '../../features/checklist/services/checklists.service';

export const Route = createFileRoute('/checklist/$checklistId')({
  component: RouteComponent,
});

function RouteComponent() {
  const [data, setData] = useState(null);
  const { checklistId } = useParams({ from: '/checklist/$checklistId' });

  useEffect(() => {
    getChecklist(checklistId).then(setData).catch(console.error);
  }, [checklistId]);

  return <ChecklistPage list={data} />;
}
