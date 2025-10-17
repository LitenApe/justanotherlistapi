'use server';

import { HttpError, http } from '@/common/services/http';

export enum RegistrationError {
  MISMATCH,
  INVALID,
  DUPLICATE,
}

export type RegistrationResult = {
  ok: boolean;
  formError?: string;
  fieldErrors?: {
    username?: string;
    password?: string;
  };
  values: {
    email: string;
    password: string;
    'repeat-password': string;
  };
};

export async function registerNewUser(
  _: unknown,
  formData: FormData,
): Promise<RegistrationResult> {
  const email = String(formData.get('email') ?? '');
  const password = String(formData.get('password') ?? '');
  const repeatedPassword = String(formData.get('repeat-password') ?? '');

  const values = {
    email,
    password,
    'repeat-password': repeatedPassword,
  };

  if (password !== repeatedPassword) {
    return {
      ok: false,
      fieldErrors: {
        password: 'Passwords do not match',
      },
      values,
    };
  }

  try {
    await http.post('http://localhost:55733/register', {
      email,
      password,
    });

    return {
      ok: true,
      values,
    };
  } catch (error: unknown) {
    if (error instanceof HttpError) {
      const response = error.response;
      const body = response.error;

      if (
        response.status === 400 &&
        body &&
        typeof body === 'object' &&
        'errors' in body
      ) {
        const errs = (body as any).errors as Record<string, any>;
        const invalidPassword =
          'PasswordTooShort' in errs ||
          'PasswordRequiresNonAlphanumeric' in errs ||
          'PasswordRequiresDigit' in errs ||
          'PasswordRequiresUpper' in errs ||
          'PasswordRequiresLower' in errs;
        const invalidUsername = 'DuplicateUserName' in errs;

        return {
          ok: false,
          fieldErrors: {
            username: invalidUsername
              ? 'A user with that email already exists'
              : undefined,
            password: invalidPassword
              ? 'Password does not meet complexity requirements'
              : undefined,
          },
          values,
        };
      }

      const msg =
        typeof body === 'string'
          ? body
          : body?.message ?? 'Unable to register. Try again later.';

      return {
        ok: false,
        formError: msg,
        values,
      };
    }

    console.error('Failed to register user', error);
    return {
      ok: false,
      formError: 'Network error. Please try again.',
      values,
    };
  }
}
