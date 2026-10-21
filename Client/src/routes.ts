import { index, layout, route, type RouteConfig } from "@react-router/dev/routes";

export default [
  route("sign-in", "auth/SignInPage.tsx"),
  // Everything inside requires a session; the layout redirects to sign-in otherwise.
  layout("auth/ProtectedLayout.tsx", [
    index("lists/ListsPage.tsx"),
    route("lists/:listId", "list/ListPage.tsx"),
  ]),
  route("*", "NotFoundPage.tsx"),
] satisfies RouteConfig;
