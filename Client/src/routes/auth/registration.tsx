import { RegistrationPage } from '../../features/auth/pages/Registration.page';
import { createFileRoute } from '@tanstack/react-router';

export const Route = createFileRoute('/auth/registration')({
  component: RegistrationPage,
});
