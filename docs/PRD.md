# Campus Hub (events, external visits, research and KPIs) - Product Requirements

Last updated: 2026-11-23. See also [SETUP.md](SETUP.md), [USER-GUIDE.md](USER-GUIDE.md) and [SITEMAP.md](SITEMAP.md).

## 1. Overview

Campus Hub is an internal web app that takes a guest workshop or a student bus trip from request to approval, notification and (for events) attendance and evaluation, and records research papers and proposals sent to the research in-charge, with one audit trail and full Arabic/English support.

**Problem.** Guest events and student trips are arranged by email and phone. Approvals are slow and untracked, the media team gets no image, attendance is on paper, and the transport office hears about trips too late.

**Product.** Four areas share one login, role model, settings page, mail engine, look and audit log:

- **Guest event request** - staff request an event with a company guest; the request goes through an **approval chain the Admin configures** (by default HOD, then Dean); only after the last step approves does the system create the event image and a QR code, emails the media officer, contact and admin, collects check-ins from the public QR page, and gathers anonymous evaluations. Staff can also request food for an approved event.
- **External visit (bus) request** - staff request a bus to take students off campus; a separately configured approver decides; a bus coordinator confirms the bus and enters bus details.
- **Research upload** - staff upload a research paper or proposal (PDF or Word); it is emailed to the research in-charge and kept in an archive. There is nothing to approve.
- **KPI dashboard** - the strategic KPIs, which department is responsible, targets, deadlines and status, with charts, Excel import and export, and five KPI roles; events and visits can be linked to the KPI they help.

**Goals**

- One approval click triggers every follow-up (image, QR, emails) with no manual copying.
- Approvers, bus coordinators and email recipients are changeable by an Admin in Settings, with no code change.
- Sign in with the company email address and existing Active Directory password; no new passwords.
- Work in Arabic (right-to-left) and English, with optional Arabic digits.
- Every decision, sign-in and settings change is written to a detailed audit log.
- Each person lands on a page that fits their role and can see what they may open (Home, My access).
- The Admin controls the platform's look (colours, light/dark, fonts, logo, name) without code; it works on phones, tablets and desktops.

**Non-goals (v1)**

- No bus fleet scheduling, route planning or driver app; the bus coordinator types bus details as free text.
- No parent consent forms or per-student rosters for visits.
- No external-user accounts; guests only use the public QR pages.
- No payment or budget tracking.
- No review, scoring or approval of research documents; the archive is a record, not a workflow.

## 2. Users and roles

Eleven roles cover everyone who signs in; guests never sign in and only see the public QR pages. A person gets a role from their Active Directory group, from an in-app role assignment made by an Admin, or both. Six roles work on the events side (Staff, HOD, Dean, Media, Research, Admin) and five roles work only in the KPI pages.

| Role | AD group (default) | Who | Can do |
|---|---|---|---|
| Staff | Events-Staff | Teachers and coordinators | Create, edit and submit event requests and external visits; request food for their approved events; upload research papers and proposals; see their own items |
| HOD | Events-HOD | Head of department | Default first approval step for events (approve, which moves it to the next step, or reject); approve visits when no visit approvers are listed in Settings |
| Dean | Events-Dean | Dean | Default second and final approval step for events: nothing is confirmed until the last step approves |
| Media | Events-Media | Media officer | View approved events and download the event image and QR |
| Research | Research-InCharge | Research in-charge | Receives every research upload by email; browses, searches and downloads the research archive. Nothing to approve or reject |
| Admin | Events-Admin | Administration / IT | Everything: reports, food confirmation, settings, appearance, approval flow, access and roles, connections, audit log; can always approve visits, confirm buses and see the research archive; has every KPI permission |
| KPI team | none (given in the app) | A small team that does all the KPI work | KPI pages only: follow and update any department, assign KPIs, add/edit/import/export (no delete) |
| KPI manager | none | Owner of the KPI workbook | KPI pages only: define KPIs (add, edit, delete, import), assign them to departments, manage departments, export; does not update progress |
| KPI editor | none | Follow-up officer | KPI pages only: update target, deadline and status for any department; export |
| Department KPI officer | none | The person in a department who owns its KPIs | KPI pages only, one department only (chosen by the Admin): update that department's targets; the dashboard shows only that department |
| KPI viewer | none | Manager or committee member | KPI pages only: read-only dashboard and tracking, Excel export |
| Guest (no login) | none | Event attendees | Check in and evaluate through the public QR link |

