# Guest Workshop & Event Management (ASP.NET Core 8, one app)

One ASP.NET Core MVC app serves both the internal site (AD/LDAP login) and the public QR pages (`/p/{token}`).

Licensed under the [MIT License](LICENSE).

## Documentation
- [Product requirements (PRD)](docs/PRD.md) - goals, roles, requirements, workflows, architecture, data model, security
- [Setup and deployment guide](docs/SETUP.md) - run it locally, connect the real directory, deploy, configuration reference, troubleshooting
- [User guide](docs/USER-GUIDE.md) - how each role uses it (Staff, HOD, Media, Research in-charge, Admin, attendees)
- [Sitemap](docs/SITEMAP.md) - every page and route, who sees what
- [Test tools](testtools/README.md) - fake directory, mail catcher and the automated checks

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

## Sign-in
- People sign in with their **company email address** (the AD `mail` attribute) and their normal password. The short username, `DOMAIN\user` and the UPN also still work.
- Roles, the HOD, visit approvers and bus coordinators can be given by **email address or username** (Roles page and Settings). Existing records keep working because the internal user name is unchanged.
- **Forgot your password?** on the sign-in page. Passwords live in Active Directory and the app never stores or sets them, so the page shows a *Reset my password* button (set the URL in Settings > Sign-in help, for example your self-service reset portal) and an *Ask IT to reset it* form that emails the IT helpdesk address. The answer is always the same, so nobody can find out which addresses exist, and one address can ask once every 10 minutes.

## Setting up and testing the real directory
1. Sign in as Admin and open **Admin > LDAP & Email**.
2. Press **Use standard defaults** (LDAPS 636, verify certificate, nested groups, the usual role group names). If the server name is filled in, the Base DN is worked out from it (`dc1.company.local` -> `DC=company,DC=local`).
3. Enter the server, the read-only service account DN and its password. The password stays in your browser until you press Save or Test; it is stored encrypted.
4. Press **Test LDAP**. It checks the connection and certificate, the service account sign-in, the Base DN, each role group, and **how many users have an email address and whether any two share one** (people sharing an email cannot sign in by email; they can still use their username). Add a test user and password to try a full sign-in and see the roles.
5. **Who gets each role:**
   - Each role box takes **one or several groups** separated by commas ("Group-A, Group-B"); a person gets the role when they are in any of them, including groups inside those groups when nested groups are on.
   - Press **Fetch groups from LDAP** to list the directory's groups. **Members** opens a group: its people with their email addresses and, indented, the groups inside it (as a tree, or as one flat list with where each person comes from). **Copy emails** copies the whole list. A role button on each group adds or removes it from that role.
   - **Preview people per role** shows, for every role, the groups (green = found, with a head count), every person who would get the role and why, and lets you **add a single person** by email address or username (or remove one). People added directly are saved at once and also appear on the Roles page; group boxes are saved with **Save**.
   - Pick **what to show** (emails, usernames, tree or flat list); the choice is remembered in your browser.
   - From a group's member list, the *Assign role...* menu next to a person gives that one person a role directly.
The mail box has the same **Use standard defaults** button (port 587, STARTTLS) and a **Send test email** check.

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
