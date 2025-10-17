'use server';

import { HttpError, http } from '@/common/services/http';

export type AuthResult = {
  ok: boolean;
  formError?: string;
  fieldErrors?: Record<string, string>;
  values: { email: string; password: string };
};

export async function authenticateUser(
  _: unknown,
  formData: FormData,
): Promise<AuthResult> {
  const email = String(formData.get('email') ?? '');
  const password = String(formData.get('password') ?? '');
  const values = { email, password: '' };

  try {
    const response = await http.post('http://localhost:55733/login', {
      email,
      password,
    });

    const body = response.data;
    console.log(body);

    return { ok: true, values };
  } catch (error: unknown) {
    if (error instanceof HttpError) {
      const response = error.response;

      if (response.status === 401) {
        return {
          ok: false,
          formError: 'Invalid credentials',
          values,
        };
      }

      const msg =
        typeof response.error === 'string'
          ? response.error
          : response.error?.message ??
            'Unable to authenticate. Try again later.';
      return { ok: false, formError: msg, values };
    }

    console.error('Failed to authenticate user', error);
    return { ok: false, formError: 'Network error. Try again.', values };
  }
}
