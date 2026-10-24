# Setup and deployment guide

The app is one ASP.NET Core 8 project in `src/GuestEvents`. A developer can run it in two minutes with no Active Directory. Production needs LDAPS, SMTP and a reverse proxy.

Related: [USER-GUIDE.md](USER-GUIDE.md) (how people use it), [PRD.md](PRD.md), [SITEMAP.md](SITEMAP.md).

## 1. Requirements

- **.NET 8 SDK** to build, or the **ASP.NET Core 8 runtime** to run a published copy.
- Windows Server or Linux. On Linux, install a font that supports Arabic for the event image (default font: Tahoma) or pick another in Settings > Event image.
- A database: SQLite (default, a file in `App_Data`) or SQL Server.
- For production: an Active Directory reachable over LDAPS, an SMTP relay, TLS certificates, and two host names (internal and public).

## 2. Run it locally (no Active Directory)

```powershell
cd src/GuestEvents
$env:ASPNETCORE_ENVIRONMENT = 'Development'
dotnet run --urls https://localhost:7100
```

Open https://localhost:7100 and sign in. Development mode uses built-in test users (password `dev` for all):

| Role | Email | Username |
|---|---|---|
| Staff | `staff@example.com` | `staff` |
| HOD | `hod@example.com` | `hod` |
| Media | `media@example.com` | `media` |
| Research in-charge | `research@example.com` | `research` |
| Admin (also Staff) | `admin@example.com` | `admin` |

The SQLite database is created in `src/GuestEvents/App_Data/`. If the browser warns about the development certificate, run `dotnet dev-certs https --trust` once.

## 3. Try the real sign-in flow locally (fake directory + mail catcher)

The `testtools` folder has a fake Active Directory and a mail catcher, so you can test LDAP sign-in and emails on your own machine. See [testtools/README.md](../testtools/README.md) for every test user.

```powershell
# 1. fake Active Directory on port 1389 (LDAPS on 1636 with a self-signed certificate)
python testtools\fake_ad.py 1389 --tls testtools\fake_ad_cert.pem testtools\fake_ad_key.pem 1636
# (create the certificate first, see testtools/README.md)

# 2. mail catcher: SMTP 1025, inbox at http://127.0.0.1:8025
testtools\mailpit\mailpit.exe --smtp 127.0.0.1:1025 --listen 127.0.0.1:8025

# 3. the app in LDAP mode
$env:ASPNETCORE_ENVIRONMENT='Development'; $env:Auth__Mode='Ldap'
$env:Ldap__Host='127.0.0.1'; $env:Ldap__Port='1389'; $env:Ldap__UseSsl='false'; $env:Ldap__BaseDn='DC=test,DC=local'
$env:Ldap__ServiceAccountDn='CN=svc-events,OU=Service,DC=test,DC=local'; $env:Ldap__ServiceAccountPassword='svcpass'
$env:Smtp__Enabled='true'; $env:Smtp__Host='127.0.0.1'; $env:Smtp__Port='1025'; $env:Smtp__UseStartTls='false'
cd src/GuestEvents; dotnet run --urls https://localhost:7100
```

Sign in with `admin@test.local` / `admin123` (or `staff@test.local` / `staff123`, `research@test.local` / `research123`, ...).

Automated checks: `python smoke_test.py` (whole app, dev mode) and `python testtools\research_groups_test.py` (81 checks; needs the fake AD on 1390, Mailpit and the app on 7102, see the testtools README).

## 4. Deploy to production

