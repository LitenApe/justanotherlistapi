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
        { type: "slices", pattern: "src/slices/**" },
        { type: "app", pattern: "src/App.tsx" },
        { type: "main", pattern: "src/main.tsx" },
      ],
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
            { from: "components", allow: ["shared", "slices", "components"] },
            { from: "slices", allow: ["shared"] },
            { from: "app", allow: ["shared", "slices", "components"] },
            { from: "main", allow: ["shared", "slices", "components", "app"] },
          ],
        },
      ],
    },
  },
];
