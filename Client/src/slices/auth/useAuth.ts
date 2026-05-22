import { useAuthToken } from "@shared/hooks";

export function useAuth() {
  const token = useAuthToken();

  return {
    isAuthenticated: token !== null,
    token,
  };
}
