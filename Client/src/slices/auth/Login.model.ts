import { login } from "./api";
import { useActionState } from "react";
import { useNavigate } from "react-router";

export interface LoginModel {
  error: string | null;
  isPending: boolean;
  formAction: (payload: FormData) => void;
}

export function useLoginModel(): LoginModel {
  const navigate = useNavigate();

  const [state, formAction, isPending] = useActionState<
    { error: string | null },
    FormData
  >(
    async (_prev, formData) => {
      const clientId = formData.get("clientId") as string;
      const clientSecret = formData.get("clientSecret") as string;

      try {
        await login(clientId, clientSecret);
        navigate("/", { replace: true });
        return { error: null };
      } catch (e) {
        return {
          error: e instanceof Error ? e.message : "Login failed",
        };
      }
    },
    { error: null },
  );

  return { error: state.error, isPending, formAction };
}
