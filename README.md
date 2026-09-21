# Sysslo

> A family app for handing out, tracking and rewarding household chores.

[![CI](https://github.com/DennisCederqvist/Syssloappen/actions/workflows/ci.yml/badge.svg)](https://github.com/DennisCederqvist/Syssloappen/actions/workflows/ci.yml)

**Live: [sysslo.dxcode.se](https://sysslo.dxcode.se)** · Svenska: [README.sv.md](README.sv.md)

Sysslo is a web app where parents create chores and hand them out to their kids, and kids open the app on their own tablet or phone, see what they should do today, and tick it off. Parents approve the work, the kids earn points, and the points can be spent on rewards the family defines.

**Adults manage. Kids do. Everyone sees the right information.**

## Screenshots

<p align="center">
  <img src="docs/screenshots/child-home.png" alt="The child's view on a tablet: today's chores as colourful cards with a button to report each one as done, and the child's points in the corner" width="760">
</p>
<p align="center"><em>The child's view, on a tablet.</em></p>

<table align="center">
  <tr>
    <td align="center" valign="top">
      <img src="docs/screenshots/adult-home.png" alt="The adult home screen on a phone: each child with their points and progress for the day, chores waiting for approval and reward requests" width="280">
    </td>
    <td align="center" valign="top">
      <img src="docs/screenshots/adult-child-profile.png" alt="A child's profile as an adult sees it: the child's points, current chores and upcoming chores with their dates and schedules" width="280">
    </td>
  </tr>
  <tr>
    <td align="center"><em>The adult home screen, on a phone.</em></td>
    <td align="center"><em>A child's profile, with upcoming chores.</em></td>
  </tr>
</table>

<p align="center"><sub>The app is shown in Swedish here; it is also available in English.</sub></p>

## Features

**For adults**

- Create a family (a *household*), invite a second adult with a one-time code, and manage who belongs to it.
- Add children, each with their own login. Connect a child's device by a one-time code or by scanning a QR code; a family code plus the child's name and password works as a fallback.
- Keep a bank of reusable chores with a free point value and an optional picture.
- Hand out a chore for a specific date, once or on a schedule: every day, every week, every month, or on chosen weekdays. Missed recurring chores roll forward and are replaced by the next occurrence, so they never pile up.
- Edit a chore's date or schedule later, and see upcoming chores that the child cannot see yet.
- Approve a finished chore, or send it back for another go with a comment.
- See each child's current points on the home screen and on the child's profile.
- Create a reward catalogue with a stock count and pictures. Approve, decline or mark requested rewards as delivered.
- Browse a history of finished chores and rewards.

**For children**

- A simple, playful view of today's chores, and nothing else to get lost in.
- Report a chore as done, see when it needs to be redone, and watch the points add up.
- Browse the rewards the family offers and ask for one. The points are reserved until an adult answers.
- New chores and answers show up live, without reloading. Push notifications can be enabled for when the app is closed.

**For the whole family**

- Installable as an app on phones and tablets (PWA).
- Swedish and English, switchable at any time.
- Responsive and checked for accessibility.
- Email confirmation, password reset, and a way to permanently delete the family and all its data (GDPR).

## Using the app

1. **Create a family.** Open [sysslo.dxcode.se](https://sysslo.dxcode.se), choose *Create account*, and confirm your email address. Save the family code you are shown; it is only displayed once.
2. **Add a child.** Go to *Settings → Children and accounts*, add the child, and choose a name and password.
3. **Pair the child's device.** Press *Pair device* on the child. On the child's tablet, open the login page, choose *Child*, and enter the one-time code (or scan the QR code).
4. **Create and hand out a chore.** In *Chores*, create a chore and press *Assign*. Pick the child, a date, and optionally a schedule.
5. **Follow up.** When the child reports a chore as done it shows up on the home screen. Approve it to give the points.

### Install it as an app

- **iPhone / iPad (Safari):** tap the Share icon and choose *Add to Home Screen*.
- **Android (Chrome):** open the menu (three dots) and choose *Install app*.

The same instructions are in the app's help page.

## How it is built

| | |
| --- | --- |
| **Frontend** | Angular 22, TypeScript, Tailwind CSS 4, Transloco (i18n), service worker (PWA) |
| **Backend** | C#, ASP.NET Core (.NET 10), Entity Framework Core, ASP.NET Core Identity, SignalR |
| **Database** | PostgreSQL (Supabase in production) |
| **Other** | Resend (email), Web Push with VAPID, Supabase Storage or local disk for images |
| **Hosting** | Render (one Docker image serving both API and app), GitHub Actions for CI |

```text
┌──────────────────────────┐
│     Angular (PWA)        │   phone / tablet / desktop
└────────────┬─────────────┘
             │  HTTPS: JSON + SignalR (live updates)
             ▼
┌──────────────────────────┐
│    ASP.NET Core API      │   auth, permissions, business rules,
│                          │   household isolation, notifications
└────────────┬─────────────┘
             │  Entity Framework Core
             ▼
┌──────────────────────────┐
│       PostgreSQL         │
└──────────────────────────┘
```

In production the API also serves the built Angular app, so the two share one origin and need neither CORS nor cross-site cookies.

### The one rule: household isolation

Everything belongs to a **household**, and a user must never be able to reach another household's data, even by calling the API directly. Every query combines the requested id with the signed-in user's household, and the tests check it. Some other choices that follow from the same care:

- One-time codes (device pairing, invitations) are random, short-lived, single-use, and only their hash is stored.
- Confirmation and reset links are built from a configured base URL, never from the request's Host header.
- Row Level Security is enabled on every table, so the database's public API exposes nothing.
- Reward points are reserved atomically, so points cannot be spent twice.

## Run it locally

You need the **.NET 10 SDK**, **Node.js 22** with npm, and a local **PostgreSQL**.

```bash
# 1. Backend: point it at your database, apply the migrations and start the API
cd backend/Syssloappen.Api
dotnet user-secrets set "ConnectionStrings:SyssloappenDatabase" "Host=localhost;Port=5432;Database=syssloappen_dev;Username=postgres;Password=YOUR_PASSWORD"
dotnet tool restore
dotnet ef database update
dotnet run --launch-profile http        # http://localhost:5047

# 2. Frontend (in a second terminal)
cd frontend
npm ci
npm start                               # http://localhost:4200, proxies /api and /hubs to the API
```

- Restart the API after every backend change; `dotnet run` does not reload code. Run `dotnet ef database update` again after pulling new migrations.
- Without an email provider configured, the confirmation and reset links are printed in the API console instead of being sent.

### Configuration

Set these with `dotnet user-secrets` locally, or as environment variables (`Section__Key`) when deployed. All are optional except the connection string.

| Key | Purpose |
| --- | --- |
| `ConnectionStrings:SyssloappenDatabase` | PostgreSQL connection string (required) |
| `PublicBaseUrl` | Base URL used in emailed links (set in `appsettings*.json`) |
| `Email:Provider` = `Resend`, `Email:Resend:ApiKey` | Send real email instead of logging it |
| `Storage:Provider` = `Supabase`, `Storage:Supabase:{Url,ServiceKey,Bucket}` | Store images in Supabase instead of on local disk |
| `WebPush:PublicKey`, `WebPush:PrivateKey`, `WebPush:Subject` | Enable push notifications (a VAPID key pair) |

### Tests

```bash
dotnet test backend/Syssloappen.Api.Tests      # ~250 integration tests, in-memory SQLite, no database needed
cd frontend && npx ng test --watch=false       # unit tests
cd frontend && npm run e2e                     # browser tests (Playwright), needs the API and a database running
```

CI runs the backend and frontend tests on every push and pull request.

### Deploying

The Dockerfile builds the frontend and the API into a single image. Build it from the repository root:

```bash
docker build -f backend/Syssloappen.Api/Dockerfile -t syssloappen-api .
```

Give it the configuration above as environment variables. Migrations are not applied automatically; run `dotnet ef database update` against the production database when a release adds one.

## Project structure

```text
backend/
  Syssloappen.Api/          ASP.NET Core API: controllers, models, migrations, services, SignalR hub
  Syssloappen.Api.Tests/    integration tests
frontend/
  src/app/features/adult/   the adult views
  src/app/features/child/   the child views
  src/app/core/             auth, i18n, real-time connection
  public/i18n/              Swedish and English texts
docs/                       HANDOFF.md (technical status and decisions), design specs
REQUIREMENTS.md             user stories and acceptance criteria
```

- [`REQUIREMENTS.md`](REQUIREMENTS.md) is the specification: what the system should do, and what is done.
- [`docs/HANDOFF.md`](docs/HANDOFF.md) is the technical status: decisions, gotchas, and what changed when.

## Status and what is next

Everything in the feature list above is built and running in production. Not built, and not planned yet: weekly allowance, badges and achievements, statistics, calendar import, "every other week" schedules, exceptions for single dates, offline mode, and a public landing page. Push notifications are implemented but still to be verified on real devices in production.

## About the project

Sysslo started as a learning project, a way to build something real end to end with a modern frontend, a separate API, authentication and a relational database, and it is still built with that in mind: the code should be understandable, and it should be clear *why* something works. It grew into a fully working app along the way.

The principles it follows:

- **Simple for kids.** A child should be able to use it without instructions.
- **Clear roles.** Adults and children have different needs and different views.
- **The backend owns security.** The frontend is never the only thing standing between a user and data they should not see.
- **Build what is needed.** Core first, then more.

## License

[MIT](LICENSE) © 2026 Dennis Cederqvist
