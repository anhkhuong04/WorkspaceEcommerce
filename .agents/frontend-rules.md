# Frontend Rules

Frontend stack:

- Storefront is React + TypeScript + Tailwind CSS + React Hook Form + Zod.
- Admin is React + TypeScript + Tailwind CSS.
- Do not introduce a different UI framework, form library, validation library, or styling system unless the stack decision is explicitly changed.

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

- Use Tailwind CSS consistently for Storefront and Admin.
- Do not introduce a component framework for Admin unless the stack decision is explicitly changed.
- Design with a light visual tone: white as the primary base plus a deliberate supporting color palette.
- Keep UI clean, modern, and focused on ecommerce/admin workflows; avoid visually noisy sections.
- Build standard UX patterns for navigation, filtering, forms, tables, carts, checkout, status feedback, confirmation dialogs, and empty states.
- Optimize layouts for Full HD screens first: important content should fit cleanly in a 1920x1080 viewport without unnecessary vertical scrolling.
- Keep responsive behavior for smaller screens, but do not sacrifice the Full HD desktop layout.
- Prefer accessible controls with labels, keyboard support, and visible error states.
- Keep money, stock, status, and dates formatted consistently.
- Do not implement unsupported screens or flows beyond `overview.md`.