A role can come from **several** AD groups, and any person can also be given a role directly by email address or username. The KPI roles have no AD group and are given in the app. Other roles are read-only in the KPI pages unless the Admin creates a KPI role of the same name to widen or hide them.

**Visit permissions are separate from roles.** In Settings, an Admin lists the people (email or username) who approve visits and who confirm buses. Those people see every submitted visit and a badge of what is waiting for them. They still need one of the roles to be able to sign in.

| Action on an external visit | Requester | Listed approver | Listed bus coordinator | Admin |
|---|---|---|---|---|
| Create, edit, submit (draft or rejected) | Yes | Only if Staff | Only if Staff | Yes |
| View | Own | All submitted | All submitted | All |
| Approve or reject | No | Yes | No | Yes |
| Confirm bus and enter bus details | No | No | Yes | Yes |

**Research permissions.**

| Action on a research upload | Staff (uploader) | Research in-charge | Admin |
|---|---|---|---|
| Upload a paper or proposal | Yes | Only if also Staff | Yes |
| See the archive | Own uploads | All uploads | All uploads |
| Download a file | Own files | All files | All files |

## 3. Functional requirements

### A. Guest event requests

| ID | Requirement |
|---|---|
| EV-1 | Staff create an event with name, type (workshop, seminar, training, other), presenter, company, company logo (PNG/JPG up to 2 MB), contact name, email and phone, venue, expected attendees and bilingual content. |
| EV-2 | An event can be one day or up to 60 days; start and end times apply to every day. |
| EV-3 | Staff can save a draft and submit later; a rejected event can be edited and resubmitted. |
| EV-4 | **Configurable approval chain.** Admin > **Approval flow** defines the steps, their order, who may decide at each step and how many approvals each step needs. The default is two steps: HOD, then Dean. |
| EV-4a | **Who decides.** A step lists either a role (everyone with it may decide) or named people (emails or usernames; then only they may decide). Each step has *Approvals needed* (1-20): that many different people must approve before the request moves on, e.g. 2 of 3 committee members. A step may be switched off. |
| EV-4b | **Sequence.** Steps run strictly one after the other. When a step has enough approvals the next step's people are emailed and the requester is told; after the last step the event is Approved (image, QR and the final emails). A rejection by any eligible person at any step needs a comment and returns the request to the requester, who can edit and resubmit; it then starts again at step 1. |
| EV-4c | **Rules.** Each person can approve once per step. A requester can never approve their own request. A person sees only the requests waiting at a step they may decide. A step with requests waiting cannot be switched off or deleted and at least one step must stay active. Every decision is stored with the step name and shown in the decision history. |
| EV-5 | A reminder email goes to the people of the current step after a configurable number of working days (Sunday to Thursday). |
| EV-6 | When the **last approval step** approves (the Dean in the default chain), the system generates the event image (locked template, bilingual) and a QR code, then emails the media officer (image), the company contact (image), the admin mailbox and the requester. |
| EV-7 | Status rolls over automatically: Approved, Event done, Evaluation closed. |

### B. Attendance and evaluation (public QR pages)

| ID | Requirement |
|---|---|
| QR-1 | Guests scan the QR and check in with name and organization (email and phone optional or required by setting); one check-in per device per day. |
| QR-2 | Check-in opens and closes by configurable windows around the event time. |
| QR-3 | Optional protections: rotating QR on the venue screen (refreshes every 60 seconds), or allow check-in only from venue network prefixes. |
| QR-4 | After checking in, a guest submits an anonymous evaluation (1 to 5 ratings and comments). Attendance and evaluation use different device-hash salts and have no link between them. |
| QR-5 | The public host exposes only the check-in and evaluation pages; everything else returns 404 on that host. |

### C. Food requests

| ID | Requirement |
|---|---|
| FD-1 | The owner of an approved event requests food: people, meal type, delivery time, location, notes. |
| FD-2 | The request is emailed to the admin mailbox; Admin confirms or rejects it and the requester is emailed. |

### D. External visits (bus requests)