1. **Create the AD groups** (names are configurable, and the LDAP & Email page can list the directory's groups for you): `Events-Staff`, `Events-HOD`, `Events-Media`, `Events-Admin`, `Research-InCharge`. Create a **read-only service account** for LDAP lookups.
2. **Publish** and copy to the server:
   ```powershell
   dotnet publish src/GuestEvents -c Release -o C:\apps\guest-events
   ```
3. **Run it as a service** behind a reverse proxy that terminates TLS.
   - Windows: host it in **IIS** with the ASP.NET Core Hosting Bundle (recommended; IIS terminates TLS and keeps it running), or run `GuestEvents.exe --urls http://127.0.0.1:5000` under a service wrapper such as NSSM. The app has no built-in Windows service mode, so `sc.exe create` on its own will not work.
   - Linux (systemd): a unit with `ExecStart=/usr/bin/dotnet /opt/guest-events/GuestEvents.dll --urls http://127.0.0.1:5000`, `WorkingDirectory=/opt/guest-events` and your environment variables.
4. **Set the first-run configuration** in environment variables (or `appsettings.json`). Keep secrets out of files:

   | Variable | Value |
   |---|---|
   | `Auth__Mode` | `Ldap` |
   | `Ldap__Host`, `Ldap__Port`, `Ldap__BaseDn`, `Ldap__ServiceAccountDn` | your directory, port `636`, for example `DC=company,DC=local` |
   | `Ldap__ServiceAccountPassword`, `Smtp__Password` | the passwords (environment variable or secret store only) |
   | `Security__HashSecret` | a long random value (device hashes and rotating QR) |
   | `Db__Provider`, `ConnectionStrings__Default` | only for SQL Server |
   | `App__InternalBaseUrl` | for example `https://events.company.local` (links in emails) |
   | `App__PublicBaseUrl`, `App__PublicHost` | for example `https://qr.company.com` and `qr.company.com` |
   | `App__BootstrapAdmins` | the email or username of the first Admin(s) |
   | `Smtp__Enabled`, `Smtp__Host`, `Smtp__Port`, `Smtp__From` | your mail relay |

5. **Publish two host names** through the proxy. The internal name stays inside the network; the public name goes to the internet. The public host can reach **only** the QR pages (`/p/...`), static files and the language switch; everything else returns 404 on it.
   Example for nginx (both names go to the same app):
   ```nginx
   server {
     server_name events.company.local qr.company.com;
     location / {
       proxy_pass http://127.0.0.1:5000;
       proxy_set_header Host $host;
       proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
       proxy_set_header X-Forwarded-Proto $scheme;
       client_max_body_size 30m;   # research uploads
     }
   }
   ```
6. **Start the app and sign in as the bootstrap Admin.** The home page shows a setup checklist until everything is done.

## 5. Connect the real directory (in the browser)

Open **Admin > LDAP & Email**:

1. Press **Use standard defaults** (LDAPS 636, verify certificate, nested groups, the usual group names; the Base DN is worked out from the server name).
2. Enter the server, the service account DN and its password.
3. Press **Test LDAP**. It checks the connection and certificate, the service account sign-in, the Base DN, each role group, **how many users have an email address and whether any two share one** (people who share an email cannot sign in by email; they can still use their username). Add a test user and password to try a full sign-in and see the roles.
4. Press **Fetch groups from LDAP**.
   - **Members** shows who is in a group, with email addresses and the groups inside it (tree or flat list, **Copy emails**).
   - A role button adds or removes the group for that role. A role can have **several groups** ("Group-A, Group-B").
5. Press **Preview people per role** to see exactly who gets each role and why. Add a single person by email address or username (no group needed), or remove one.
6. Press **Save**. People added directly are saved at once; group boxes are saved with Save.
7. Do the same for mail: **Use standard defaults** (port 587, STARTTLS), enter the server, **Send test email**, Save.

Then open **Settings** and fill: Recipients (admin mailbox, media officer, HOD), **External visits (bus)** (approvers, bus coordinators, bus email), **Research** (research in-charge email), **Sign-in help** (password reset page and IT helpdesk email).

## Configuration reference

Settings live in two places: first-run values in `appsettings.json` or environment variables, and everything an Admin changes day to day in the Settings page (saved in the database, effective immediately). Values saved in the browser replace `appsettings.json`.

### appsettings.json / environment variables

| Key | Default | Meaning |
|---|---|---|
| `Db:Provider` | `Sqlite` | `Sqlite` or `SqlServer` |
| `ConnectionStrings:Default` | `Data Source=App_Data/events.db` | Database connection |
| `Auth:Mode` | `Ldap` | `Ldap`, or `Dev` for local testing (password `dev`) |
| `Auth:Sso` | `false` | Windows single sign-on |
| `Auth:LoginsPerMinutePerIp` | `20` | Login rate limit |
| `Ldap:Host`, `Port`, `UseSsl`, `BaseDn`, `ServiceAccountDn` | `dc.company.local`, `636`, `true` | Directory connection |
| `Ldap:GroupStaff`, `GroupHod`, `GroupMedia`, `GroupAdmin` | `Events-Staff`, `Events-HOD`, `Events-Media`, `Events-Admin` | AD groups mapped to roles (several allowed, comma separated) |
| `Ldap:GroupResearch` | `Research-InCharge` | AD group for the Research role |
| `Smtp:Enabled`, `Host`, `Port`, `UseStartTls`, `From` | `false`, `smtp.company.local`, `25`, `true` | Outgoing mail |
| `App:InternalBaseUrl` | `https://events.company.local` | Base of links in emails |
| `App:PublicBaseUrl`, `App:PublicHost` | `https://qr.company.com` | Address printed in the QR, and the only host the public may reach |
| `App:HodEmail` | `hod@company.local` | Fallback HOD address |
| `App:BootstrapAdmins` | empty | Emails or usernames made Admin on sign-in |
| `Public:RequestsPerMinutePerIp`, `SubmitsPerMinutePerIp` | `60`, `30` | Public page rate limits |
| `Security:HashSecret` | `CHANGE-ME` | Secret for device hashes and the rotating QR; set a long random value |

### Settings page (Admin > Settings)

| Tab | Keys |
|---|---|
| Recipients | `AdminMailbox`, `AdminExtraTo`, `AdminCc`, `MediaOfficerEmail`, `HodUsername`, `HodEmail` |
| External visits (bus) | `VisitApprovalRequired` (Yes), `VisitApprovers`, `VisitApproverEmail`, `VisitBusUsers`, `VisitBusEmail`, `VisitBusCc`, `VisitNotifyRequester` (Yes), `VisitMinDaysAhead` (0) |
| Research | `ResearchEmail`, `ResearchCc`, `ResearchNotifySubmitter` (Yes), `ResearchMaxMb` (20, range 1 to 25) |
| Sign-in help | `PasswordResetUrl` (http or https address; empty = only the Ask IT form), `PasswordHelpEmail` (empty = admin mailbox) |
| Approval and check-in | `ReminderWorkingDays` (2), `CheckInOpenMinutesBefore` (30), `CheckInCloseMinutesAfterEnd` (0), `EvaluationOpenDays` (7), `RequireEmail`, `RequirePhone` |
| QR and public page | `RotatingQr`, `BlockOutsideVenue`, `VenueNetworkPrefixes` |
| Event image | `ImgTitleEn`, `ImgTitleAr`, `ImgPrimary`, `ImgAccent`, `ImgFont` |
| Evaluation | `Questions` (four optional labels) |
| Email templates | subject and body for media, contact, admin and requester |
| Display | `UseArabicDigits`, `DateFormat` |

Visit approvers, bus coordinators, the HOD and in-app role assignments accept an **email address or a username**. An empty approver list means anyone with the HOD role may approve visits; Admin can always approve and confirm.

## 6. Operate

- **Back up** the database and the whole `App_Data` folder together, daily: it holds the event images, logos, research uploads and the encryption keys in `App_Data/keys`. Without the keys, saved LDAP and SMTP passwords cannot be read.
- **Upgrade:** stop the service, replace the files, start it. New tables are created on start (no manual migration).
- **Logs:** application logs go to the console or the service log. **Admin > Audit log** shows business events; **Admin > Notifications** shows every email and whether it was sent.
- **Time:** a background job runs every 10 minutes (reminders, status roll-over). The working week is Sunday to Thursday.
- **Secrets:** rotate the LDAP service account password by entering it on the LDAP & Email page (leave blank to keep the saved one).

## 7. Troubleshooting

| Symptom | What to check |
|---|---|
| Cannot sign in | Use the company **email**; the account must be enabled in AD and have a role (AD group or **Admin > Roles**). If two accounts share the email, sign in with the username. Test it on LDAP & Email with a test user. |
| "Cannot reach the server, or the TLS handshake failed" | Server name, port and Encryption setting; with port 3268 or 3269 the "whole forest" box is ticked; for a self-signed certificate use "Trust this certificate only" and paste the thumbprint shown by Test LDAP. |
| "The service account was rejected" | Service account DN and password on LDAP & Email. |
| A role box is red | The group name is not found under the Base DN; use **Fetch groups** to pick the exact name. |
| Test LDAP says "No user accounts were found" | Base DN, and the service account's read rights. |
| Emails not arriving | **Admin > Notifications** shows each send and its error; check the SMTP settings, press **Send test email**, and make sure Sending is On. |
| Research upload says "Not sent" | The file was saved but the email failed: see Notifications; download it from the archive and fix the SMTP settings. |
| Upload rejected | Only PDF, DOC and DOCX up to the size limit; the file content must match its extension; the proxy must allow bodies over the limit (`client_max_body_size`). |
| Event image has boxes instead of Arabic letters | Install a font with Arabic support on the server and set it in Settings > Event image. |
| Public QR page not reachable from the internet | `App__PublicHost` must match the public host name and the proxy must forward it; only `/p/...` is served there. |
| Browser shows a certificate warning locally | `dotnet dev-certs https --trust`. |
