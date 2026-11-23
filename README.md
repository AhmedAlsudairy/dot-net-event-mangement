# Campus Hub: events, visits, research and KPIs (ASP.NET Core 8, one app)

One ASP.NET Core 8 app for a school or department: guest **event requests** with an Admin-configured approval flow, student **bus (external visit) requests**, **research** uploads, and the **KPI dashboard**, all behind one company sign-in, with English and Arabic (right-to-left) throughout. It serves both the internal site (AD/LDAP login) and the public QR check-in pages (`/p/{token}`).

Licensed under the [MIT License](LICENSE).

## What is in it
- **Events:** staff request a guest event; it goes through the approval steps the Admin sets (default HOD, then Dean); after the last approval the event image and QR code are made and the emails go out; guests check in and evaluate on the public QR page; food requests; Excel reports.
- **External visits (bus):** request, separate approver, bus coordinator confirms the bus.
- **Research:** upload a paper or proposal; it is emailed to the research in-charge and kept in an archive.
- **KPIs** (under `/Kpi`): dashboard, tracking table, KPI list, Excel import/export, departments and permissions. Events and visits can be linked to a KPI with a searchable picker. Five KPI roles (manager, editor, department officer, viewer, all-round team).
- **Role-based experience:** a Home page for each role (what is waiting for you), a *My access* page, a clear sign-in page, and a KPI-only landing for KPI roles.
- **Admin tools:** *Access & roles* (one page for every role), *Approval flow*, *Appearance* (themes, own colours, light/dark, corners, fonts, text size, logo, app name), an advanced *Audit log* (filters, detail view, CSV/Excel export, alerts, retention) and the *LDAP & Email* connection page.
- **Works on phones, tablets and desktops;** dark mode and per-user text size.

## Documentation
- [Product requirements (PRD)](docs/PRD.md) - goals, roles, requirements, workflows, architecture, data model, security
- [Setup and deployment guide](docs/SETUP.md) - run it locally, connect the real directory, deploy on Windows or Linux, configuration reference, troubleshooting
- [User guide](docs/USER-GUIDE.md) - how each role uses it (Staff, approvers, Media, Research, KPI people, Admin, attendees)
- [Sitemap](docs/SITEMAP.md) - every page and route, who sees what
- [Test tools](testtools/README.md) - fake directory, mail catcher and the automated checks

## Run (development, no AD needed)
Windows (PowerShell):

    cd src\GuestEvents
    $env:ASPNETCORE_ENVIRONMENT = 'Development'; $env:Auth__Mode = 'Dev'
    dotnet run --urls https://localhost:7100

Linux / macOS:

    cd src/GuestEvents
    ASPNETCORE_ENVIRONMENT=Development Auth__Mode=Dev dotnet run --urls https://localhost:7100

Dev mode has built-in test accounts, all with password `dev`: `staff`, `hod`, `dean`, `media`, `research`, `kpi` (all-round KPI team), `kpimanager`, `kpieditor`, `kpiofficer`, `kpiviewer` and `admin`. The sign-in page lists them: click one to sign in. SQLite databases are created in `App_Data/` (`events.db` and `kpi_dashboard.db`). **Never use Dev mode on a server people can reach.**

`python smoke_test.py` runs the end-to-end check (about 250 checks) against a running dev instance. Start that instance with `Auth__LoginsPerMinutePerIp=1000` and a fresh database; to test beside a copy you are using, start a second instance on another port with its own databases (`ConnectionStrings__Default`, `ConnectionStrings__Kpi`) and run `SMOKE_BASE=https://localhost:7101 SMOKE_DB=<path to that events.db> python smoke_test.py`.

## KPI dashboard (merged in)
The KPI Alignment & Execution Dashboard lives inside this app under **/Kpi** (menu: *KPIs*), with the same look, language switch and sign-in as the rest of the app; there are no separate KPI accounts.
- **Roles:** an app role maps to the KPI role with the **same name**. **Admin** has every KPI permission; the KPI roles `Kpi`, `KpiManager`, `KpiEditor`, `KpiOfficer` and `KpiViewer` are created automatically and given to people at **Admin > Access & roles** (the department officer is tied to one department); any other role (HOD, Dean, Staff, Media, Research) is read-only *Viewer* unless a KPI role with that exact name exists (**Customize** on its card, or `/Kpi/Admin/Roles`; with no permissions ticked the KPIs menu is hidden from it).
- **Data:** KPI data is in its own SQLite file, `App_Data/kpi_dashboard.db` (`ConnectionStrings:Kpi`), migrated automatically at start. To keep existing KPI data, copy the old `kpi_dashboard.db` there before the first start.
- **Link to events and visits:** the event and visit forms have an optional **Which KPI does this help?** picker (search by number or word, grouped by domain), shown on the details pages.

