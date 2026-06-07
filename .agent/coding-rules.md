# Coding Rules

General rules:

- Read `overview.md` before changing business behavior.
- Explain the implementation plan and affected files before modifying code.
- Make the smallest complete change that satisfies the requested scope.
- Do not change unrelated files, broad formatting, public contracts, routes, database schema, or generated files without a task reason.
- Follow existing project conventions when they are compatible with these rules.

Clean code:

- Prefer intention-revealing names over comments.
- Keep methods short enough to express one idea; extract private methods for named business steps.
- Avoid boolean traps in public APIs; use explicit commands, enums, or named options.
- Avoid primitive obsession for important domain concepts when a value object would clarify rules.
- Keep constructors valid; do not allow entities or command objects to exist in impossible states if the rule belongs there.
- Return clear success/failure results from Application use cases; do not use exceptions for normal validation flow.
- Use exceptions for unexpected failures and infrastructure errors.

SOLID:

- Single Responsibility: each class should have one reason to change.
- Open/Closed: extend behavior through new use cases or strategies only when variation exists now.
- Liskov Substitution: do not create inheritance hierarchies that weaken domain invariants.
- Interface Segregation: keep ports small and use-case oriented.
- Dependency Inversion: Application defines abstractions; Infrastructure implements them.

C# rules:

- Enable nullable-aware code patterns; avoid `!` unless the invariant is proven locally.
- Use async APIs for I/O and EF Core access.
- Pass `CancellationToken` through controllers, Application services, and EF Core calls.
- Prefer immutable DTOs/records for request and response models when practical.
- Avoid static mutable state.
- Keep mapping explicit unless the project already uses a mapper consistently.

Frontend rules:

- Use TypeScript types for API inputs and outputs.
- Avoid `any`; use narrow types, discriminated unions, or schema inference from Zod where useful.
- Keep server state, form state, and local UI state separate.
- Do not put business rules in React components when the backend must enforce them.

Testing rule:

- Add tests for changed business rules, validators, status transitions, checkout, inventory, persistence behavior, and API contracts when relevant.
