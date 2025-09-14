import { describe, expect, test } from 'vitest';

import { renderHook } from 'vitest-browser-react';
import { useState } from 'react';

type ToggleCallback = (value?: boolean) => boolean;

function useBoolean(initialValue = false): [boolean, ToggleCallback] {
  const [value, setValue] = useState(initialValue);

  const toggle = (newValue?: boolean) => {
    if (newValue != null) {
      setValue(newValue);
      return newValue;
    }

    setValue(!value);
    return !value;
  };

  return [value, toggle];
}

describe('UseBoolean', () => {
  test('returns "false" by default', () => {
    const { result } = renderHook(() => useBoolean());

    const [actual] = result.current;
    expect(actual).toBe(false);
  });

  test('returns initial value', () => {
    const { result } = renderHook(() => useBoolean(true));

    const [actual] = result.current;
    expect(actual).toBe(true);
  });

  test('callback toggles value', () => {
    const { result, act } = renderHook(() => useBoolean());

    const [, callback] = result.current;
    const actualBefore = result.current[0];
    expect(actualBefore).toBe(false);

    act(() => {
      callback();
    });

    const actualAfter = result.current[0];
    expect(actualAfter).toBe(true);
  });

  test('callback respect override value', () => {
    const { result, act } = renderHook(() => useBoolean());

    const [, callback] = result.current;
    const actualBefore = result.current[0];
    expect(actualBefore).toBe(false);

    act(() => {
      callback(false);
    });

    const actualAfter = result.current[0];
    expect(actualAfter).toBe(false);
  });
});
