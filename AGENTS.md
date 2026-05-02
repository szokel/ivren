# ivren Codex Instructions

## Project status

This repository is in a stable state.

Do not make broad or speculative changes. Preserve the current architecture and coding style.

## Core rules

- Make minimal, targeted changes only.
- Do not rewrite whole classes unless explicitly requested.
- Do not refactor unrelated code.
- Do not introduce new frameworks, packages, or architectural patterns unless explicitly requested.
- Preserve existing public behavior unless the task explicitly asks for a behavior change.
- Analyze the existing implementation before editing.
- Prefer small, easy-to-review changes.

## Filename and path handling

This project contains Windows filename/path handling logic.

When working with filename sanitization:

- Respect Windows filename rules.
- Invalid filename characters must be handled intentionally.
- Reserved Windows device names such as CON, PRN, AUX, NUL, COM1-COM9, and LPT1-LPT9 must be considered.
- Trailing spaces and trailing dots must be considered.
- Unicode characters must not be removed unless explicitly required.
- Existing tilde-based replacement behavior must not be changed unless explicitly requested.

## Workflow

Before editing code:

1. Read the relevant files.
2. Explain the current behavior briefly.
3. State the intended change.

After editing code:

1. Build the project.
2. Report:
   - changed files
   - commands executed
   - build result
   - remaining risks or manual checks

## Safety

- Do not run destructive commands.
- Do not delete files unless explicitly requested.
- Do not change git history.
- Do not commit automatically unless explicitly requested.
