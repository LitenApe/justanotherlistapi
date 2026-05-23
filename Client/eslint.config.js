import tseslint from "typescript-eslint";
import reactHooks from "eslint-plugin-react-hooks";
import boundaries from "eslint-plugin-boundaries";

export default tseslint.config(
  {
    ignores: ["dist/**", "node_modules/**"],
  },
  ...tseslint.configs.recommended,
  reactHooks.configs.flat.recommended,
  {
    files: ["src/**/*.{ts,tsx}"],
    plugins: {
      boundaries,
    },
    settings: {
      "boundaries/elements": [
        { type: "shared", pattern: "src/shared/**" },
        { type: "components", pattern: "src/components/**" },
        {
          type: "slice",
          pattern: "src/slices/*",
          mode: "folder",
          capture: ["sliceName"],
        },
        { type: "app", pattern: "src/App.tsx" },
        { type: "main", pattern: "src/main.tsx" },
      ],
      "boundaries/dependency-nodes": ["import"],
      "import/resolver": {
        typescript: {
          alwaysTryTypes: true,
        },
      },
    },
    rules: {
      "@typescript-eslint/no-unused-vars": [
        "error",
        { argsIgnorePattern: "^_", varsIgnorePattern: "^_" },
      ],
      "@typescript-eslint/consistent-type-imports": [
        "error",
        { prefer: "type-imports", fixStyle: "inline-type-imports" },
      ],
      "no-restricted-imports": [
        "error",
        {
          patterns: [
            {
              group: ["../*", "../"],
              message:
                "Use path aliases (@shared/, @slices/, @components/) instead of parent directory imports.",
            },
          ],
        },
      ],
      "boundaries/dependencies": [
        "error",
        {
          default: "disallow",
          rules: [
            {
              from: { type: "shared" },
              allow: [{ to: { type: "shared" } }],
            },
            {
              from: { type: "components" },
              allow: [
                { to: { type: "shared" } },
                { to: { type: "slice", internalPath: "index.ts" } },
                { to: { type: "components" } },
              ],
            },
            {
              from: { type: "slice" },
              allow: [
                { to: { type: "shared" } },
                { to: { type: "slice", internalPath: "index.ts" } },
              ],
            },
            {
              from: { type: "app" },
              allow: [
                { to: { type: "shared" } },
                { to: { type: "slice", internalPath: "index.ts" } },
                { to: { type: "components" } },
              ],
            },
            {
              from: { type: "main" },
              allow: [
                { to: { type: "shared" } },
                { to: { type: "slice", internalPath: "index.ts" } },
                { to: { type: "components" } },
                { to: { type: "app" } },
              ],
            },
          ],
        },
      ],
    },
  },
);