| ID | Requirement |
|---|---|
| XV-1 | Staff create an external visit: visit name, destination and address, purpose, date, departure and return time, pick-up point, grade or class, number of students, accompanying staff, supervisor name and phone, notes. |
| XV-2 | Return time must be after departure time. The date cannot be in the past; same-day requests are allowed. |
| XV-3 | Minimum notice (days before the trip) is configurable; 0 means no limit. |
| XV-4 | Approval is separate from event approval. An Admin chooses whether approval is required, and who approves (email or username; empty = anyone with the HOD role; Admin can always approve). |
| XV-5 | If approval is not required, a submitted visit goes straight to the bus step. |
| XV-6 | Approver rejection needs a comment; the requester can edit and resubmit. |
| XV-7 | After approval, the bus request email goes to the configured bus recipients (default: admin mailbox and extra admin recipients). |
| XV-8 | A bus coordinator (listed people, or Admin) confirms the bus and records bus details such as bus number, driver and phone. |
| XV-9 | The requester is emailed on approval, rejection and bus confirmation when that setting is on. |
| XV-10 | Approvers and bus coordinators see a badge on the External visits menu counting what waits for them. |

### E. Reports, sign-in and administration

| ID | Requirement |
|---|---|
| AD-1 | Reports per event and overall: attendance versus expected, evaluation averages, response rate, average by presenter; Excel export. |
| AD-2 | Settings page with tabs: Name, Recipients, External visits, Research, Sign-in help, Approval and check-in, QR and public page, Event image, Evaluation, Email templates, Audit log, Display; and the logo card. |
| AD-3 | **Access & roles:** one page with a card per role (who has it, what it does on the events side, which approval steps it handles, its KPI permissions with Customize/Edit), where the Admin adds or removes people by email or username (and sets the department of a KPI officer), plus a comparison of the KPI roles. The Admin also configures the LDAP and SMTP connections from the browser. |
| AD-4 | Audit log of submissions, decisions, settings changes and food, bus and research actions; a notifications log of every email sent with success or error. |
| AD-4a | Advanced audit log: every entry has a category (Auth, Events, Visits, Research, Approvals, Admin, Access, KPI, Data export, System), a severity (Info, Warning, Error, Security), an outcome, the person and their roles, IP, browser, page and a request id. Sign-ins (and failed ones), blocked access, role and permission changes, KPI changes, exports, server errors and slow requests are all logged. The Admin page filters by period, category, severity, action, person and free text, opens a detail view per entry, follows one request end to end, warns about repeated failed sign-ins, exports CSV / Excel, can refresh live, and deletes entries older than a retention period (default 365 days, automatic daily). |
| AD-4b | Appearance, branding and fonts: see section I. |
| AD-4c | Excel exports: events (with approval history, KPI and counts), visits, evaluations, one event's attendance and the KPI report. |
| AD-5 | Arabic and English interface with right-to-left layout, optional Arabic digits and configurable date format; built-in help pages. |
| AD-6 | On the LDAP & Email page an Admin presses Fetch groups from LDAP to list the directory's groups, then assigns groups to roles. A role box turns green when its groups exist and red when one does not. Nothing is saved until the Admin saves. |
| AD-7 | Forgot password: the sign-in page links to a page with a Reset my password button (a URL an Admin sets, such as a self-service portal) and an Ask IT form that emails the IT helpdesk. The app never stores or changes passwords. The answer is the same for every address, and one address can ask once every 10 minutes. |
| AD-8 | The LDAP & Email page has a Use standard defaults button (LDAPS 636, verify certificate, nested groups, default role group names, Base DN worked out from the server name) and a matching one for mail (port 587, STARTTLS). Test LDAP is a readiness report: connection and certificate, service account sign-in, Base DN, each role group, how many users have an email address, and any duplicate emails that would block email sign-in. |
| AD-9 | Role assignment workspace: each role takes one or several AD groups (any match gives the role, nested groups included when on). Members opens a group to show its people with email addresses and the groups inside it, as a tree or a flat list; Copy emails copies them. Preview people per role lists every person who would get each role and from which group, flags groups that do not exist, and lets an Admin add or remove a single person by email or username without any group. Display choices (emails, usernames, tree or flat) are remembered in the browser. Reads are capped (150 groups, 500 members a group, 6 levels). |

### F. Research uploads

