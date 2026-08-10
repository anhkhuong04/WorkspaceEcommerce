# PRH-018 release-candidate gate

PRH-018 is an approval gate, not a deployment command. The release manager creates a
new JSON manifest from
[`prh-018-release-candidate-manifest.example.json`](prh-018-release-candidate-manifest.example.json)
outside the repository evidence template, then runs this validation from the exact
candidate checkout:

```powershell
./scripts/verify-prh-018-release-candidate.ps1 `
  -EvidenceManifestPath '<protected-evidence-directory>/release-candidate.json'
```

The validator requires all of the following before it returns success:

- the checked-out commit exactly matches `candidateCommit`;
- the worktree is clean;
- `imageReference` is a fully qualified immutable digest (tags are rejected);
- every required CI, migration, security, browser, contract/authorization,
  two-replica, load/recovery, telemetry, and configuration gate is `Passed` with an
  retained, non-placeholder evidence reference; and
- no critical finding is accepted, and each accepted high finding has an owner,
  rationale, non-past ISO remediation date, and verification evidence.

The example intentionally contains `Pending` gates and must fail. It is a schema
template, never evidence. Store the filled record in the protected release-evidence
location and do not include credentials, tokens, customer data, webhook bodies, or
Data Protection material.

The script does not prove an external result by itself: the linked evidence must come
from the immutable candidate deployed to the approved staging topology. `-AllowDirtyWorktree`
exists only for schema debugging and is prohibited for a release decision.

The release manager also attaches the required human approvals (application,
QA/security, Platform/SRE, and release) to the same record. A failed or pending gate
is a no-go and may not be bypassed with a mutable image tag or a new local build.
