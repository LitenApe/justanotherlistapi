import { PropsWithChildren, use } from 'react';

import { isAuthenticated } from '@/common/services/auth/TokenService.service';
import { redirect } from 'next/navigation';

export default function AuthLayout(props: PropsWithChildren) {
  const { children } = props;

  const hasAuthenticated = use(isAuthenticated());

  if (hasAuthenticated) {
    redirect('/dashboard');
  }

  return children;
}