| ID | Requirement |
|---|---|
| RS-1 | Staff upload a research paper or a research proposal with title, type, authors, a short summary and one file (PDF, DOC or DOCX). |
| RS-2 | The file type, its content header and its size are checked before saving; the size limit defaults to 20 MB and an Admin sets 1 to 25 MB. |
| RS-3 | Each upload is recorded with uploader, department, time, file name and size, and written to the audit log. |
| RS-4 | The upload is emailed to the research in-charge address (default: the admin mailbox) with the file attached. Files over 10 MB are not attached; the email points to the archive instead. |
| RS-5 | The archive shows whether the email went out (Sent or Not sent). |
| RS-6 | There is no approval step: nobody accepts or rejects an upload. |
| RS-7 | The Research role and Admin see all uploads, search them and download files; staff see only their own uploads. |
| RS-8 | The uploader can receive a receipt email (a setting). |

### G. KPI link and KPI dashboard

| ID | Requirement |
|---|---|
| G1 | The event form and the external-visit form have an optional **Which KPI does this help?** picker: a searchable list (type a number or a word) grouped by domain in numeric order, showing each KPI's code, text and strategic goal, with keyboard use, a clear button and a plain drop-down fallback without JavaScript. One KPI can be chosen. |
| G2 | The chosen KPI (code and text) is shown on the event and visit detail pages and is kept even if the KPI list changes later. |
| G3 | Each role has KPI permissions (view dashboard, view tracking, update own department, update any department, assign, add/edit, delete, import, export, manage departments, manage users, manage roles). Roles map by name to KPI roles; Admin has all of them; a role without a KPI role of its own is read-only. |
| G4 | KPI saves from the tracking table are refused with a real 401/403 (not a redirect) when the user is signed out or has no permission. |
| G5 | A **Department KPI officer** is tied to one department by the Admin; the tracking table, dashboard, saves and exports are limited to that department. |
| G6 | The KPI pages follow the app language (English/Arabic), the theme (including dark mode and the KPI charts) and the fonts. Tracking rows, the KPI list and other tables become cards on phones. |
| G7 | KPI changes (add, edit, delete, tracking updates, imports, exports, departments, permissions) are written to the audit log. |

### H. Landing, access and guidance

| ID | Requirement |
|---|---|
| H1 | After sign-in, events-side roles land on **Home**: what is waiting for them (requests to approve, requests returned, drafts, visits needing a bus, approved events to announce, new research, overdue KPI targets), quick actions for their role, recent requests, events in the next 14 days, a KPI snapshot, and for Admin a setup checklist. |
| H2 | People who have only a KPI role land on the KPI dashboard; people with no role see "No access yet"; opening a page outside one's role shows "Your role cannot open this page" with links to what they can use. |
| H3 | **My access** lists the person's roles in plain words, their department (KPI officer), the approval steps they handle and every area with a tick or the role it needs, plus how to ask the Admin. |
| H4 | The sign-in page explains what each kind of role sees; in demo mode it lists the test accounts and signs in with one click. A "You are signed in as" bar and the user menu show the person's roles. |
| H5 | The top menu shows only what the person may use and stays on one line from 1200 px wide for every role and both languages. |

### I. Appearance, branding and fonts

| ID | Requirement |
|---|---|
| I1 | **Appearance.** The Admin chooses one of eight ready-made themes or four own colours (primary, secondary, accent, page background), light / dark / follow-the-device mode and corner style (sharp, soft, round). A live preview in light and dark is built by the server from the same code as the real theme; white text on the primary and secondary colours must reach 3:1 (4.5:1 is shown as readable) or the colour is refused. |
| I2 | The theme applies to every page, the login and public pages and the KPI charts through CSS variables. People can choose their own Light / Dark / Auto in their browser unless the Admin locks the mode for everyone. Reset restores the default. |
| I3 | **Name and logo.** The Admin sets the app name in English and Arabic and uploads a logo (PNG, JPG or WEBP up to 3 MB, decoded and resized to 256 px) shown in the top bar, login and public pages and as the tab icon. |
| I4 | **Fonts.** One font for English pages and one for Arabic pages from 16 built-in fonts or imported font files (WOFF2, WOFF, TTF, OTF; up to 3 MB and 10 files; type decided by file content; stored in `App_Data/fonts` and served by the app, so they work offline). If a font cannot load, the system font is used. |
| I5 | **Text size.** The Admin sets a default (90 to 120 percent); each person can change their own text size unless the theme is locked. |
| I6 | All appearance changes are logged. |

### J. Phones, tablets and desktops

