import Cookies from 'js-cookie';
import { store } from '../../../services/localstorage.service';

export function signIn(email: string, password: string) {
  const payload = {
    email,
    password,
  };

  const options = {
    method: 'POST',
    body: JSON.stringify(payload),
    headers: new Headers({ 'content-type': 'application/json' }),
  };

  return fetch('https://localhost:55732/login', options)
    .then((response) => response.text())
    .then((response) => (response !== '' ? JSON.parse(response) : null))
    .then((response) => {
      store('token', response);
      Cookies.set('accessToken', response.accessToken);
      return response;
    });
}
