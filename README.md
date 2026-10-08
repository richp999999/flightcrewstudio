# FlightCrewStudio

A self-hosted web app that downloads your flight log from **GlobaLog**
(`next.app.globalog.com`) and keeps your own copy in a MySQL database. Built with
**C# / Blazor Web App (.NET 9, interactive Server)** and **ASP.NET Core Identity**, and
shipped as a Docker image.

## What it does

- **User accounts** — register, log in, edit display name, change password, and (with SMTP
  configured) password-reset by email. Handled by ASP.NET Core Identity.
- **Settings** — store your GlobaLog credentials. They are **encrypted with a key held on the
  server** (AES-256-GCM) before being written, so the database only ever contains ciphertext.
- **Sync** — a "Sync now" button that fetches flights from GlobaLog, shows live progress, and
  records the last-synced point. That point is **editable**, so you can move it back to
  re-download earlier flights.
- **Flights** — a paginated list of every flight stored locally.
- **Export** — download the logbook as a PDF in the EASA Part-FCL layout (AMC1 FCL.050: A4
  landscape, the 12 standard columns, page totals carried forward, optional title page), or as
  a CSV in logbook.aero's column layout. Times are UTC. Multi-pilot time is logged for every
  flight; PIC/co-pilot time comes from your role; take-offs/landings from "pilot flying". The
  aircraft "Model" column uses each type's ICAO designator (set it under Settings → Aircraft,
  or it's worked out from the type name). PDFs are made with QuestPDF (Community licence).
- **No duplicates** — flights are matched on GlobaLog's own flight `id` (unique per user), so a
  re-sync updates existing flights in place rather than inserting copies. Enforced by a unique
  database index on `(UserId, GlobaLogFlightId)`.

## Important: how GlobaLog authentication works

GlobaLog's documented API authenticates with an **`X-Token` header**, and there is **no API
endpoint that exchanges a username/password for a token**. The username/password is only used by
GlobaLog's *website* login (Devise). This app therefore offers two modes, chosen in **Settings**:

1. **Username & password (default)** — the app logs in to GlobaLog's website with your stored
   credentials and downloads your logbook from the site's own CSV export
   (`/flights/export?format=csv`), the same file the "Export CSV" link on the Flights page gives you.
   GlobaLog's `/api/v1` does **not** accept a web session (it returns 401 without an `X-Token`), so
   the website is used instead:
   - the CSV export gives the flight details;
   - the Flights list pages (`/flights?limit=200&page=N`) give each flight's real GlobaLog id,
     matched to the CSV by (off-block time, callsign);
   - each flight's own page (`/flights/{id}`) gives the crew (Captain, First officer, Cabin crew,
     Passive). Requests are spaced ~150 ms apart, so a first full sync takes a few minutes.

   This mode depends on GlobaLog's login page, export format and page layout staying the same.
2. **API token (`X-Token`)** — if GlobaLog issues you an operator API token, paste it into
   Settings. This uses the documented `/api/v1` endpoints directly and is the robust path.

Use **Test connection** in Settings to see which works for your account. The CSRF/`authenticity_token`
meta tag on GlobaLog's pages is **not** an API token — it's a short-lived, per-session value used
only to submit the login form, and the app reads it automatically during web-session login.

## Quick start (Docker)

```bash
cp .env.example .env         # edit passwords etc.
docker compose up --build    # app on http://localhost:8080
```

On first run the app:
- waits for MySQL, then applies its EF Core migrations (creates all tables), and
- generates an encryption key in the `/keys` volume (unless you set `ENCRYPTION_KEY`).

Then open `http://localhost:8080`, **Register**, go to **Settings** to add your GlobaLog
credentials, and use **Sync**.

### Using your existing MySQL container

You already run MySQL in Docker, so you'll likely want to drop the bundled database:

1. Delete the `db:` service from `docker-compose.yml`.
2. In `.env`, set `DB_HOST`, `DB_PORT`, `DB_NAME`, `DB_USER`, `DB_PASSWORD` to your server. The
   app needs an **empty database** and a user with full rights on it — it builds its own tables.
3. Make sure the `app` container can reach your MySQL. If MySQL is on another compose project's
   network, attach `app` to it, e.g.:

   ```yaml
   services:
     app:
       networks: [default, mysqlnet]
   networks:
     mysqlnet:
       external: true
       name: <your-existing-mysql-network>
   ```
4. Remove the `depends_on: db` block from the `app` service.

## Security notes