| ID | Requirement |
|---|---|
| J1 | Every page fits widths from 320 px up without sideways scrolling, in English and Arabic. |
| J2 | Wide tables become readable cards on phones (KPI tracking, KPI list, audit log, visits, reports, food, research, email log, review items); tab bars scroll sideways; pop-ups are full screen on phones; form fields use 16 px text; action bars respect the phone's safe area. |
| J3 | Touch screens get tap areas of about 40 px for links, switches and tabs; how-to boxes start folded on phones. |

## 4. Workflows and status lifecycles

```mermaid
stateDiagram-v2
    direction LR
    state "Guest event" as EV {
        [*] --> E_Draft: save
        E_Draft --> E_Submitted: submit
        E_Submitted --> E_Submitted: a step approves, next step starts (emails)
        E_Submitted --> E_Approved: last step approves (image, QR, 4 emails)
        E_Submitted --> E_Rejected: any step rejects (comment)
        E_Rejected --> E_Draft: edit and resubmit
        E_Approved --> E_Done: event ends
        E_Done --> E_Closed: evaluation window ends
    }
    state "External visit" as XV {
        [*] --> V_Draft: save
        V_Draft --> V_Submitted: submit
        V_Submitted --> V_Approved: approver approves (or approval is off)
        V_Submitted --> V_Rejected: approver rejects (comment)
        V_Rejected --> V_Draft: edit and resubmit
        V_Approved --> V_Bus: coordinator confirms bus
    }
```

Both flows use the same pattern: draft, submit, decision, follow-up. On approval an event creates its image and QR and sends four emails; a visit sends the bus request email. If an Admin turns visit approval off, a submitted visit skips the decision and goes straight to Approved. A rejection always needs a comment and returns the request to its owner, who edits and resubmits the same record.

Research uploads have no lifecycle: one upload is saved, emailed to the research in-charge and archived, and nobody approves it.

## 5. Sitemap and navigation

See [SITEMAP.md](SITEMAP.md) for the diagram and every route.

The top bar shows **Home**, My events, External visits, Research, **KPIs**, **Approvals** (anyone who may decide at a step) and an **Admin** menu (Reports & Excel, Food request, Settings, Appearance, Approval flow, Access & roles, LDAP & Email, KPI entities, Import KPIs, Audit log, Notifications), each only for the roles that may use it. A yellow New request button lets Staff and Admin start an event, an external visit or a research upload. The External visits link carries a badge for approvers and bus coordinators, and Approvals a count. The user menu has My access, Light / Dark / Auto, text size and sign out. People with only KPI roles see just the KPIs menu.

## 6. Architecture

```mermaid
flowchart TD
    IB["Internal browsers<br/>Staff, HOD, Dean, Media, Admin"] --> PX
    GP["Guest phones<br/>scan the QR, no sign-in"] --> PX
    PX["Reverse proxy<br/>TLS, two hostnames;<br/>the public host reaches only /p"]
    PX --> APP

    subgraph APP["Campus Hub web app (ASP.NET Core 8 MVC, one process)"]
        CT["Controllers and views<br/>Home, Events, Visits, Research, Approvals,<br/>Food, Reports, Admin (Settings, Appearance,<br/>Approval flow, Access and roles, Audit),<br/>Account (sign-in, My access), Public QR"]
        KP["KPI dashboard (Razor Pages, /Kpi)<br/>Dashboard, Tracking, KPIs, Import/Export,<br/>Entities, KPI roles"]
        KP --> SV
        SV["Services<br/>Workflow, Approval chain, Settings, Audit,<br/>language, Theme, Fonts, Brand (logo),<br/>Mail, QR, Image (SkiaSharp), LDAP, device hash"]
        PR["Protection and jobs<br/>cookie sign-in, role checks,<br/>anti-forgery, rate limits,<br/>public host filter, error and slow-request log,<br/>10-minute tick job, daily log clean-up"]
        CT --> SV
    end

    APP -- "EF Core" --> DB[("Database<br/>SQLite or SQL Server")]
    APP -- "EF Core (migrations)" --> KDB[("KPI database<br/>App_Data/kpi_dashboard.db (SQLite)")]
    APP -- "read and write" --> FS[("App_Data folder<br/>event images, company logos, research uploads,<br/>app logo, imported fonts, keys")]
    APP -- "LDAPS 636" --> AD["Active Directory<br/>sign-in and groups"]
    APP -- "SMTP" --> SM["Mail server"]
```

