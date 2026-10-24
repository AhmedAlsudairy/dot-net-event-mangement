# Guest Workshop & Event Management (ASP.NET Core 8, one app)

One ASP.NET Core MVC app serves both the internal site (AD/LDAP login) and the public QR pages (`/p/{token}`).

## Run (development, no AD needed)
    cd src/GuestEvents
    set ASPNETCORE_ENVIRONMENT=Development
    dotnet run --urls https://localhost:7100
Dev login users `staff`, `hod`, `media`, `admin`, password `dev` (Auth:Mode=Dev). SQLite DB is created in `App_Data/`.
`python smoke_test.py` runs an end-to-end check against the running dev instance. `docs/sample-event.png` is a generated event image.

## Production configuration
LDAPS and SMTP are set in the app: sign in as an Admin -> **LDAP & Email** page (host, port, SSL, base DN, service account, group names, SMTP host/port/STARTTLS/user/from) with Test buttons.
Saved values override appsettings.json; passwords are encrypted (Data Protection keys in `App_Data/keys`, back it up with the DB; on Windows the keys are also DPAPI-protected).
First-time bootstrap (before anyone can sign in) still uses appsettings.json / environment variables:
- `Auth:Mode` = `Ldap`; `Ldap:*` (LDAPS 636, read-only service account, groups Events-Staff/HOD/Media/Admin).
  Put `Ldap__ServiceAccountPassword`, `Smtp__Password`, `Security__HashSecret` in environment variables or a secret store, not in the file.
- `Db:Provider` = `SqlServer` and `ConnectionStrings:Default`.
- `Smtp:Enabled` = true plus host/port/credentials.
- `App:PublicHost` / `App:PublicBaseUrl` / `App:InternalBaseUrl`.

## Other sections
- **External visits (bus requests):** Staff request a bus for a student trip. Who approves, who confirms the bus and who gets the emails is set in Settings > External visits (bus).
- **Research:** Staff upload a research paper or proposal (PDF, DOC, DOCX). It is emailed to the research in-charge and saved in the archive; there is no approval. The in-charge has the **Research** role (AD group `Research-InCharge` by default, or assign it on the Roles page). Set the in-charge email in Settings > Research.
- **Fetch groups from LDAP:** on the LDAP & Email page press *Fetch groups from LDAP*, then press a role button next to a group to put it in that role's box. Boxes turn green when the group exists and red when it does not.

## One app, two faces
Set `App:PublicHost` (e.g. `qr.company.com`). Requests to that host can reach ONLY `/p/*`, `/lang` and static files (everything else is 404).
Publish the public hostname through your reverse proxy / firewall to the internet; keep the internal hostname inside the network.
Public pages need no AD access. Rate limits per IP are in `Public:*`.

## Rules implemented
- Working week Sunday-Thursday (reminder to HOD after N working days, default 2).
- Rejected request keeps its ID; staff edit and resubmit; every decision is a row in Approvals.
- Attendance (named) and Evaluation (anonymous) are separate tables with no link; device hashes use different keys.
- Evaluation question labels are editable in Settings (4 optional slots: presenter, content, venue, food); the overall rating is fixed.
