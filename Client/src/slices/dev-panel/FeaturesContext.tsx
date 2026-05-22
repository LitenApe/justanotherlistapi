import {
  createContext,
  useContext,
  useState,
  useCallback,
  type ReactNode,
} from "react";

export interface FeatureFlags {
  useConcurrent: boolean;
  showRenderCounts: boolean;
}

const defaultFlags: FeatureFlags = {
  useConcurrent: true,
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

const STORAGE_KEY = "dev-panel-features";

function loadFlags(): FeatureFlags {
  try {
    const stored = sessionStorage.getItem(STORAGE_KEY);
    if (stored) return { ...defaultFlags, ...JSON.parse(stored) };
  } catch {
    /* ignore */
  }
  return defaultFlags;
}

function saveFlags(flags: FeatureFlags): void {
  sessionStorage.setItem(STORAGE_KEY, JSON.stringify(flags));
}

export function FeaturesProvider({ children }: { children: ReactNode }) {
  const [flags, setFlags] = useState(loadFlags);

  const setFlag = useCallback(
    <K extends keyof FeatureFlags>(key: K, value: FeatureFlags[K]) => {
      setFlags((prev) => {
        const next = { ...prev, [key]: value };
        saveFlags(next);
        return next;
      });
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
