# Quality Standards

Definition of good code in this project:

- Correct against `overview.md`.
- Easy to locate by module and layer.
- Small enough to change safely.
- Explicit about business intent.
- Testable without HTTP or database when testing Application logic.
- Consistent with ASP.NET Core, EF Core, PostgreSQL, React, and TypeScript conventions.

Design quality checks:

- A controller action must not contain branching business workflow.
- An Application use case must not depend on EF Core concrete types unless the project intentionally uses DbContext as the unit-of-work abstraction.
- A Domain entity must not depend on ASP.NET Core, EF Core attributes, HTTP, JSON, or UI concepts unless no better option exists.
- A repository or query service must not hide unrelated business decisions.
- A React component must not duplicate backend validation that affects correctness; frontend validation is for UX and early feedback.

Maintainability checks:

- Prefer vertical feature clarity over over-engineered generic layers.
- Keep abstractions close to current needs; do not build plugin systems, event buses, CQRS frameworks, or microservice seams unless requested.
- Keep naming consistent with `overview.md` and existing code.
- Avoid hidden side effects in methods named like queries.
- Keep date/time generation behind a clock abstraction when business behavior depends on current time.
- Keep money calculations explicit and decimal-based on the backend.

Quality gates before final answer:

- Build or test the changed area when feasible.
- Check that no unrelated files were modified.
- Check that new code does not introduce out-of-scope behavior.
- Check that validation, authorization, transaction, and persistence boundaries are appropriate for the change.
- Report commands run and checks not run.
