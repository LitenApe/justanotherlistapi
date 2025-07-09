import { get } from '../../../services/localstorage.service';

export function getChecklists() {
  const token = get('token');

  if (token == null) {
    throw new Error('Unable to retrieve tokens from localstorage');
  }

  const options = {
    headers: new Headers({
      authorization: `Bearer ${token.accessToken}`,
    }),
  };

  return fetch('https://localhost:55732/api/list', options)
    .then((response) => response.text())
    .then((response) => (response !== '' ? JSON.parse(response) : null));
}

export function createChecklist(name: string) {
  const token = get('token');

  if (token == null) {
    throw new Error('Unable to retrieve tokens from localstorage');
  }

  const options = {
    method: 'POST',
    headers: new Headers({
      authorization: `Bearer ${token.accessToken}`,
      'content-type': 'application/json',
    }),
    body: JSON.stringify({ name }),
  };

  return fetch('https://localhost:55732/api/list', options);
}