A single ASP.NET Core 8 MVC application serves both the internal site and the public QR pages; the proxy and the public host filter decide which face a request sees. Controllers call services; services read and write the database through Entity Framework Core, write generated files to App_Data, talk to Active Directory over LDAPS and send mail over SMTP. Visits reuse the same settings, mail, audit and role services as events, so the new feature added one controller, one table and a few views; research uploads did the same and added a folder of files.

| Layer | Technology |
|---|---|
| Web | ASP.NET Core 8 MVC, Razor views, Bootstrap 5.3 (light and dark) and Bootstrap Icons from a CDN; the look is driven by CSS variables built by the server (`Theme`, `Fonts`) |
| Data | Entity Framework Core 8 with SQLite (default) or SQL Server |
| Auth | LDAP via the Novell LDAP library, cookie authentication, optional Negotiate (Windows SSO) |
| Mail | MailKit over SMTP, every send logged in Notifications |
| Image and QR | SkiaSharp (with the Linux native packages, so Windows and Linux both work) for the event image and the logo; a QR generator for the code |
| Excel | ClosedXML for report exports and the KPI Excel import/export |
| KPI dashboard | ASP.NET Core Razor Pages under `/Kpi`, its own EF Core context and SQLite file, Chart.js charts |
| Background | A hosted service that ticks every 10 minutes (reminders, status roll-over) and once a day deletes audit entries past the retention period |

**KPI dashboard (merged in).** The former stand-alone KPI Alignment & Execution Dashboard is part of this app. It has no accounts of its own: the events sign-in is the only one, and the `/Kpi` pages sit behind the same cookie. Permissions are looked up per request from the roles in the cookie: each role maps to the KPI role of the **same name** (Admin = every permission; a role without a KPI role of its own = read-only Viewer). Five KPI roles are created automatically (`Kpi`, `KpiManager`, `KpiEditor`, `KpiOfficer`, `KpiViewer`) and given to people at Access & roles; a department officer carries the department id as a claim (`kpi_entity`) so the pages and saves are limited to it. An Admin can add or edit a KPI role named like any app role. The KPI data lives in a separate SQLite file (`ConnectionStrings:Kpi`, default `App_Data/kpi_dashboard.db`) migrated automatically at start, so it can stay SQLite even when the events database is SQL Server. Events and visits point at a KPI by id (`KpiId`) and keep a text snapshot (`KpiLabel`); there is no database link between the two files.

**Look and feel.** `Theme` turns the Admin's four colours, mode and corner style into CSS variables (including the soft tints and a full dark palette); `Fonts` adds the chosen fonts (Google Fonts links and `@font-face` rules for imported files) and the default text size; `Brand` stores the resized logo. Both layouts (internal and public) inject them, so every page, including login and the KPI pages and charts, follows the theme. The user's own Light / Dark / Auto and text size live in the browser.

**Audit.** `AuditService` writes every entry with category, severity, outcome, roles, IP, browser, page, item and a per-request id; `AuditCatalog` names each action. A middleware logs unhandled exceptions, server errors, blocked API calls and requests slower than 5 seconds without logging normal page views.

**Request flow for an external visit.** A staff member submits a visit; the app saves it, writes an audit row and emails the approver. The approver opens the link, decides, and the app emails the requester and the bus recipients; the bus coordinator confirms the bus and the requester gets the final email.

**Request flow for a research upload.** A staff member uploads a PDF or Word file; the app checks it, saves it under `App_Data/research` with a random name, writes a database row and an audit row, and emails the research in-charge with the file attached. The in-charge opens the archive, searches and downloads.

**Fetching groups.** An Admin on the LDAP & Email page presses Fetch groups; the app binds with the service account, lists every group under the Base DN, and returns the names to the page. Nothing is stored until the Admin saves the role-to-group choices.

## 7. Data model

The events database holds 14 tables managed by Entity Framework Core. The separate KPI database (Kpis, Entities, TrackingItems, ReviewItems, Roles, RolePermissions and a few unused legacy tables) is described in the KPI dashboard's own model and is migrated with EF Core migrations. External visits and research uploads are standalone tables, so they do not touch the event tables.