## Production configuration
LDAPS and SMTP are set in the app: sign in as an Admin -> **LDAP & Email** page (host, port, SSL, base DN, service account, group names, SMTP host/port/STARTTLS/user/from) with Test buttons.
Saved values override appsettings.json; passwords are encrypted (Data Protection keys in `App_Data/keys`, back it up with the DB; on Windows the keys are also DPAPI-protected).
First-time bootstrap (before anyone can sign in) still uses appsettings.json / environment variables:
- `Auth:Mode` = `Ldap` (never `Dev`); `Ldap:*` (LDAPS 636, read-only service account, groups Events-Staff/HOD/Dean/Media/Admin and Research-InCharge). The KPI roles have no AD group: give them at Admin > Access & roles.
  Put `Ldap__ServiceAccountPassword`, `Smtp__Password`, `Security__HashSecret` in environment variables or a secret store, not in the file.
- `Db:Provider` = `SqlServer` and `ConnectionStrings:Default`.
- `Smtp:Enabled` = true plus host/port/credentials.
- `App:PublicHost` / `App:PublicBaseUrl` / `App:InternalBaseUrl`.
- `App:BootstrapAdmins` = the first Admin's email or username.
- Back up the whole `App_Data` folder (databases, event images, research files, **logo, imported fonts**, encryption keys).

## Sign-in
- People sign in with their **company email address** (the AD `mail` attribute) and their normal password. The short username, `DOMAIN\user` and the UPN also still work.
- Roles, the HOD and Dean, visit approvers and bus coordinators can be given by **email address or username** (Access & roles page and Settings). Existing records keep working because the internal user name is unchanged.
- **Forgot your password?** on the sign-in page. Passwords live in Active Directory and the app never stores or sets them, so the page shows a *Reset my password* button (set the URL in Settings > Sign-in help, for example your self-service reset portal) and an *Ask IT to reset it* form that emails the IT helpdesk address. The answer is always the same, so nobody can find out which addresses exist, and one address can ask once every 10 minutes.

## Setting up and testing the real directory
1. Sign in as Admin and open **Admin > LDAP & Email**.
2. Press **Use standard defaults** (LDAPS 636, verify certificate, nested groups, the usual role group names). If the server name is filled in, the Base DN is worked out from it (`dc1.company.local` -> `DC=company,DC=local`).
3. Enter the server, the read-only service account DN and its password. The password stays in your browser until you press Save or Test; it is stored encrypted.
4. Press **Test LDAP**. It checks the connection and certificate, the service account sign-in, the Base DN, each role group, and **how many users have an email address and whether any two share one** (people sharing an email cannot sign in by email; they can still use their username). Add a test user and password to try a full sign-in and see the roles.
5. **Who gets each role:**
   - Each role box takes **one or several groups** separated by commas ("Group-A, Group-B"); a person gets the role when they are in any of them, including groups inside those groups when nested groups are on.
   - Press **Fetch groups from LDAP** to list the directory's groups. **Members** opens a group: its people with their email addresses and, indented, the groups inside it (as a tree, or as one flat list with where each person comes from). **Copy emails** copies the whole list. A role button on each group adds or removes it from that role.
   - **Preview people per role** shows, for every role, the groups (green = found, with a head count), every person who would get the role and why, and lets you **add a single person** by email address or username (or remove one). People added directly are saved at once and also appear on the Access & roles page; group boxes are saved with **Save**.
   - Pick **what to show** (emails, usernames, tree or flat list); the choice is remembered in your browser.
   - From a group's member list, the *Assign role...* menu next to a person gives that one person a role directly.
The mail box has the same **Use standard defaults** button (port 587, STARTTLS) and a **Send test email** check.

## Other sections
- **External visits (bus requests):** Staff request a bus for a student trip. Who approves, who confirms the bus and who gets the emails is set in Settings > External visits (bus).
- **Research:** Staff upload a research paper or proposal (PDF, DOC, DOCX). It is emailed to the research in-charge and saved in the archive; there is no approval. The in-charge has the **Research** role (AD group `Research-InCharge` by default, or assign it on the Access & roles page). Set the in-charge email in Settings > Research.
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

## Approval flow
Admins decide who approves an event, in which order and how many approvals each step needs at **Admin > Approval flow** (default: HOD, then Dean). See the [user guide](docs/USER-GUIDE.md#approval-flow-who-approves-events).
