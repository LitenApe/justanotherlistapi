import Cookies from 'js-cookie';

export function getChecklists() {
  const token = Cookies.get('accessToken');

  if (token == null) {
    throw new Error('Unable to retrieve token from cookie');
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
  const token = Cookies.get('accessToken');

  if (token == null) {
    throw new Error('Unable to retrieve token from cookie');
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
  const token = Cookies.get('accessToken');

  if (token == null) {
    throw new Error('Unable to retrieve token from cookie');
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
  const token = Cookies.get('accessToken');

  if (token == null) {
    throw new Error('Unable to retrieve token from cookie');
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
