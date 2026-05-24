---
description: "Use when creating or modifying frontend slices, React components, or client-side feature code"
applyTo: "Client/src/slices/**"
---

# Client Slice Conventions

Each slice follows the MVC pattern in a single component file:

```typescript
// ─── Model ────────────────────────────────────────────────────────────────────
function useSliceModel(): SliceModel { /* state, effects, callbacks */ }

// ─── View ─────────────────────────────────────────────────────────────────────
function SliceView(props: SliceModel) { /* pure JSX */ }

// ─── Controller ───────────────────────────────────────────────────────────────
export function Slice() {
  const model = useSliceModel();
  return <SliceView {...model} />;
}
```

## Rules

- **Slice isolation**: Never import from another slice directly — use barrel exports or shared infrastructure
- **No third-party state management**: Use React 19 primitives (`use`, `useTransition`, `useDeferredValue`, `useOptimistic`, `useActionState`)
- **Two-layer HTTP**: Transport in `@shared/api/client.ts`, business operations in the slice's `api.ts`
- **Path aliases**: Use `@shared`, `@slices`, `@components` — never relative paths crossing boundaries
- **Factory pattern**: Services export a factory function + TypeScript type + default singleton
- **CSS Modules**: Co-locate styles as `Component.module.css`

See [specifications/client.md](../../specifications/client.md) for full conventions.
