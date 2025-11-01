import { LoginForm } from '@/features/login';
import { isAuthenticated } from '@/common/services/auth/TokenService.service';
import { redirect } from 'next/navigation';
import { use } from 'react';

export default function Login() {
  const hasAuthenticated = use(isAuthenticated());

  if (hasAuthenticated) {
    redirect('/dashboard');
  }

  return (
    <>
      <h1>Login</h1>
      <LoginForm />
    </>
  );
}