| Table | Purpose | Key fields |
|---|---|---|
| Events | Guest event request | CurrentStepId, StepStartedAt, KpiId, KpiLabel, Name, Type, Presenter, CompanyName, ContactName/Email/Phone, Date, EndDate, StartTime, EndTime, Venue, ExpectedAttendees, ContentAr/En, Status, requester fields, SubmittedAt |
| Approvals | One row per decision on an event | EventRequestId, ApproverUsername, Approved, StepId, StepName (snapshot), Comment, DecidedAt |
| ApprovalSteps | The configurable approval chain | Order, Name, Role, Approvers (emails/usernames), RequiredApprovals, NotifyEmail, Enabled |
| Images | Generated event image | EventRequestId (1:1), FilePath |
| QRCodes | QR token and time windows | EventRequestId (1:1), Token (unique), CheckInOpen, CheckInClose, EvaluationClose |
| Attendances | Check-ins | EventRequestId, FullName, Organization, Day, DeviceHash (salt A) |
| Evaluations | Anonymous ratings | EventRequestId, Overall, Presenter, Content, VenueOrg, Food, Comments, DeviceHash (salt B) |
| FoodRequests | Food order for an event | EventRequestId, People, Meal, DeliveryTime, Location, Status |
| Visits | External visit (bus request) | KpiId, KpiLabel, Name, Destination, Date, DepartureTime, ReturnTime, PickupLocation, StudentCount, StaffCount, SupervisorName/Phone, Status, DecisionBy, DecisionComment, BusDetails |
| Settings | Key/value configuration | Key, Value |
| Notifications | Log of every email | Recipient, Subject, Success, Error |
| RoleAssignments | In-app roles for people, by email or username | Username, Role (unique together), EntityId (the department of a KPI officer), AssignedBy, AssignedAt |
| AuditLogs | Who did what, when and from where | User, Action, Details, At, Category, Severity, Outcome, Ip, UserAgent, Method, Path, Target, Roles, CorrelationId, DurationMs |
| ResearchDocs | Uploaded research paper or proposal | Title, DocType, Authors, Summary, OriginalFileName, FilePath, FileSize, uploader fields, UploadedAt, EmailSent |

| Entity | Statuses |
|---|---|
| Event | Draft, Submitted (in approval; the event stores which step it waits at), Approved, Rejected, Event done, Evaluation closed |
| Food request | Submitted, Confirmed, Rejected |
| External visit | Draft, Submitted, Approved (bus pending), Bus confirmed, Rejected |
| Research upload | None: recorded and emailed on upload, with an EmailSent flag |

SQLite is the default database. SQL Server is supported by setting one configuration value. On start, the app creates missing tables and columns, so existing databases pick up new tables (Visits, ResearchDocs, ApprovalSteps) and new columns (KpiId/KpiLabel, the approval-step fields, the audit columns and indexes, RoleAssignments.EntityId) without a manual migration. Appearance, font, name and retention choices are rows in Settings (keys `Theme*`, `Font*`, `CustomFonts`, `AppNameEn/Ar`, `AuditRetentionDays`); the logo is `App_Data/branding/logo.png` and imported fonts are files in `App_Data/fonts`.

## 8. Security, privacy and non-functional requirements

The internal app and the public QR pages run as one application with two faces, and the public face is locked down to the two pages guests need.

