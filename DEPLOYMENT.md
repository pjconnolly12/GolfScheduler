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
