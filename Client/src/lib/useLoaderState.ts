import { useState, type Dispatch, type SetStateAction } from "react";

/**
 * A local copy of loader data that the page may change (for example with
 * realtime events). It resets whenever the loader returns new data, after a
 * navigation or a revalidation.
 */
export function useLoaderState<T>(loaded: T): [T, Dispatch<SetStateAction<T>>] {
  const [state, setState] = useState(loaded);
  const [source, setSource] = useState(loaded);
  // Adjusting state while rendering avoids a render with stale data
  // (https://react.dev/learn/you-might-not-need-an-effect).
  if (loaded !== source) {
    setSource(loaded);
    setState(loaded);
  }
  return [state, setState];
}
