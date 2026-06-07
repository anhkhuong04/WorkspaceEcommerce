# Frontend Rules

React and TypeScript:

- Use TypeScript for all new frontend code.
- Keep page components focused on composition and data flow.
- Extract feature components when a section has meaningful behavior or reuse.
- Avoid `any`; prefer generated or handwritten API types and Zod-inferred form types.
- Keep API calls in a client/service layer, not scattered through UI components.

Forms:

- Use React Hook Form for create, edit, checkout, login, and filter forms with non-trivial state.
- Use Zod for client-side validation and user-friendly messages.
- Treat frontend validation as UX only; backend remains authoritative.
- Keep form default values explicit.
- Convert strings to numbers, booleans, and dates at boundaries, not inside random components.

State and data loading:

- Separate server data from local UI state.
- Avoid duplicating derived totals or selected entity data unless needed for UX.
- Handle loading, empty, error, and success states intentionally.
- Do not assume admin and storefront data shapes are identical.

UI rules:

- Use Tailwind CSS consistently unless the admin app uses Ant Design components.
- Prefer accessible controls with labels, keyboard support, and visible error states.
- Keep money, stock, status, and dates formatted consistently.
- Do not implement unsupported screens or flows beyond `overview.md`.
