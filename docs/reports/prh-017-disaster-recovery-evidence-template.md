# PRH-017 disaster recovery evidence

> Copy this template into the release evidence system for one rehearsal. Do not include
> credentials, protected keys, tokens, customer data, object contents, or webhook bodies.

## Approval and scope

| Field | Value |
| --- | --- |
| Rehearsal / incident ID | |
| Candidate image digest | `<registry/image@sha256:...>` |
| Git commit / migration version | |
| Recovery environment and isolation controls | |
| Approved RPO / RTO | |
| Requested recovery point | |
| Incident commander / DB / storage / app / release owners | |
| Start / end UTC | |

## Backup and recovery-point evidence

| Component | Backup/version reference | Encryption / retention | Restore identity | Alert test | Result / evidence |
| --- | --- | --- | --- | --- |
| PostgreSQL automated backup/PITR | | | | | |
| PostgreSQL migration job/image | | | | | |
| Object-store original media | | | | | |
| Object-store media variants | | | | | |
| Data Protection key ring | | | | | |
| External credential configuration | | | | | |

## Restore execution log

| Step | Start/end UTC | Operator | Result | Evidence / notes |
| --- | --- | --- | --- |
| Isolated target and outbound sandbox validation | | | | |
| PostgreSQL restore/PITR | | | | |
| Migration history and exact migration-job validation | | | | |
| Original media object restore | | | | |
| Variant object restore | | | | |
| Metadata/reference/checksum/access validation | | | | |
| Application health and media-read validation | | | | |
| Cleanup/revoke temporary access | | | | |

## Media sample reconciliation

| Asset/variant identifier | Source object version | Restored object version | Checksum match | Metadata/reference match | Application read / permission result |
| --- | --- | --- | --- | --- | --- |
| | | | | | |

## Failure and recovery exercises

| Scenario | Expected behavior | Observed behavior | Pass? | Owner / follow-up date | Evidence |
| --- | --- | --- | --- | --- | --- |
| Accidental media deletion | | | | | |
| Corrupted metadata | | | | | |
| Failed migration | | | | | |
| Data Protection key access loss | | | | | |
| Revoked external credential | | | | | |
| Deployment rollback / forward fix | | | | | |

## Achieved objectives and release decision

| Measure | Approved target | Achieved | Result / explanation |
| --- | ---: | ---: | --- |
| RPO | | | |
| RTO | | | |
| PostgreSQL + media recoverability | | | |
| Migration/data compatibility | | | |
| Credential/incident runbook execution | | | |
| Final recommendation | `pass / fail / blocked` | | |
