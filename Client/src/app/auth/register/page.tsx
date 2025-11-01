import {
  isAuthenticated,
  refreshToken,
} from '@/common/services/auth/TokenService.service';

import { RegistrationForm } from '@/features/registration';
import { redirect } from 'next/navigation';
import { use } from 'react';

export default function Register() {
  const hasAuthenticated = use(isAuthenticated());

  if (hasAuthenticated) {
    redirect('/dashboard');
  }

  return (
    <>
      <h1>Register</h1>
      <RegistrationForm />
    </>
  );
}
