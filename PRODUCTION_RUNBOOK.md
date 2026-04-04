# Production Runbook (Docker host model)

This runbook assumes deployment via the included `Dockerfile` and `docker-compose.production.yml` on a Linux host.

## 1) Required environment variables

Set these in the host environment or an `.env` file consumed by Compose:

- `ConnectionStrings__DefaultConnection`
- `Authentication__Google__ClientId`
- `Authentication__Google__ClientSecret`
- `RoundNotificationEmail__FromAddress`
- `RoundNotificationEmail__SiteUrl`

Optional (defaults shown in compose file):

- `RoundNotificationEmail__SenderUserId` (`me`)
- `RoundNotificationEmail__ApplicationName` (`Golf Scheduler`)

Configured by mount path in compose:

- `RoundNotificationEmail__CredentialsFilePath=/run/secrets/gmail_credentials.json`
- `RoundNotificationEmail__TokenDirectoryPath=/var/opt/golfscheduler/google-token`

## 2) Secret setup

1. Create host directories:
   - `/opt/golfscheduler/secrets`
   - `/opt/golfscheduler/state/google-token`
2. Place Gmail OAuth client credential JSON at:
   - `/opt/golfscheduler/secrets/gmail_credentials.json`
3. Lock down permissions:
   - `chown root:root /opt/golfscheduler/secrets/gmail_credentials.json`
   - `chmod 600 /opt/golfscheduler/secrets/gmail_credentials.json`
   - `chmod 700 /opt/golfscheduler/state/google-token`
4. Store the database connection string and Google OAuth client ID/secret in your host secret manager (or protected `.env` with strict file ACLs).

## 3) Gmail/API setup

1. In Google Cloud Console, enable Gmail API for the project.
2. Configure OAuth consent and publish/test according to your org policy.
3. Create OAuth credentials (Desktop/Installed app or flow compatible with your token bootstrap process).
4. Download the credentials JSON and place it at `/opt/golfscheduler/secrets/gmail_credentials.json`.
5. Ensure `RoundNotificationEmail__FromAddress` is a mailbox permitted by the authenticated Gmail account.
6. Ensure `RoundNotificationEmail__SiteUrl` points at the public HTTPS URL users click in emails.

## 4) DB migration step (required before app start)

The app fails fast if EF migrations are pending. Run migration as a release step before bringing traffic back.

Example:

```bash
# Build image
docker compose -f docker-compose.production.yml build

# Run migrations from a one-off container (uses same env + mounts)
docker compose -f docker-compose.production.yml run --rm \
  -e ASPNETCORE_ENVIRONMENT=Production \
  web dotnet ef database update --project GolfScheduler.csproj

# Start service only after migration succeeds
docker compose -f docker-compose.production.yml up -d web
```

## 5) Deployment / restart procedure

```bash
docker compose -f docker-compose.production.yml pull || true
docker compose -f docker-compose.production.yml build
docker compose -f docker-compose.production.yml up -d web
docker compose -f docker-compose.production.yml ps
```

Post-deploy checks:

- `docker compose -f docker-compose.production.yml logs --tail=200 web`
- Verify app responds on `http://<host>:8080` behind your reverse proxy.
- Validate login and round notification functionality.

## 6) Backup and restore expectations

### What must be backed up

1. Primary PostgreSQL database (`GolfScheduler` schema + data).
2. `/opt/golfscheduler/state/google-token` (Gmail token cache).
3. Secret material managed outside source control (credential JSON and secret-manager values).

### Backup frequency (recommended baseline)

- Database: daily full backup + transaction log backups per RPO policy.
- Token directory: daily snapshot (small but operationally important).
- Secrets: follow your platform secret rotation and backup standards.

### Restore notes

1. Restore PostgreSQL database to target point-in-time.
2. Restore `/opt/golfscheduler/state/google-token` to preserve Gmail refresh tokens.
3. Rehydrate secrets and env vars.
4. Re-run `docker compose ... up -d web` and verify health.

## 7) Log locations

- Container stdout/stderr via Docker logging driver:
  - `docker compose -f docker-compose.production.yml logs web`
- Optional persisted host logs if your Docker daemon is configured with file logging:
  - commonly `/var/lib/docker/containers/<container-id>/` (driver dependent)
- Reverse proxy logs (if used) are outside this repo and should be included in your host runbook.

## 8) Rollback steps

If a deployment is unhealthy:

1. Keep current container running long enough to capture logs:
   - `docker compose -f docker-compose.production.yml logs --tail=500 web`
2. Re-deploy last known good image tag:
   - update `image:` tag or retag local cache
   - `docker compose -f docker-compose.production.yml up -d web`
3. If issue is schema-related and migration was applied, restore DB from backup to pre-release point and deploy prior image.
4. Confirm login, round creation, and notification email path before reopening traffic.

## 9) Minimal first-time bootstrap checklist

- [ ] Host has Docker + Compose plugin installed.
- [ ] Reverse proxy configured for HTTPS and forwards to container port `8080`.
- [ ] Production env vars are set.
- [ ] Gmail credentials file mounted and readable.
- [ ] Token directory is writable and persisted.
- [ ] EF migration command succeeds.
- [ ] App starts and passes smoke checks.
