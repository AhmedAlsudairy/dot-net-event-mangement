# Setup and deployment guide

This guide takes you from nothing to a working system. Pick the path that fits, then follow the numbered steps. Each step says **what you should see**, so you know it worked.

Related: [USER-GUIDE.md](USER-GUIDE.md) (how people use it), [PRD.md](PRD.md) (what it does), [SITEMAP.md](SITEMAP.md) (every page).

## Contents

1. [Choose your path](#1-choose-your-path)
2. [Before you start](#2-before-you-start)
3. [Install the tools](#3-install-the-tools)
4. [Get the code](#4-get-the-code)
5. [Path A: demo on your computer (10 minutes)](#5-path-a-demo-on-your-computer-10-minutes)
6. [Path B: real sign-in on your computer with a fake directory](#6-path-b-real-sign-in-on-your-computer-with-a-fake-directory)
7. [Path C: production](#7-path-c-production)
8. [Database options](#8-database-options)
9. [Configuration reference](#configuration-reference)
10. [Operate: backup, upgrade, logs](#10-operate-backup-upgrade-logs)
11. [Troubleshooting](#11-troubleshooting)
12. [Glossary](#12-glossary)

---

## 1. Choose your path

| Path | Use it to | Needs | Time |
|---|---|---|---|
| **A. Demo** | See every screen, click through all roles | .NET 8 SDK only. Built-in test users, no Active Directory, no email | 10 min |
| **B. Real sign-in, locally** | Test LDAP sign-in, groups and emails safely on your own machine | A + Python 3 + the `testtools` folder (fake directory and mail catcher) | 30 min |
| **C. Production** | Run it for real staff | Active Directory with LDAPS, a mail relay, a server, TLS certificates, two host names | half a day |

Do **A** first even if you are going to production. It proves the code runs on your machine.

> [!NOTE]
> **Platform.** The app is developed and tested on **Windows**. It is plain ASP.NET Core 8, so Linux hosting is possible, but it is **not tested**: the event-image feature uses SkiaSharp, and the project does not yet reference the SkiaSharp Linux native package, so on Linux you would need to add `SkiaSharp.NativeAssets.Linux` (and install fontconfig and an Arabic-capable font) before approving events works. Use Windows Server unless you are ready to do that.

---

## 2. Before you start

### Information to collect (production only)

Gather this first. Ask your IT team for the directory items.

| Item | Example | Where it is used |
|---|---|---|
| Domain controller name(s) | `dc1.company.local` | LDAP & Email > Server(s) |
| Base DN | `DC=company,DC=local` | where users and groups are searched |
| Service account DN and password | `CN=svc-events,OU=Service,DC=company,DC=local` | read-only account the app uses to look people up |
| The company email for each person | `name@company.com` (the AD `mail` attribute) | people sign in with it |
| AD groups for the roles (or the people for each role) | `Events-Staff`, `Events-HOD`, `Events-Media`, `Events-Admin`, `Research-InCharge` | who gets which role |
| SMTP relay | `smtp.company.local`, port 25 or 587 | all emails |
| Two host names | internal `events.company.local`, public `qr.company.com` | internal site, public QR page |
| TLS certificates for both names | from your company CA | HTTPS |
| Who is the first Admin | their company email | `App__BootstrapAdmins` |
| Research in-charge, IT helpdesk, admin mailbox, media officer, HOD | email addresses | Settings tabs |

### Network ports

| From | To | Port | Why |
|---|---|---|---|
| App server | Domain controller | **636** (LDAPS). 389 with StartTLS, 3269 or 3268 for a global catalog | sign-in and groups |
| App server | Mail relay | 25 or 587 | sending email |
| Staff computers | App server (via proxy or IIS) | 443 | the internal site |
| The internet | Public host name only | 443 | guests scanning the QR code |

### What the service account needs

A normal **domain user** with no special rights. Reading users and groups is allowed for any authenticated user in a default Active Directory. It never needs to change anything, and the app never stores your users' passwords.

---

## 3. Install the tools

On a Windows machine, open **PowerShell** and run (skip what you already have):

```powershell
winget install Microsoft.DotNet.SDK.8      # to build and run (production servers only need the runtime / Hosting Bundle)
winget install Git.Git                     # to download the code
winget install Python.Python.3.12          # only for Path B (test tools) and the automated checks
```

Close and reopen PowerShell, then check:

```powershell
dotnet --version     # you should see 8.0.x
git --version
python --version     # Path B only
```

---

## 4. Get the code

```powershell
git clone https://github.com/AhmedAlsudairy/dot-net-event-mangement.git
cd dot-net-event-mangement
```

What is where:

| Folder or file | What it is |
|---|---|
| `src/GuestEvents` | the application (ASP.NET Core 8 MVC). Everything you run |
| `docs` | this guide, the PRD, the user guide, the sitemap |
| `testtools` | fake Active Directory, mail catcher notes, automated checks |
| `smoke_test.py` | quick end-to-end check of the whole app in demo mode |
| `src/GuestEvents/appsettings.json` | first-run settings (placeholders only, no secrets) |
| `src/GuestEvents/App_Data` | created on first run: database, event images, research files, encryption keys. **Never commit or share it** |

---

## 5. Path A: demo on your computer (10 minutes)

### Step A1. Start the app

```powershell
cd src\GuestEvents
$env:ASPNETCORE_ENVIRONMENT = 'Development'
dotnet run --urls https://localhost:7100
```

**You should see** `Now listening on: https://localhost:7100` and `Application started`. The first start downloads packages and creates `App_Data\events.db`.

If the browser warns about the certificate, run this once and restart the browser: `dotnet dev-certs https --trust`

> Without `--urls`, `dotnet run` listens on **https://localhost:7063** (the project's default profile). The rest of the docs use 7100.

### Step A2. Sign in

Open https://localhost:7100. Demo mode (`Auth:Mode=Dev`) has built-in users. The password for all of them is `dev`.

| Role | Email | Username |
|---|---|---|
| Admin (also Staff) | `admin@example.com` | `admin` |
| Staff | `staff@example.com` | `staff` |
| HOD | `hod@example.com` | `hod` |
| Media | `media@example.com` | `media` |
| Research in-charge | `research@example.com` | `research` |

**You should see** the **My events** page. As Admin, a **setup checklist** also appears on the home page.

### Step A3. Take the 10-minute tour

1. **As `staff`:** press **New request > New event request**, fill the form (any values), press **Submit for approval**.
2. **As `hod`** (use a private window or sign out first): open **Pending approvals**, press **Approve**. **You should see** the event become *Approved*.
3. **As `staff` again:** open the event. **You should see** a generated event image, **Download QR**, a printable **poster** and the **Food request** button.
4. **As `staff`:** press **New request > External visit (bus)**, tomorrow's date, submit. As `hod`, open **External visits** and approve. As `admin`, open the visit, type bus details and press **Confirm**.
5. **As `staff`:** **New request > Upload research**, choose any small PDF, press **Upload and send**. As `research`, open **Research archive**. **You should see** the upload and a download button.
6. **As `admin`:** open **Admin > Notifications**. Email is switched off in demo mode, so each email is logged but not sent. Open **Admin > LDAP & Email** and **Admin > Settings** to look around.

> To start again from scratch, stop the app and delete `src\GuestEvents\App_Data\events.db`.

### Step A4. (Optional) Run the automated check

With the app running in demo mode, in another PowerShell window from the repo folder:

```powershell
python smoke_test.py
```

**You should see** a list of `PASS` lines and no `FAIL`.

---

## 6. Path B: real sign-in on your computer with a fake directory

This is the safest way to learn LDAP sign-in, groups, the group browser and emails before touching your real directory. Everything runs locally.

You need three things running. Use **three PowerShell windows**, all in the repo folder.

### Step B1. Fake Active Directory

Create a self-signed certificate once (needs OpenSSL, which comes with Git for Windows: `C:\Program Files\Git\usr\bin\openssl.exe`):

```powershell
openssl req -x509 -newkey rsa:2048 -nodes -keyout testtools\fake_ad_key.pem -out testtools\fake_ad_cert.pem -days 30 -subj "/CN=fake-ad.test.local"
```

Start it (window 1):

```powershell
cd testtools
python fake_ad.py 1389 --tls fake_ad_cert.pem fake_ad_key.pem 1636
```

**You should see** `Fake AD LDAPS on 127.0.0.1:1636 (self-signed certificate)` and `Fake AD on 127.0.0.1:1389 (plain LDAP, TEST ONLY)`; it then prints one line for every sign-in or search it answers. It is a fake directory with about 20 test accounts, nested groups, a circular group, a locked account, a disabled account and two accounts that share one email. The full list is in [testtools/README.md](../testtools/README.md).

### Step B2. Mail catcher

Download **Mailpit** (a free local mail inbox) from https://github.com/axllent/mailpit/releases and put `mailpit.exe` in `testtools\mailpit\`. Start it (window 2):

```powershell
testtools\mailpit\mailpit.exe --smtp 127.0.0.1:1025 --listen 127.0.0.1:8025
```

Open http://127.0.0.1:8025. **You should see** an empty inbox. Every email the app sends will appear here, and nothing leaves your computer.

### Step B3. The app in LDAP mode

Window 3:

```powershell
cd src\GuestEvents
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:Auth__Mode = 'Ldap'
$env:Ldap__Host = '127.0.0.1'; $env:Ldap__Port = '1389'; $env:Ldap__UseSsl = 'false'
$env:Ldap__BaseDn = 'DC=test,DC=local'
$env:Ldap__ServiceAccountDn = 'CN=svc-events,OU=Service,DC=test,DC=local'
$env:Ldap__ServiceAccountPassword = 'svcpass'
$env:Smtp__Enabled = 'true'; $env:Smtp__Host = '127.0.0.1'; $env:Smtp__Port = '1025'; $env:Smtp__UseStartTls = 'false'; $env:Smtp__From = 'events@test.local'
$env:ConnectionStrings__Default = 'Data Source=App_Data/ldap-test.db'     # keeps your demo data separate
dotnet run --urls https://localhost:7100
```

### Step B4. Sign in through the fake directory

Open https://localhost:7100 and sign in with an **email**:

| Email | Password | Role it gets |
|---|---|---|
| `admin@test.local` | `admin123` | Admin and Staff |
| `staff@test.local` | `staff123` | Staff |
| `hod@test.local` | `hod123` | HOD |
| `research@test.local` | `research123` | Research in-charge |

**You should see** the **My events** page, with your name from the directory in the top right.
Try `nogroup@test.local` / `nogroup123`: you should see "your password is correct but you have no role yet". Fix it as Admin: **Admin > Roles**, add `nogroup@test.local` as Staff, then sign in again.

### Step B5. Explore the directory tools (as `admin@test.local`)

Open **Admin > LDAP & Email**. The values from step B3 are already in the form.

1. Press **Test LDAP**. **You should see** a green result: connected, groups found, `Users: 22`, and `Duplicate emails ... shared@test.local`.
2. Type `looper@test.local` and password `looper123` in the test boxes and press **Test LDAP** again. **You should see** the person's groups, including the group inside a group, and the roles they would get.
3. Press **Fetch groups from LDAP**. **You should see** 11 groups.
4. Press **Members** on `Events-Staff`. **You should see** its people with emails, and `Marketing-All` indented inside it with `nested@test.local`. Try `Deep-1` (three levels) and `Loop-A` (a circular group, marked "already shown above").
5. Press **Preview people per role**. **You should see** each role with the groups, every person and where they come from. Add `someone@test.local` to HOD, then remove them.
6. Under **Email**, press **Use standard defaults**, then set Server `127.0.0.1`, Port `1025`, STARTTLS **Off**, press **Send test email**. **You should see** the message in the Mailpit inbox.

### Step B6. (Optional) Run the 81 automated checks

```powershell
# fake AD on 1390, app on 7102 with its own database, Mailpit already running
python testtools\research_groups_test.py
```

The exact commands are in [testtools/README.md](../testtools/README.md). Restart the test app before each run. **You should see** `FAILED: 0`.

---

## 7. Path C: production

Follow these steps in order. Do not skip the checks.

### Step C1. Prepare Active Directory

1. **Service account.** Create a normal domain user, for example `svc-events`, with a long random password that does not expire (or a rotation plan). No special rights.
2. **Groups** (optional but recommended). Create the groups you want, for example `Events-Staff`, `Events-HOD`, `Events-Media`, `Events-Admin`, `Research-InCharge`, and add people. If you prefer not to create groups, you can give roles to individual people later in **Admin > Roles**. You can use any existing groups: the app lets you pick them from a list.
3. **Emails.** Check people have the `mail` attribute filled in. People sign in with it; people without one can still use their short username.
4. **LDAPS.** Make sure the domain controller answers on port 636 with a certificate the app server trusts (issued by your company CA). If it uses a self-signed certificate, you can pin its thumbprint later in the app (step C9).

### Step C2. Prepare the server

1. Windows Server 2019 or later, with the internal and public DNS names pointing at it (or at your reverse proxy).
2. Open the firewall ports from the table in section 2.
3. Create the folder `C:\apps\guest-events`.
4. Install the **ASP.NET Core 8 Hosting Bundle** (it includes the runtime and the IIS module) from https://dotnet.microsoft.com/download/dotnet/8.0 . Then run `iisreset`.

### Step C3. Publish the application

On your build machine (the one with the SDK), from the repo folder:

```powershell
dotnet publish src\GuestEvents -c Release -o publish
```

Copy the whole `publish` folder to `C:\apps\guest-events` on the server.

**You should see** `GuestEvents.dll`, `web.config`, `appsettings.json` and a `wwwroot` folder in `C:\apps\guest-events`.

### Step C4. Set the configuration (environment variables)

These are first-run settings. Passwords belong here, **never in a file**. Everything else is changed later in the browser.

Run this once in an **elevated** PowerShell on the server (replace the values):

```powershell
$v = @{
  'Auth__Mode'                   = 'Ldap'
  'Ldap__Host'                   = 'dc1.company.local'
  'Ldap__Port'                   = '636'
  'Ldap__BaseDn'                 = 'DC=company,DC=local'
  'Ldap__ServiceAccountDn'       = 'CN=svc-events,OU=Service,DC=company,DC=local'
  'Ldap__ServiceAccountPassword' = 'THE-SERVICE-ACCOUNT-PASSWORD'
  'Smtp__Enabled'                = 'true'
  'Smtp__Host'                   = 'smtp.company.local'
  'Smtp__Port'                   = '25'
  'Smtp__From'                   = 'events@company.com'
  'Security__HashSecret'         = 'PASTE-A-LONG-RANDOM-VALUE'
  'App__InternalBaseUrl'         = 'https://events.company.local'
  'App__PublicBaseUrl'           = 'https://qr.company.com'
  'App__PublicHost'              = 'qr.company.com'
  'App__BootstrapAdmins'         = 'first.admin@company.com'
}
$v.GetEnumerator() | ForEach-Object { [Environment]::SetEnvironmentVariable($_.Key, $_.Value, 'Machine') }
iisreset
```

Make the random secret with: `[Convert]::ToBase64String((1..48 | ForEach-Object { Get-Random -Max 256 }))`

What each one does:

| Variable | Meaning |
|---|---|
| `Auth__Mode` | `Ldap` for real sign-in. (`Dev` is demo mode: never use it in production) |
| `Ldap__*` | how to reach the directory. Port 636 uses LDAPS. Use `Ldap__UseSsl=false` and `Ldap__StartTls=true` for StartTLS on 389 |
| `Smtp__*` | the mail relay. Add `Smtp__User` and `Smtp__Password` if it needs a login. `Smtp__UseStartTls=true` for port 587 |
| `Security__HashSecret` | a long random value used to hash devices (anonymous check-in and evaluation) and to sign the rotating QR. Changing it later invalidates old QR links |
| `App__InternalBaseUrl` | the address used in links inside emails |
| `App__PublicBaseUrl` / `App__PublicHost` | the address printed in the QR code, and the only host name the public may use |
| `App__BootstrapAdmins` | people who get **Admin** when they sign in, so the first Admin exists before any roles are set. Comma separated emails or usernames |

> If your directory is not reachable yet, you can leave the `Ldap__*` values empty and finish them in the browser (step C9), but then nobody can sign in until they are set. The bootstrap admin still has to authenticate against the directory.

### Step C5. Host it in IIS

1. Open **IIS Manager**. **Application Pools > Add Application Pool**: name `GuestEvents`, **.NET CLR version: No Managed Code**.
2. **Sites > Add Website**: name `GuestEvents`, application pool `GuestEvents`, physical path `C:\apps\guest-events`.
3. **Bindings**: add two **https** bindings on port 443, one with host name `events.company.local` and one with `qr.company.com`, choosing the matching certificate for each.
4. **Give the app permission to write** its data folder (the database, images, research files and keys are all in `App_Data`):
   ```powershell
   New-Item -ItemType Directory -Force C:\apps\guest-events\App_Data | Out-Null
   icacls C:\apps\guest-events\App_Data /grant "IIS AppPool\GuestEvents:(OI)(CI)M"
   ```
5. Restart the site.

**You should see** the sign-in page at https://events.company.local .

> Alternative to IIS: run `GuestEvents.exe --urls http://127.0.0.1:5000` under a service wrapper such as NSSM and put a reverse proxy (nginx, or IIS as a proxy) in front of it. The app has no built-in Windows service mode, so `sc.exe create` on its own will not work. Behind any proxy, forward `Host`, `X-Forwarded-For` and `X-Forwarded-Proto` (the app trusts them), and allow request bodies of at least 30 MB (research uploads).

### Step C6. Publish the two host names safely

The app is **one program with two faces**. Which face a visitor gets depends on the host name:

- `events.company.local` (internal): the full application with sign-in. **Keep this inside your network.**
- `qr.company.com` (public): reaches **only** `/p/...` (the QR check-in and evaluation pages), static files and the language switch. Everything else returns 404 on this name.

Publish **only the public name** to the internet (firewall or reverse proxy rule). If the internal name were reachable from the internet, the sign-in page would be too.

**Check:** from a phone on mobile data, open `https://qr.company.com/` (you should get a 404) and, later, a real QR link (you should get the check-in page).

### Step C7. First start and first Admin

1. Open https://events.company.local and sign in with the email in `App__BootstrapAdmins` and that person's company password.
2. **You should see** the **My events** page with a **setup checklist** at the top.
3. If you see "your password is correct but you have no role", the bootstrap value does not match: use the email exactly as it is in Active Directory (or the short username).

### Step C8. Create the other roles

Roles come from AD groups, from people you add directly, or both. Do this in **Admin > LDAP & Email** (step C9), or add people one by one in **Admin > Roles** (type an email, choose a role, press **Add role**). A role applies from the person's next sign-in.

### Step C9. Connect and check the directory in the browser

Open **Admin > LDAP & Email**.

1. Press **Use standard defaults** (LDAPS 636, verify certificate, nested groups, default group names; it fills the Base DN from the server name if empty).
2. Check the server, Base DN, service account DN and password (leave the password empty to keep the saved one).
3. Press **Test LDAP**. **You should see** a green line: connected, signed in as the service account, each group `found`, and `Users: N, M with an email address`. Read the whole line:
   - *"NOT FOUND"* after a group: the group name is wrong. Use **Fetch groups** to pick the exact name.
   - *"Duplicate emails ..."*: those people cannot sign in by email. Fix them in AD, or they use their username.
   - A message with a **certificate thumbprint**: the server's certificate is not trusted. Install your company CA on the server, or choose *Trust this certificate only (pin thumbprint)*, paste the thumbprint, and test again.
4. Add a real test user and password and press **Test LDAP**. **You should see** their groups and roles.
5. Press **Fetch groups from LDAP**, then for each role press the role buttons next to the right groups. You can put **several groups on one role**. Use **Members** to check who is inside a group (including groups inside groups) and **Preview people per role** to see exactly who gets each role.
6. Add single people to a role with the **Add** box in the preview if needed.
7. Press **Save**.

### Step C10. Connect mail

In the **Email** box on the same page:

1. Press **Use standard defaults** (port 587, STARTTLS), then set **Server**, **Port**, **STARTTLS**, **Username/Password** (if the relay needs a login) and **From address** (use a real mailbox name such as `events@company.com`).
2. Make sure **Sending** is **On**.
3. Enter your own address under *Send a test email* and press **Send test email**. **You should see** a green result and the email in your inbox.
4. Press **Save**.

Microsoft 365 notes: server `smtp.office365.com`, port 587, STARTTLS on, a licensed mailbox as the username, and *Authenticated SMTP* enabled for that mailbox. If your tenant has disabled basic SMTP authentication, use an internal relay instead (port 25, no login, allow the app server's IP).

### Step C11. Fill in Settings

Open **Admin > Settings**. The home-page checklist shows what is still unset.

| Tab | Set this | Why |
|---|---|---|
| Recipients | Admin mailbox, Media officer email, HOD username/email | receive approval notices, event images, reminders |
| External visits (bus) | Approval required? approvers, approver email, bus coordinators, bus email, minimum notice | who decides and who arranges the bus |
| Research | Research in-charge email, maximum file size | where uploads are emailed |
| Sign-in help | Password reset page URL, IT helpdesk email | the *Forgot password* page |
| Approval and check-in | reminder days, check-in windows, require email/phone | event behaviour |
| QR and public page | rotating QR, venue network prefixes | protect check-in |
| Event image, Evaluation, Email templates, Display | titles, colours, fonts, wording, digits, date format | look and wording |

Press **Save** (all tabs save together).

### Step C12. Go-live checklist

Tick each one before announcing the system:

- [ ] An ordinary Staff member can sign in with their **email** and company password.
- [ ] A person with no role sees the "no role yet" message (and is fixed in **Admin > Roles**).
- [ ] **Test LDAP** is green and every role group is `found`.
- [ ] **Send test email** arrives, and **Admin > Notifications** shows `Success`.
- [ ] Staff submits an event, the HOD approves: the **image and QR** are created and the **four emails** arrive (media officer, company contact, admin, requester).
- [ ] The QR link opens on a phone **on mobile data** through the public name, check-in works, and evaluation works.
- [ ] The internal name is **not** reachable from the internet; the public name returns 404 for everything except `/p/...`.
- [ ] A bus request goes to the approver, then to the bus recipients; the requester gets the bus details.
- [ ] A research upload arrives at the research in-charge with the file attached and appears in the **Research archive**.
- [ ] **Forgot your password?** works (reset button and/or the Ask IT email).
- [ ] A backup of `App_Data` and the database has been taken and a restore has been tried (section 10).
- [ ] Arabic and English both look right (button at the top).

---

## 8. Database options

**SQLite (default).** Nothing to install. The database is the file `App_Data\events.db`. Good for one school or department. Back up the file together with `App_Data`.

**SQL Server.** Use it if your company standard requires a database server.

1. Ask your DBA for an **empty database** (for example `GuestEvents`) and a login with `db_owner` on it.
2. Set these variables (step C4):
   ```
   Db__Provider = SqlServer
   ConnectionStrings__Default = Server=sql01.company.local;Database=GuestEvents;User Id=guestevents;Password=...;TrustServerCertificate=True
   ```
   (Use `Integrated Security=True` instead of a user and password if the app pool identity has a login.)
3. Restart the site. The app creates its tables on first start; later upgrades add new tables by themselves. **You should see** the tables `Events`, `Visits`, `ResearchDocs`, `Settings` and others in the database.

You cannot move data automatically from SQLite to SQL Server. Decide before you go live.

---

## Configuration reference

Settings live in two places: **first-run values** in environment variables (or `appsettings.json`), and **everything an Admin changes day to day** in the browser (saved in the database, effective immediately). Values saved in the browser (for example on the LDAP & Email page) replace the first-run values.

### Environment variables and appsettings.json

Environment variables use a double underscore for the colon: `Ldap:Host` is `Ldap__Host`.

| Key | Default | Meaning |
|---|---|---|
| `Db:Provider` | `Sqlite` | `Sqlite` or `SqlServer` |
| `ConnectionStrings:Default` | `Data Source=App_Data/events.db` | Database connection |
| `Auth:Mode` | `Ldap` | `Ldap`, or `Dev` for demo mode (built-in users, password `dev`) |
| `Auth:Sso` | `false` | Windows single sign-on (Kerberos/NTLM) when the server is joined to the domain |
| `Auth:LoginsPerMinutePerIp` | `20` | Login rate limit |
| `Ldap:Host`, `Port`, `UseSsl`, `BaseDn`, `ServiceAccountDn` | `dc.company.local`, `636`, `true` | Directory connection |
| `Ldap:ServiceAccountPassword` | placeholder | Service account password: set it as an environment variable |
| `Ldap:GroupStaff`, `GroupHod`, `GroupMedia`, `GroupAdmin` | `Events-Staff`, `Events-HOD`, `Events-Media`, `Events-Admin` | AD groups mapped to roles (several allowed, comma separated) |
| `Ldap:GroupResearch` | `Research-InCharge` | AD group for the Research in-charge role |
| `Smtp:Enabled`, `Host`, `Port`, `UseStartTls`, `From` | `false`, `smtp.company.local`, `25`, `true`, `events@company.local` | Outgoing mail |
| `Smtp:User`, `Smtp:Password` | empty | Mail login, if the relay needs one |
| `App:InternalBaseUrl` | `https://events.company.local` | Base of links in emails |
| `App:PublicBaseUrl`, `App:PublicHost` | `https://qr.company.com`, `qr.company.com` | Address printed in the QR and the only host the public may reach |
| `App:HodEmail` | `hod@company.local` | Fallback HOD address |
| `App:BootstrapAdmins` | empty | Emails or usernames made Admin when they sign in |
| `Public:RequestsPerMinutePerIp`, `SubmitsPerMinutePerIp` | `60`, `30` | Public page rate limits |
| `Security:HashSecret` | `CHANGE-ME-LONG-RANDOM-VALUE` | Secret for device hashes and the rotating QR. **Set a long random value in production** |

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

---

## 10. Operate: backup, upgrade, logs

### Back up (daily)

Back up the **database** and the whole **`App_Data`** folder together. `App_Data` holds event images, logos, research uploads and the **encryption keys** (`App_Data\keys`). Without the keys, saved LDAP and mail passwords cannot be read and must be typed again.

Example (SQLite) for a scheduled task:

```powershell
Import-Module WebAdministration
$stamp = Get-Date -Format yyyyMMdd
Stop-WebSite GuestEvents                      # a short stop gives a consistent copy of the database
Compress-Archive -Path C:\apps\guest-events\App_Data\* -DestinationPath "D:\backups\guest-events-$stamp.zip" -Force
Start-WebSite GuestEvents
```

For SQL Server, back up the database with your normal SQL backups and still copy `App_Data`.

### Restore

Stop the site, put the files back in `App_Data` (and restore the database), start the site. **You should see** your events and settings as they were.

### Upgrade

1. Take a backup.
2. Stop the site (`Import-Module WebAdministration; Stop-WebSite GuestEvents`, or use IIS Manager).
3. Publish the new version (step C3) and copy it over `C:\apps\guest-events`, but **do not delete `App_Data`**.
4. Start the site. New tables are created on start; no manual migration.
5. Run the go-live checklist quickly. To roll back, stop the site and put the previous files and the backup back.

### Logs and health

- **Admin > Audit log:** who submitted, approved, uploaded, downloaded, changed roles or settings.
- **Admin > Notifications:** every email, with success or the error text.
- Application logs go to the console, or to IIS stdout logs if you enable them in `web.config` (`stdoutLogEnabled`).
- A background job runs every 10 minutes (reminders, status roll-over). The working week is Sunday to Thursday.

### Rotate a password

Change the service account password in AD, then enter it on **Admin > LDAP & Email** and press **Save**. Leave a password box empty to keep the saved one.

---

## 11. Troubleshooting

| Symptom | Likely cause and fix |
|---|---|
| `dotnet` is not recognised | Close and reopen PowerShell after installing the SDK; check `dotnet --version` |
| Browser says the certificate is not trusted (local) | `dotnet dev-certs https --trust`, restart the browser |
| App starts but the page is blank or 500 on IIS | Install the **Hosting Bundle** and run `iisreset`; the app pool must be *No Managed Code*; enable `stdoutLogEnabled` in `web.config` and read the log |
| 500 error or "unable to open database file" | The app pool identity cannot write to `App_Data`: re-run the `icacls` command in step C5 |
| Cannot sign in | Use the company **email**. The account must be enabled in AD and have a role (AD group or **Admin > Roles**). If two accounts share the email, sign in with the username. Test on LDAP & Email with a test user |
| "Your password is correct but you have no role" | Add the person in **Admin > Roles**, or add them to one of the role groups, then sign in again |
| "Cannot reach the server, or the secure (TLS) handshake failed" | Check server name, port and Encryption. Port 3268/3269 means the "whole forest" box is ticked. For a self-signed certificate use *Trust this certificate only* and the thumbprint shown by Test LDAP |
| "The service account was rejected" | Service account DN or password is wrong (LDAP & Email) |
| A role box is red | The group name is not found under the Base DN. Press **Fetch groups** and pick the exact name |
| Test LDAP: "No user accounts were found" | Base DN is wrong, or the service account cannot read that part of the directory |
| Test LDAP: "Duplicate emails" | Those people cannot sign in by email (they can use their username); fix the duplicate in AD |
| Emails not arriving | **Admin > Notifications** shows each send and its error. Check the SMTP settings, make sure **Sending** is On, press **Send test email**, and check the mail relay allows the app server |
| Research upload says "Not sent" | The file was saved but the email failed: see Notifications. The in-charge can still download it from the archive |
| Upload rejected | Only PDF, DOC and DOCX; the content must match the extension; within the size limit; the proxy must allow bodies over the limit (`client_max_body_size 30m` in nginx, `maxAllowedContentLength` in IIS) |
| Event image shows boxes instead of Arabic letters | The server has no Arabic-capable font: install one and set it in **Settings > Event image > Font** |
| QR page not reachable from the internet | `App__PublicHost` must equal the public name and the name must reach the app; only `/p/...` is served there |
| Everyone is logged out suddenly | Sessions last 30 minutes of inactivity; if it happens all the time, the encryption keys in `App_Data\keys` are not persisted (check permissions) |
| Saved LDAP/mail passwords stopped working after a move | The `App_Data\keys` folder was not copied with the database; re-enter the passwords |

---

## 12. Glossary

| Term | Meaning |
|---|---|
| **Active Directory (AD)** | Your company's user directory; keeps users, passwords and groups |
| **LDAP / LDAPS** | The protocol the app uses to look people up. LDAPS is the encrypted version (port 636) |
| **Base DN** | The place in the directory where users and groups are searched, for example `DC=company,DC=local` |
| **Service account** | A read-only user the app uses to search the directory. It never changes anything |
| **Nested group** | A group inside another group. People in the inner group also count for the outer one |
| **UPN** | `name@domain`, a user's AD sign-in name; often the same as the email |
| **Global catalog** | A forest-wide directory index (ports 3268 and 3269); only needed when users are spread over several domains |
| **Bootstrap admin** | A person made Admin by configuration so the very first Admin exists |
| **Public host** | The internet-facing name that serves only the QR check-in and evaluation pages |
| **Reverse proxy** | A front door (IIS or nginx) that handles HTTPS and forwards requests to the app |
| **`App_Data`** | The folder where the app keeps its database, images, uploads and encryption keys |
