# Admin App Structure

Route-level screens live under `src/pages/<route-name>/`.

Use a page folder when a route needs local components, forms, modal state, or mutations:

- `PagesPage.tsx` composes data hooks, local hooks, and render components.
- `components/` contains UI pieces that belong only to that route.
- `hooks/` contains route-local state and mutation hooks.

Use `src/features/` only for cross-cutting product areas that are reused across routes, such as authentication/session behavior.

Shared query hooks live in `src/hooks/queries/`. Page components should call these hooks instead of calling `useQuery` directly.
