---
description: "Use when completing a task, finishing a work chunk, or wrapping up changes. Commit workflow guidance."
applyTo: "**"
---

# Commit After Each Work Chunk

After completing a logical unit of work, always commit with a clear, descriptive message.

## Workflow

1. Run `dotnet build` (backend) and/or `cd Client && npm run check` (frontend) to verify no errors
2. Stage relevant files with `git add`
3. Commit with a conventional message:

```
feat: add member removal endpoint
fix: handle null description in item update
refactor: extract shared authorization helper
test: add integration tests for audit log capture
```

## Message Format

- Use lowercase imperative mood: "add", "fix", "remove" (not "Added", "Fixes")
- Prefix with type: `feat`, `fix`, `refactor`, `test`, `docs`, `chore`
- Keep the subject line under 72 characters
- Add a body for non-obvious changes (separated by blank line)

## When to Commit

- After each completed feature, fix, or refactoring step
- After adding or updating tests
- Before switching to a different area of the codebase
- Do NOT batch unrelated changes into a single commit
