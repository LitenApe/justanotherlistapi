import { get } from '../../../services/localstorage.service';

export function getChecklists() {
  const token = get('token');

  if (token == null) {
    throw new Error('Unable to retrieve token from localstorage');
  }

  const options = {
    headers: new Headers({
      authorization: `Bearer ${token}`,
    }),
  };

  return fetch('https://localhost:55732/api/list', options)
    .then((response) => response.text())
    .then((response) => (response !== '' ? JSON.parse(response) : null));
}

export function getChecklist(id: string) {
  const token = get('token');

  if (token == null) {
    throw new Error('Unable to retrieve token from localstorage');
  }

  const options = {
    headers: new Headers({
      authorization: `Bearer ${token}`,
    }),
  };

  return fetch(`https://localhost:55732/api/list/${id}`, options)
    .then((response) => response.text())
    .then((response) => (response !== '' ? JSON.parse(response) : null));
}

export function createChecklist(name: string) {
  const token = get('token');

  if (token == null) {
    throw new Error('Unable to retrieve token from localstorage');
  }

  const options = {
    method: 'POST',
    headers: new Headers({
      authorization: `Bearer ${token}`,
      'content-type': 'application/json',
    }),
    body: JSON.stringify({ name }),
  };

  return fetch('https://localhost:55732/api/list', options);
}

export function createItem(
  id: string,
  name: string,
  description: string,
  completed: boolean,
) {
  const token = get('token');

  if (token == null) {
    throw new Error('Unable to retrieve token from localstorage');
  }

  const options = {
    method: 'POST',
    headers: new Headers({
      authorization: `Bearer ${token}`,
      'content-type': 'application/json',
    }),
    body: JSON.stringify({ name, description, completed }),
  };

  return fetch(`https://localhost:55732/api/list/${id}`, options)
    .then((response) => response.text())
    .then((response) => (response !== '' ? JSON.parse(response) : null));
}
