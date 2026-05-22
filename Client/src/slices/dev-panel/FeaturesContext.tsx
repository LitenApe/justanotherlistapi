import {
  createContext,
  useContext,
  useState,
  useCallback,
  type ReactNode,
} from "react";

export interface FeatureFlags {
  suspense: boolean;
  useTransition: boolean;
  useDeferredValue: boolean;
  useOptimistic: boolean;
  showRenderCounts: boolean;
}

export const defaultFlags: FeatureFlags = {
  suspense: true,
  useTransition: true,
  useDeferredValue: true,
  useOptimistic: true,
  showRenderCounts: false,
};

interface FeaturesContextValue {
  flags: FeatureFlags;
  setFlag: <K extends keyof FeatureFlags>(
    key: K,
    value: FeatureFlags[K],
  ) => void;
}

const FeaturesContext = createContext<FeaturesContextValue>({
  flags: defaultFlags,
  setFlag: () => {},
});

export function FeaturesProvider({ children }: { children: ReactNode }) {
  const [flags, setFlags] = useState(defaultFlags);

  const setFlag = useCallback(
    <K extends keyof FeatureFlags>(key: K, value: FeatureFlags[K]) => {
      setFlags((prev) => ({ ...prev, [key]: value }));
    },
    [],
  );

  return (
    <FeaturesContext.Provider value={{ flags, setFlag }}>
      {children}
    </FeaturesContext.Provider>
  );
}

export function useFeatures(): FeaturesContextValue {
  return useContext(FeaturesContext);
}
