import boundaries from "eslint-plugin-boundaries";
import reactHooks from "eslint-plugin-react-hooks";
import tseslint from "@typescript-eslint/eslint-plugin";
import tsparser from "@typescript-eslint/parser";

export default [
  {
    ignores: ["dist/**", "node_modules/**"],
  },
  {
    files: ["src/**/*.{ts,tsx}"],
    languageOptions: {
      parser: tsparser,
      parserOptions: {
        ecmaVersion: "latest",
        sourceType: "module",
        ecmaFeatures: { jsx: true },
      },
    },
    plugins: {
      "@typescript-eslint": tseslint,
      "react-hooks": reactHooks,
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
      "import/resolver": {
        typescript: {
          alwaysTryTypes: true,
        },
      },
    },
    rules: {
      ...reactHooks.configs.recommended.rules,
      "@typescript-eslint/consistent-type-imports": [
        "error",
        { prefer: "type-imports", fixStyle: "inline-type-imports" },
      ],
      "boundaries/element-types": [
        "error",
        {
          default: "disallow",
          rules: [
            { from: "shared", allow: ["shared"] },
            { from: "components", allow: ["shared", "slice", "components"] },
            { from: "slice", allow: ["shared", "slice"] },
            { from: "app", allow: ["shared", "slice", "components"] },
            { from: "main", allow: ["shared", "slice", "components", "app"] },
          ],
        },
      ],
      "boundaries/entry-point": [
        "error",
        {
          default: "allow",
          rules: [
            { target: ["slice"], disallow: "*" },
            { target: ["slice"], allow: "index.ts" },
          ],
        },
      ],
    },
  },
];