| Area | Requirement |
|---|---|
| Sign-in | **Dev mode (built-in test accounts, password `dev`) is for local testing only and must never be reachable by other people.** Active Directory over LDAPS (port 636) with a service account; the sign-in name is the company email address (the AD mail attribute), and the short username, `DOMAIN\user` and UPN also work; optional Windows single sign-on (Kerberos/NTLM) when the server is domain-joined. A Dev mode exists for local testing only. |
| Session | Cookie named `ge_auth`, HttpOnly, Secure, 30-minute sliding expiry. |
| Brute-force protection | After 5 wrong passwords for one name the app stops asking AD for 10 minutes; login is also rate limited per IP (default 20 per minute). |
| CSRF | Anti-forgery token validated on every POST. |
| Public host | When `App:PublicHost` is set, requests on that host reach only `/p`, static files and the language switch; all else returns 404. Public pages are rate limited (60 reads and 30 submits per minute per IP by default). |
| Anonymity | Attendance and evaluation hash the device with two different salts and share no key, so a rating cannot be traced to a check-in. |
| Secrets | LDAP and SMTP passwords saved from the browser are encrypted with ASP.NET Data Protection; keys live in `App_Data/keys` and use Windows DPAPI on Windows. Back up that folder with the database. |
| Uploads | The company logo on an event must be PNG or JPG, up to 2 MB, and its file header is checked, not only the extension. The app logo is decoded as an image (PNG, JPG, WEBP, up to 3 MB) and re-saved resized. Imported fonts are identified by their first bytes (WOFF2, WOFF, TTF, OTF), limited to 3 MB and 10 files, stored under a random name and served with `nosniff`. |
| Research files | Only PDF, DOC and DOCX; the file header must match the extension; size limited (default 20 MB). Stored under `App_Data/research` with a random name, never served as a static file. Download is allowed only to the uploader, the Research role and Admin, and each download is audited. |
| Group lookup | Fetch groups, group members and the per-role preview are Admin only, use the LDAP service account, read names and emails only (capped) and are audited where they change data. Results are shown on the page and not stored. |
| Forgot password | No password is stored or changed by the app; the answer is identical for every address; one request per address every 10 minutes. |
| Headers | nosniff, no-referrer and X-Frame-Options DENY on every response; HSTS outside Development. |
| Audit | Sign-ins and failed sign-ins, blocked access, submissions, decisions, bus confirmations, research uploads and downloads, role and KPI-permission changes, KPI changes, exports, settings, appearance and font changes, server errors and slow requests are logged with person, roles, time, IP, page and request id. Entries cannot be edited; the Admin can export them and old ones are deleted automatically after a retention period (default 365 days). Repeated failed sign-ins raise an alert. |
| Language and display | Arabic (right-to-left) and English, switched per browser by cookie; light and dark mode and text size per browser; layouts from 320 px phones to wide desktops. Built-in fonts load from Google Fonts (imported fonts work offline). |
| Availability | Single-server deployment; a background job runs every 10 minutes for reminders and status roll-over. Target (an assumption to confirm): business-hours availability, daily database and `App_Data` backup. |
| Capacity | Designed for one school or department: hundreds of requests a year, not thousands of concurrent users (an assumption to confirm). |

**Privacy.** Guests give name and organization (email and phone optional). Student data in external visits is limited to counts, grade and the supervisor's contact; no student names are stored. Uploaded research files are visible only to their uploader, the research in-charge and Admin.

## 9. Setup and deployment

See [SETUP.md](SETUP.md) for the full guide: requirements, local run, production deployment, connecting the real directory, operating and troubleshooting.

## 10. Configuration reference

See [SETUP.md](SETUP.md#configuration-reference) for every first-run key and every Settings page option.

## 11. Open questions and roadmap

**Open questions**

- [ ] Should a same-day visit be blocked when the departure time has already passed?
- [ ] Is transport a separate team that needs its own role, instead of a list of people?
- [ ] Should visit emails become editable templates like the event emails?
- [ ] Should the requester be able to cancel an approved visit, and should the bus coordinator be told?
- [ ] Should the Media role see the External visits page? Today it sees only its own (empty) list.
- [ ] Confirm the availability and capacity targets in section 8; they are assumptions.
- [ ] Should the research in-charge also be allowed to upload, or only Staff?
- [ ] Should uploads be scanned for viruses and kept for a set number of years?
- [ ] Is a single research in-charge address enough? Several addresses are supported, separated by commas.

**Roadmap candidates**

| Priority | Item | Why |
|---|---|---|
| 1 | Departure-time check and cancel flow for visits | Closes the gaps in same-day and late changes |
| 2 | Visit email templates in Settings | Match the event emails; Arabic and English wording |
| 3 | Visit reminder to the approver after N working days | Same behavior as event approvals |
| 4 | Visit counts by month, destination and approval time in Reports (the visits Excel export already exists) | Management view of trips |
| 5 | Calendar view of approved visits and bus conflicts | Avoids two trips needing the same bus |
| 6 | Student roster and parent consent per visit | Only if the school requires it |
| 9 | Local accounts (email and password kept in the app) as an alternative to Active Directory | For deployments without a reachable directory, such as a public VPS |
| 7 | Research: virus scan, retention rule and filters by type and year | Keeps the archive safe and usable as it grows |
| 8 | In-app password reset (needs delegated reset rights for the service account) | Today the app points to the company reset page or IT |
