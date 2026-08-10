# Runtime Configuration Matrix

This matrix records configuration **names, authority, and operational ownership** only.
Do not place values, screenshots containing values, connection strings, JWTs, recovery
codes, or provider credentials in this file. `Development` may use an ignored `.env` or
`appsettings.Local.json`; CI uses synthetic values; staging and production use the
platform secret/configuration authority.

| Area | Keys | Development | CI | Staging / Production authority | Owner | Rotation / review |
| --- | --- | --- | --- | --- | --- | --- |
| Database | `ConnectionStrings:DefaultConnection`, `POSTGRES_*` | Local PostgreSQL only | Disposable PostgreSQL only | Secret manager + managed PostgreSQL workload identity/secret | Platform + DBA | Password/role after exposure; quarterly access review |
| Admin authentication | `AdminAuth:*` | Ignored local config | Synthetic | Secret manager, least-privilege release access | Application security | Password on exposure and at least every 90 days |
| JWT | `Jwt:*` | Ignored local config | Synthetic | Secret manager; documented key rollover window | Application security | Key rollover at least every 90 days or forced session expiry |
| Data Protection | `DataProtection:KeyRingPath` | Local ignored path | Ephemeral test path | Encrypted, persistent shared mount/managed key store | Platform | Access/key-ring recovery rehearsal quarterly |
| Google OAuth | `GoogleAuth:*`, frontend `VITE_GOOGLE_CLIENT_ID` | Local public client ID optional | Disabled/synthetic | Server audience list from configuration authority; public client ID in frontend build | Application security | Review on client/domain change; disable/revoke on compromise |
| Email | `EmailDelivery:*` | Logging provider only | Logging provider only | SMTP sandbox/production secrets from secret manager | Platform + product ops | Provider credential after exposure / quarterly |
| Durable media | `MediaStorage:*`, `MediaStorage:NoOpMalwareScannerRisk*` | Local or isolated MinIO | Local only | S3-compatible bucket, encryption, restricted workload credential; temporary NoOp scanner exception needs named security owner, risk reference, and <=90-day expiry | Platform + storage owner + application security | Credential / bucket policy on change; security-risk renewal before expiry; quarterly restore review |
| Payment | `Payment:VNPay:*` | Sandbox only | Synthetic callback values | Provider portal + secret manager | Payments owner | Hash secret/merchant setup on exposure or provider request |
| MiniLogistics | `MiniLogistics:*` | Local/sandbox only | Fake provider | Provider portal + secret manager | Logistics owner | API/webhook secret on exposure or provider request |
| Browser origin and host | `AllowedHosts`, `Cors:AllowedOrigins`, `Storefront:BaseUrl`, `MediaStorage:PublicBaseUrl` | Localhost only | Test-only host | Exact public HTTPS names only | Platform + frontend owner | Review with every domain/ingress change |
| Proxy / topology | `ForwardedHeaders:KnownProxies`, replica/backplane/edge limiter settings | Empty/direct | Test-only | Platform-controlled immediate proxy IPs and shared service references | Platform/SRE | Review every ingress, network, or scaling change |
| Process limits | `RuntimeLimits:*` | Repository defaults | Repository defaults | Bounded values, changed only with load-test evidence | Platform/SRE + application | Review after a capacity or upload-policy change |
| Telemetry | `APPLICATIONINSIGHTS_CONNECTION_STRING` / `ApplicationInsights:ConnectionString` | Optional | Omitted/synthetic | Secret/config authority; redaction policy in code | Platform + observability owner | Access review quarterly; rotate on exposure |

## Startup safety contract

Outside Development, existing provider validators reject local media storage, logging
email delivery, an unaccepted/expired NoOp media-scanner exception, missing production
CORS origins, placeholder credentials, and missing Data Protection/Application Insights
configuration. In Production,
`ProductionRuntimeConfigurationValidator` additionally rejects wildcard/localhost
`AllowedHosts`, a relative/non-external Data Protection key-ring path, an empty or
placeholder telemetry connection string, and a non-HTTPS storefront URL.

Changing a value follows the [credential rotation runbook](credential-rotation.md).
Changing a public endpoint, proxy, media URL, or cookie/CORS policy requires the
staging ingress smoke in the [production release runbook](production-release.md).

## Environment hand-off record

For every staging/production candidate, the Platform/SRE owner records externally:

1. Environment, candidate commit/digest, config version/secret-manager revision, and
   the responsible owners above (never values).
2. Confirmation that old credentials reject authentication after rotation.
3. Data Protection shared-key-ring persistence/encryption/access evidence.
4. Exact public host/origin/proxy topology and cookie/CORS/HTTPS behavior.
5. Any accepted forced-session-expiry window for JWT/key rotation.
