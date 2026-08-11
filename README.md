# Princess Dog Walker

A full-stack dog-walking platform with a public website, customer accounts, live services and availability, conflict-safe booking, customer dashboards, and a protected owner dashboard.

## Technology

- Frontend: semantic HTML, responsive CSS, and vanilla JavaScript
- API: ASP.NET Core 10 with JWT authentication and role authorization
- Accounts: ASP.NET Core Identity
- Google accounts: Google Identity Services with backend ID-token verification
- Database: PostgreSQL with Entity Framework Core migrations
- Local orchestration: Docker Compose

## Main structure

```text
frontend/
  index.html, services.html, availability.html, about.html, contact.html
  auth.html, book.html, dashboard.html, owner.html
  css/styles.css
  js/api.js, main.js, auth.js, booking.js, dashboard.js, owner.js, ...
backend/PawsAndPaths.Api/
  Controllers/       Auth, users, dogs, services, availability, bookings, contact
  Models/            AppUser, Dog, ServiceOffering, AvailabilityRule, Booking
  DTOs/              Validated public API contracts
  Services/          Tokens, booking conflicts, availability, owner seeding
  Data/Migrations/   PostgreSQL schema history
tests/               Booking duration, price, and overlap tests
```

## Start with Docker

1. Copy `.env.example` to `.env`.
2. Replace every placeholder. `JWT_KEY` should be a long random value; `OWNER_EMAIL` and `OWNER_PASSWORD` become the only owner account.
3. Run:

   ```bash
   docker compose up --build
   ```

4. Open `http://localhost:5500`.
5. Sign in with `OWNER_EMAIL` to reach the owner dashboard. New public registrations always receive the Customer role.

To enable the Google button locally, create a Google OAuth web client and set `GOOGLE_CLIENT_ID`. Add `http://localhost:5500` as an authorized JavaScript origin.

The API waits for PostgreSQL, applies migrations, creates the Customer and Owner roles, and seeds the configured owner account. The frontend runs on port 5500 and the API on port 5095.

## Run manually

Set configuration in the terminal rather than source code:

```bash
export ConnectionStrings__DefaultConnection='Host=localhost;Port=5432;Database=princessdogwalker;Username=postgres;Password=your-password'
export Jwt__Key='a-long-random-secret-at-least-32-characters'
export Owner__Email='owner@example.com'
export Owner__Password='your-strong-owner-password'
export Google__ClientId='your-google-web-client-id.apps.googleusercontent.com'
```

Then run:

```bash
dotnet tool restore
dotnet restore
dotnet tool run dotnet-ef database update --project backend/PawsAndPaths.Api
dotnet run --project backend/PawsAndPaths.Api
```

In another terminal:

```bash
python3 -m http.server 5500 --directory frontend
```

## Accounts and permissions

- Public visitors can browse Home, Services, Availability, About, and Contact.
- Customers can register, sign in, request/reset a password, edit their profile, manage multiple dogs, book open slots, view history, and cancel eligible bookings.
- Customers can also sign up or sign in with a verified Gmail or Google Workspace account when `Google__ClientId` is configured.
- The Owner role can view all customers and bookings, change booking statuses, and create/edit/disable/delete services and availability rules.
- Role decisions are made by ASP.NET authorization policies, never trusted from frontend state.
- JWTs are kept in browser session storage, so closing the tab ends the browser session.
- Development can expose a password-reset token for local testing. Production keeps tokens private; connect an email provider before launch.

## Availability and booking conflicts

Availability rules support either a recurring weekday or one specific date. Rules can open a time range or block a date/time range. Specific available ranges override recurring available ranges for that date; blocks always remove overlapping time.

The API calculates the booking end time from the selected database service. It rejects requests when the service is inactive, the dog belongs to another account, the time falls outside availability, a block overlaps it, or another Pending/Confirmed booking overlaps it. Creation uses a serializable PostgreSQL transaction plus an indexed date/start/end range to prevent concurrent double-booking attempts.

## Key endpoints

| Area | Endpoints |
|---|---|
| Authentication | `POST /api/auth/register`, `/login`, `/forgot-password`, `/reset-password` |
| Google authentication | `GET /api/auth/google-config`, `POST /api/auth/google` |
| Profile | `GET/PUT /api/users/me`, `GET /api/users/customers` (Owner) |
| Dogs | `GET/POST /api/dogs`, `GET/PUT/DELETE /api/dogs/{id}` |
| Services | `GET /api/services`, Owner `POST`, `PUT /{id}`, `DELETE /{id}` |
| Availability | `GET /api/availability/slots`, Owner rule `GET/POST/PUT/DELETE` |
| Bookings | Customer `GET/POST`, cancel; Owner list and status update |
| Contact | `POST /api/contact` |

## Database relationships

- `AppUser` has many `Dogs` and many `Bookings`.
- `Dog` belongs to one user and has many bookings.
- `ServiceOffering` has many bookings; booking price is snapshotted so later price edits do not change history.
- `Booking` belongs to a user, dog, and service and stores date, start/end, instructions, status, and creation time.
- `AvailabilityRule` stores recurring weekday or specific-date available/blocked ranges.
- Identity tables store password hashes, roles, reset tokens, lockout data, and security stamps.

The `PrincessDogWalkerAccounts` migration replaces the earlier appointment-request prototype tables with the account-based schema. Back up any real prototype data before applying it because those old tables are removed.

## Testing

```bash
dotnet test
```

Tests verify that booking duration and price come from the database service and that overlapping active bookings are rejected.

## Before production

- Connect an email provider for password reset delivery and contact notifications.
- Use HTTPS and a production secret manager for database, JWT, and owner credentials.
- Set absolute production Open Graph image URLs.
- Add audit logging, backup policy, rate limiting, and optional email verification.
- Replace the placeholder owner/gallery images and placeholder email with verified business assets.

## Public deployment

`render.yaml` defines one public Docker web service and a private PostgreSQL database. The API serves the frontend from the same public domain, applies migrations, and uses platform-managed secrets. To deploy it, connect this repository as a Render Blueprint and provide the prompted owner email, owner password, and Google client ID. Then add the final `https://<site>.onrender.com` address to the Google OAuth client's authorized JavaScript origins.
