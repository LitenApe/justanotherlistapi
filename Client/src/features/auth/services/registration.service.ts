export function register(email: string, password: string) {
  const payload = {
    email,
    password,
  };

  const options = {
    method: 'POST',
    body: JSON.stringify(payload),
    headers: new Headers({ 'content-type': 'application/json' }),
  };

  return fetch('https://localhost:55732/register', options)
    .then((response) => response.text())
    .then((response) => (response !== '' ? JSON.parse(response) : null));
}
