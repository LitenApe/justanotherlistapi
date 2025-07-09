import { SignInPage } from '../../features/auth/pages/SignIn.page';
import { createFileRoute } from '@tanstack/react-router';

export const Route = createFileRoute('/auth/signin')({
  component: SignInPage,
});
