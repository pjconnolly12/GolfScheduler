# Deployment Configuration

This application supports **production-safe configuration** using host-provided environment variables and/or your platform secret manager.

> Do not commit live credentials to source control.

## Required production settings

Configure the following keys at deploy time:

### Database
- `ConnectionStrings__DefaultConnection`
  - Must be a real SQL Server connection string for your hosted SQL Server instance.
  - LocalDB values such as `(localdb)` / `mssqllocaldb` are intentionally rejected in Production.

### Google OAuth (app sign-in)
- `Authentication__Google__ClientId`
- `Authentication__Google__ClientSecret`

### Round notification email (Gmail API + outbound links)
- `RoundNotificationEmail__CredentialsFilePath`
- `RoundNotificationEmail__TokenDirectoryPath`
- `RoundNotificationEmail__SenderUserId`
- `RoundNotificationEmail__FromAddress`
- `RoundNotificationEmail__ApplicationName`
- `RoundNotificationEmail__SiteUrl`

`RoundNotificationEmail__SiteUrl` must be the public URL users can access (for example, `https://golf.example.com`).

`RoundNotificationEmail__TokenDirectoryPath` must point to **persistent, writable storage** in production. If your host only provides ephemeral or read-only storage, token refresh data will be lost between restarts and Gmail API authorization can break. In that case, either mount persistent writable storage for token files or redesign the auth flow to avoid local token persistence.

## Example (Linux container/App Service style)

```bash
export ConnectionStrings__DefaultConnection="Server=tcp:sql-prod.example.com,1433;Initial Catalog=GolfScheduler;User ID=golf_app;Password=<secure-password>;Encrypt=True;TrustServerCertificate=False"

export Authentication__Google__ClientId="<google-oauth-client-id>"
export Authentication__Google__ClientSecret="<google-oauth-client-secret>"

export RoundNotificationEmail__CredentialsFilePath="/var/secrets/google/credentials.json"
export RoundNotificationEmail__TokenDirectoryPath="/var/secrets/google/token-round-notifications"
export RoundNotificationEmail__SenderUserId="me"
export RoundNotificationEmail__FromAddress="golf-notify@example.com"
export RoundNotificationEmail__ApplicationName="Golf Scheduler"
export RoundNotificationEmail__SiteUrl="https://golf.example.com"
```

## Secret manager guidance

Use your host's secret facility (for example: Azure App Service app settings + Key Vault references, AWS Secrets Manager, GCP Secret Manager, Kubernetes secrets) to provide sensitive values:

- SQL Server connection string
- Google OAuth client ID/secret
- Gmail credentials file mount path and token storage path

Non-secret defaults can remain in `appsettings.Production.json`.

## Database migration procedure (run before traffic)

The app now **fails fast** on startup when there are pending EF Core migrations. It does not auto-apply schema changes at runtime.

Run migrations as a dedicated deployment step, then start/restart the app:

```bash
# 1) Build artifact/image
dotnet publish -c Release

# 2) Apply migrations against the target production database
dotnet ef database update --project GolfScheduler.csproj

# 3) Start the app only after migrations complete successfully
dotnet GolfScheduler.dll
```

### Recommended rollout order

1. Put instance(s) in maintenance / remove from load balancer.
2. Run `dotnet ef database update` with production connection settings.
3. Verify migration completion (EF reports success and updates `__EFMigrationsHistory`).
4. Start new app instance(s) and return them to the load balancer.

### Why this is required

- Startup schema checks are intentionally minimal: only pending-migration detection.
- If schema is incorrect, startup throws immediately to avoid serving with a partially compatible model.
- `DistributionListMembers` is managed by EF migration `20260304000000_AddUserDistributionList`; manual bootstrap SQL is no longer needed.
