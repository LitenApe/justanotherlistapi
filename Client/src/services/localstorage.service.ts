export function store(key: string, value: unknown) {
  if (value == null || value === '') {
    return;
  }

  localStorage.setItem(key, JSON.stringify(value));
}
