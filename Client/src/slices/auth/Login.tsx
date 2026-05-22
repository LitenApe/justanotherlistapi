import { LoginView } from "./Login.view";
import { useLoginModel } from "./Login.model";

export function Login() {
  const model = useLoginModel();
  return <LoginView {...model} />;
}