- **Credential encryption key** lives on the server only — in the `/keys` volume (auto-generated)
  or in the `ENCRYPTION_KEY` env var. **Back it up.** If you lose it, stored GlobaLog credentials
  can't be decrypted and users must re-enter them. Anyone with both the database *and* this key can
  decrypt credentials, so protect the key accordingly.
- Credentials are encrypted with **AES-256-GCM** (authenticated encryption); stored format is
  `base64(nonce ∥ tag ∥ ciphertext)`.
- The app serves plain HTTP on port 8080 and does **not** force HTTPS redirection, on the
  assumption it sits behind a TLS-terminating reverse proxy (nginx, Traefik, Caddy). Put it behind
  one for any non-local use — Identity auth cookies should only travel over HTTPS.
- GlobaLog passwords are never shown back in the UI; the Settings form leaves the field blank and
  only overwrites the stored value when you type a new one.

## Configuration reference

All settings can be supplied via environment variables (double-underscore = nested key) or
`appsettings.json`:

| Key | Purpose |
|-----|---------|
| `ConnectionStrings__DefaultConnection` | MySQL connection string |
| `Database__ServerVersion` | Optional explicit server version (else defaults to MySQL 8.0.36) |
| `Encryption__Key` | Base64 of 32 bytes; if empty, a key file is used/generated |
| `Encryption__KeyFilePath` | Where to read/create the key file (default `/keys/credential-key`) |
| `GlobaLog__BaseUrl` | GlobaLog base URL (default `https://next.app.globalog.com`) |
| `GlobaLog__TimeoutSeconds` | Per-request timeout (default 60) |
| `Smtp__Host` / `Port` / `Username` / `Password` / `From` / `UseSsl` | Optional email for password reset |

## User types and Admin

Users are **Standard** (the default for new accounts) or **Administrator**. Administrators get an
**Admin** menu (next to Settings) with a sub-menu; its first section is **Airports** — the shared
airport list used for flight departures/arrivals (add, edit, remove unused).

Emails listed under `Admin:AdministratorEmails` are always administrators (checked on every
request and written to the account at startup). `richard.palmer@ripco.co.uk` is built in; add more
with e.g. `Admin__AdministratorEmails__0=someone@example.com` in the environment.

## Crew

Crew members are stored once per person in `CrewMembers` and linked to every flight they were on
through `FlightCrew`, which also records the role held on that flight (a person can be First
officer on one flight and Captain on another). When a flight's crew is loaded, each person is
matched to an existing record by **name** (case-insensitive, spacing tidied) or, where GlobaLog
shows only a **staff number**, by that number. Two different people with exactly the same name are
treated as one person, and someone shown sometimes by name and sometimes by number will appear as
two records. The **Crew** page lists everyone with how many flights you've shared; clicking a
name shows those flights.

## How sync / dedup works

- GlobaLog returns flights **100 per page, ordered by off-block time descending**.
- The app keeps a per-user **high-water mark** (`LastSyncedThrough`, the newest off-block time seen).
  A sync fetches pages from the top and stops as soon as it reaches a flight at/below the mark —
  i.e. it only pulls flights newer than the last sync.
- Clearing the mark (or moving it back) in the **Sync** page forces older flights to be fetched
  again; matching by GlobaLog `id` means they're updated in place, never duplicated.
- Each flight stores departure/arrival airports, aircraft, crew, times, night/instrument minutes,
  radiation amount and GlobaLog's source timestamps.

## Development (without Docker)

Requires the .NET 9 SDK and a reachable MySQL.

```bash
export ConnectionStrings__DefaultConnection="Server=localhost;Port=3306;Database=flightcrewstudio;User=root;Password=root;"
dotnet run                       # or: dotnet watch
# migrations are applied automatically on startup; to manage them manually:
dotnet ef migrations add <Name>
dotnet ef database update
```

## Project layout

```
Program.cs                         app wiring (MySQL, Identity, services, startup migration)
Data/                              EF Core: ApplicationUser, Flight, GlobaLogCredential, SyncState, migrations
Services/
  AesGcmCredentialProtector.cs     server-side credential encryption
  SmtpEmailSender.cs               optional SMTP email
  GlobaLog/                        IGlobaLogClient + web-session and X-Token implementations + factory
  Sync/                            SyncService (background sync) + SyncProgressTracker (live progress)
Components/
  Account/                         Identity UI (register, login, manage, reset password)
  Pages/                           Home, Settings, Sync, Flights
  Layout/                          nav + layout
Dockerfile, docker-compose.yml, .env.example
```
