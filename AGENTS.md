# Codex Guide: Princess Dog Walker

## Project purpose

This repository contains the production website for **Princess Dog Walker**, a small independent dog-walking and pet-care business operated by Julia in **Boca Raton, Florida**. The public site is available at <https://princess-dog-walker.onrender.com>.

Keep the experience friendly, simple, mobile-friendly, and easy for a nontechnical business owner to manage. The visual identity is feminine and playful: a pink palette with tasteful sparkles, hearts, and bows. Preserve accessibility, readable contrast, keyboard support, and responsive behavior.

## Verified business information

- Business: Princess Dog Walker
- Service area: Boca Raton, FL only
- Address: 5400 Broken Sound Blvd NW, Boca Raton, FL 33487
- Phone: 561-788-3531
- Email: kadulinaiulia@gmail.com
- Instagram: `princess___dogwalker`
- Location link: <https://www.google.com/maps/place/Princess_Dogwalker/@26.3960456,-80.1111725,17z/data=!4m6!3m5!1s0x88d91fb8a7c67e7b:0xaabb94f5f8fc21d0!8m2!3d26.3960456!4d-80.1111725!16s%2Fg%2F11y3x2qh5h!18m1!1e1?entry=ttu&g_ep=EgoyMDI2MDgwNS4xIKXMDSoASAFQAw%3D%3D>

Do not replace these details with placeholders.

## Architecture

- Frontend: semantic HTML, responsive CSS, and vanilla JavaScript in `frontend/`.
- Backend: ASP.NET Core 10 API in `backend/PawsAndPaths.Api/`.
- Authentication: ASP.NET Core Identity, JWTs, Customer/Owner roles, and optional Google Identity Services.
- Database: PostgreSQL through Entity Framework Core migrations.
- Tests: xUnit project in `tests/PawsAndPaths.Api.Tests/`.
- Local environment: Docker Compose.
- Production: one Render Docker web service plus a Render PostgreSQL database. The API serves `frontend/` as static files in production.

Important files:

- `frontend/css/styles.css`: shared visual system and responsive layouts.
- `frontend/js/api.js`: API requests and browser session handling.
- `frontend/js/booking.js`: customer booking flow.
- `frontend/js/owner.js`: owner booking, calendar, services, availability, and customer controls.
- `backend/PawsAndPaths.Api/Services/AvailabilityService.cs`: open-slot and conflict calculations.
- `backend/PawsAndPaths.Api/Services/BookingService.cs`: booking creation and status rules.
- `backend/PawsAndPaths.Api/Services/BookingSchedule.cs`: expands overnight stays into calendar care windows.
- `render.yaml`: production resources and environment configuration.

## Required product behavior

### Services

- Public services are sorted from lowest price to highest price.
- The owner can add, edit, disable, and delete services when allowed.
- A service using multi-day overnight behavior must have `IsOvernightStay` enabled. The owner controls this with **Overnight stay (multi-day care)** in the service editor.
- Never infer overnight behavior only from the service name.

### Availability

- Julia is available every day and night, year-round, by default.
- Availability records are owner-created exceptions that block a whole date or a time range.
- The owner can edit or remove blocks.
- The public availability page supports day, week, month, and year navigation and displays only bookable times.
- Pending and Confirmed bookings remove overlapping public slots. Cancelled and Declined bookings do not block availability.
- Never expose customer names, dogs, notes, or other private booking information on public availability endpoints or pages.

### Regular bookings

- Customers must be signed in and can book only their own saved dogs.
- Booking duration and price come from the selected database service, not browser-submitted values.
- The API must prevent inactive services, past dates, blocked times, ownership violations, and overlapping active bookings.
- Customer-created bookings begin as Pending. Owner-created bookings begin as Confirmed.
- Julia can create a booking for a registered customer and one of that customer’s saved dogs from the protected owner dashboard.

### Overnight stays

- Overnight stays are one multi-day booking with check-in and checkout dates.
- The default care schedule is:
  - Overnight care from 10:00 PM to 9:00 AM.
  - Midday care from 2:00 PM to 3:00 PM on full middle days.
- A middle day in Julia’s calendar therefore shows three care markers: overnight until 9:00 AM, midday from 2:00–3:00 PM, and overnight beginning at 10:00 PM.
- The first date shows the evening overnight start; the checkout date shows the morning overnight end.
- Only those care windows block other appointments. Julia remains bookable between them.
- Julia can customize the overnight start/end and midday start/end for each overnight booking. Validate the customized schedule against blocks and other bookings.
- The owner calendar uses consistent service-specific colors and a legend. Cancelled and Declined bookings are omitted from the active calendar.

## Authorization and privacy

- Enforce authorization server-side with ASP.NET policies; frontend hiding is not security.
- Owner-only operations must use `[Authorize(Roles = AppRoles.Owner)]`.
- Public registration must never grant the Owner role.
- Never commit passwords, JWT keys, database credentials, Google client secrets, reset tokens, or production connection strings.
- Keep JWTs in session storage under the existing session model unless the user explicitly requests an authentication redesign.
- Escape or assign user-provided content with `textContent`; avoid unsafe HTML interpolation.

## Database changes

- Persist business data in PostgreSQL; do not use browser storage as the source of truth.
- Add an Entity Framework migration for every schema change and inspect its `Up` method before committing.
- Render applies migrations on startup through `ApplyMigrationsOnStartup=true`.
- Preserve existing customer and booking data. Do not delete or recreate the production database.

Create a migration with:

```bash
dotnet tool restore
ConnectionStrings__DefaultConnection='Host=localhost;Database=validation;Username=validation;Password=validation' \
Jwt__Key='validation-key-that-is-long-enough-for-hmac-sha256' \
dotnet ef migrations add MigrationName \
  --project backend/PawsAndPaths.Api/PawsAndPaths.Api.csproj \
  --startup-project backend/PawsAndPaths.Api/PawsAndPaths.Api.csproj
```

## Validation before publishing

Run JavaScript syntax checks:

```bash
for file in frontend/js/*.js; do node --check "$file" || exit 1; done
```

Run backend tests with non-secret validation configuration:

```bash
ConnectionStrings__DefaultConnection='Host=localhost;Database=validation;Username=validation;Password=validation' \
Jwt__Key='validation-key-that-is-long-enough-for-hmac-sha256' \
dotnet test PawsAndPaths.slnx
```

Build the same Docker image Render uses:

```bash
docker build -f backend/PawsAndPaths.Api/Dockerfile -t princess-dog-walker:validation .
```

Add or update tests whenever booking conflicts, authorization-sensitive writes, service behavior, availability, or overnight scheduling changes.

## Git and deployment workflow

- Preserve unrelated user changes in the working tree.
- When starting from `main`, create a `codex/` feature branch before committing.
- Stage only the files belonging to the current task.
- The GitHub remote is `https://github.com/olenagalu/dogwalker.git`.
- After validation, push the finished change to GitHub `main` when the user requested a live site update.
- Render does not always deploy automatically. If the public page is still old, instruct the user to open the `princess-dog-walker` service and choose **Manual Deploy → Deploy latest commit**, wait for **Live**, then reload or use **Command + Shift + R**.

Do not create a second Render database or Blueprint for routine updates.
