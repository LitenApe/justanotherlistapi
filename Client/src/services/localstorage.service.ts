export function store(key: string, value: unknown) {
  if (value == null || value === '') {
    return;
  }

  localStorage.setItem(key, JSON.stringify(value));
}

export function get(key: string) {
  const value = localStorage.getItem(key);

  if (value == null) {
    return null;
  }

  try {
    return JSON.parse(value);
  } catch {
    return value;
  }
}
