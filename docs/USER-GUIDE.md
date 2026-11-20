# User guide

How to use Campus Hub, by role. The same short guides are inside the app under **Help**, in English and Arabic. Setup is in [SETUP.md](SETUP.md); every page is listed in [SITEMAP.md](SITEMAP.md).

## For everyone

- **Sign in** with your **company email address** and the password you use for your computer. Your short username also works.
- **Where you land depends on your role.** People who work on the events side (Staff, HOD, Dean, Media, Research, Admin) land on **Home**. A person who only works in the KPI pages (role **KPI team**) lands directly on the **KPI dashboard** and sees only the KPIs menu. If a page is not for your role you see a "Your role cannot open this page" screen with a button to the areas you can use. In test mode the login page lists the test accounts (password `dev`), one click signs in.
- **Home** opens after you sign in. It greets you, lists what is **waiting for you** for your role (requests to approve, requests returned to you, drafts, visits needing a bus, approved events to announce, new research, overdue KPI targets), gives **quick actions**, your **recent requests**, **events in the next 14 days** and a **KPI snapshot**. Admin also sees the **Finish the setup** checklist here.
- **Forgot your password?** Press the link under the sign-in button. If your company has a reset page, press **Reset my password**. Otherwise enter your email and press **Ask IT to reset it**; IT receives the request. The app cannot change AD passwords itself.
- **Language:** the button at the top switches English and Arabic (right-to-left). You are signed out after 30 minutes of inactivity.
- **Menus depend on your role.** A menu item you cannot see is not part of your role. Roles change at your next sign-in.
- The working week is Sunday to Thursday.

## Staff

You can request an event, a student bus trip, food for an approved event, and upload research.

### Ask for a guest event
1. Press the yellow **New request** button, then **New event request**.
2. Fill in the event, company and contact details. Upload the company logo (PNG or JPG, up to 2 MB). Your name and department are filled in for you.
3. Write the event content in **English and Arabic**; it is printed on the event image.
4. Choose **One day** or **Multiple days** (up to 60). The start and end times apply to every day.
5. Choose **Which KPI does this help?** (optional): pick the performance indicator this event supports, or leave it as "Not linked to a KPI".
6. Press **Save draft** to finish later, or **Submit for approval**.
6. Your request goes through the **approval steps** Admin set up (by default the HOD, then the Dean), one after the other. It is confirmed only after the last step approves. The event page shows where it is. You get an email when it moves to the next step and when it is decided. A rejection by any approver comes back to you with a comment.
7. After approval open the event: **Download image**, **Download QR**, **Print QR poster** or **Venue screen** (rotating QR), and press **Food request** if you need food.

You can edit a request only while it is a **Draft** or **Rejected**. A submitted request is locked until every approval step has decided.

### Request food for an approved event
Open the event, press **Food request**, enter the number of people, the meal, the delivery time, the location and notes. Admin confirms or rejects it and you get an email.

### Ask for a bus (external visit)
1. **New request > External visit (bus)**.
2. Enter the visit name, destination (and address), purpose, **which KPI it helps** (optional), **date**, **departure and return time**, pick-up point, grade or class, number of students, number of accompanying staff, and the supervisor's name and phone.
3. Press **Submit for approval**. A visit for today is allowed unless Admin set a minimum notice in Settings.
4. The approver Admin chose gets an email. When approved, the bus coordinator confirms the bus and you receive the bus details by email.
5. If it is rejected, read the comment, press **Edit & resubmit** and send it again.

The **External visits** page lists your visits and their status: Draft, Submitted, Approved (bus pending), Bus confirmed, Rejected.

### Upload research
1. **New request > Upload research** (or open **Research** and press **Upload research**).
2. Enter the title, choose **Research paper** or **Research proposal**, add the authors and a short summary, and choose the file (PDF, DOC or DOCX, up to 20 MB by default).
3. Press **Upload and send**. The file is emailed to the research in-charge and recorded. Nobody has to approve it.
4. **Research** lists your uploads. The **Email** column shows **Sent** when the mail went out. You can download your own files.

## HOD (head of department)

- **Pending approvals** lists event requests waiting for you; the number on the menu shows how many. Click an event name for the full details and content.
- Press **Approve** (a comment is optional). The request then moves to the next approval step and its people are emailed. The event is not confirmed until the last step approves.
- Or write a comment and press **Reject**. A comment is required.
- You get a reminder email after 2 working days. If Admin named one HOD in Settings, only that person can decide.
- You can also approve **external visits** when Admin has not listed specific approvers: open **External visits**, open the visit and press **Approve** or **Reject**.

## Approvers (any step of the approval flow)
- You see **Pending approvals** when Admin has made you an approver of a step (by role, for example HOD or Dean, or by name). The number on the menu counts the requests waiting for you.
- Each card shows **Step N of M** and the step name, who already approved it, and, when a step needs several approvals, how many it has (for example 1 / 2).
- Press **Approve** (comment optional) or write a comment and press **Reject** (a comment is required). You can decide once per step, and never on your own request.
- You get an email when a request reaches your step, and a reminder after 2 working days.

## Visit approvers and bus coordinators

Admin can name people (by email or username) in **Settings > External visits (bus)**.

- **Approvers** see every submitted visit and a badge on the **External visits** menu. Open a visit and press **Approve**, or write a comment and press **Reject** (a comment is required).
- **Bus coordinators** see approved visits waiting for a bus. Open the visit, type the bus details (bus number, driver, phone, pick-up time) and press **Confirm**. The requester is emailed.
- If Admin turns approval off, submitted visits go straight to the bus coordinator.

## Media officer

- When an event is approved you receive an **email** with the event details and the **image attached**.
- You can also open **My events**, click an approved event and press **Download image** or **Download QR**.
- The image layout is fixed; it already contains the company logo, presenter, date, venue and QR code.

## Research in-charge

- Every research upload arrives by **email** with the file attached (files over 10 MB are not attached; the email links to the archive).
- Open **Research archive** (you land there after signing in): every upload with title, authors, uploader and department, date, file name and size, and whether the email was sent.
- **Search** by title, author, uploader or file name, and press the download button to open a file.
- There is nothing to approve or reject.

## Admin

**Name and logo:** at **Admin > Settings**, the **Logo** card at the top uploads your logo (PNG, JPG or WEBP up to 3 MB; it is resized automatically) and **Remove** goes back to the built-in icon; the **Name** tab sets the app name in English and Arabic. Both show in the top bar, login page, public check-in page and (logo) the browser tab icon.

**Appearance** (Admin menu) changes the look of the whole platform for everyone: pick one of eight ready-made themes (Ocean, Emerald, Royal purple, Crimson, Teal, Sunset, Rose, Graphite) or choose your own **primary**, **secondary**, **accent** and **page background** colours; choose **Light**, **Dark** or **Follow the device**; and choose **Sharp / Soft / Round** corners. The preview on the right changes as you pick (with a Light / Dark switch), shows if white text on a colour would be hard to read, and nothing goes live until you press **Save the look**. **Reset to default** brings back the original look. Everyone gets a small Light / Dark / Auto switch in the user menu (remembered in their own browser); tick *Everyone must use this mode* to hide it. The colours also apply to the login page, public check-in page and KPI charts. The event image template has its own colours at **Settings > Event image**.

**Audit log** (Admin menu) is the activity log. The top shows the last 24 hours (events, sign-ins, failed sign-ins, security events, errors) and a 14-day chart; a red alert appears when one IP or account has 5 or more failed sign-ins in an hour. Filter by period (hour / 24 h / 7 d / 30 d / all or exact dates), category, severity, action, person, success or failure, or search the details, page, IP or item (for example `Event:12`). Click a row for everything about it: who (and their roles), what, the exact page, IP address, browser, how long it took and a **request id**; the buttons there list everything by that person or everything from the same request. **CSV** and **Excel** export the current filter (up to 50,000 rows). **Live** refreshes every 15 seconds. Old entries are deleted automatically after the number of days set at **Settings > Audit log** (365 by default, 0 keeps everything), and **Keeping the log** can delete older entries now; that deletion is itself logged. Server errors and very slow pages (5 s or more) are logged as System entries.

**Access & roles** (Admin menu) is the one page for access. It has a card for each role (Staff, HOD, Dean, Media, Research, KPI team, Admin) showing who has it (the AD group plus people you add by email), what it can do on the events side, which approval steps it handles, and what it can do in the KPI pages (with a Customize / Edit link). Add or remove a person on the card; the change applies at their next sign-in.

### First-time setup (about 10 minutes)
1. **Admin > LDAP & Email:** press **Use standard defaults**, enter the server, service account and password, press **Test LDAP** (add a test user to try a full sign-in), then set up mail and press **Send test email**. Then **Save**.
2. **Who gets each role:** press **Fetch groups from LDAP**. **Members** shows who is in a group (with emails and groups inside it). A role button adds the group to that role; a role can have several groups. **Preview people per role** shows exactly who would get each role, and lets you add a single person by email or username. Then **Save**.
3. **Admin > Settings:** Recipients (admin mailbox, media officer, HOD email for visits), **External visits (bus)** (approvers, bus coordinators, bus email, minimum notice), **Research** (research in-charge email), **Sign-in help** (password reset page and IT helpdesk email).
4. **Admin > Roles:** give roles to people who are not in an AD group yet, by email or username.
5. Check the **Event image** and **Email templates** tabs and adjust wording and colours.
6. **Admin > Approval flow:** decide who approves events, in which order and how many approvals each step needs (see below). Test: create a request as Staff, approve it at every step, and check the emails and the image.

The home page shows a setup checklist until the basics are done.

### Approval flow (who approves events)
Open **Admin > Approval flow**. Each card is one step; steps run from top to bottom.
- **Step name** is shown to everyone. **Role** lets everyone with that role decide. **Or only these people** lists emails or usernames; if you fill it in, only they can decide and the role is ignored. They still need a role in the system to sign in (for example Staff).
- **Approvals needed**: how many different people must approve before it moves on (for example 2 of 3 committee members; the number cannot be larger than the people listed).
- **Extra email**: told when a request arrives (people listed by email are always told; with none the admin mailbox is told).
- Use the arrows to change the order, **Active** to switch a step off, the bin to delete. **Add a step** at the bottom. A step with requests waiting cannot be switched off or deleted, and one step must stay active.
- Changes apply to requests that reach a step from now on. A rejection at any step sends the request back to the requester.

### Everyday tasks
- **Food requests:** confirm or reject each one.
- **Reports:** live attendance, scores per event and presenter, Excel exports.
- **Notifications:** check every automatic email was sent. **Audit log:** who approved, uploaded, downloaded, changed roles or settings.
- **Roles:** roles apply from the person's next sign-in. Remove a direct role with the trash button; group roles are changed by changing the group or the role's group list.
- Protect the public QR page: check-in is open from 30 minutes before the start until the end; one check-in per device; optional rotating QR on the venue screen (Settings > QR and public page).

## KPI dashboard
**KPI roles (pick the narrowest that fits):**
| Role | What they can do in the KPI pages |
|---|---|
| **KPI manager** (`KpiManager`) | Defines the KPIs: add, edit, delete, import, review items, assign KPIs to departments, manage departments, export. Does not update progress. |
| **KPI editor** (`KpiEditor`) | Updates target, deadline and status for **any** department; exports. Cannot change the KPI set. |
| **Department KPI officer** (`KpiOfficer`) | Updates **only one department** (chosen when you add them on Access & roles); the dashboard and tracking table show only that department. |
| **KPI viewer** (`KpiViewer`) | Read-only: dashboard, tracking table, Excel export. |
| **KPI team** (`Kpi`) | All-round: editor + manager (no delete). For small teams. |

None of these has any access to events, visits, research or reports; they land on the KPI dashboard. Admin can change what any of them may do at **KPIs > Permissions**. **Access & roles** has a "Compare the KPI roles" table.

**KPI-only people:** give them the role **KPI team** (Admin > Roles). They see only the KPIs menu and cannot open events, visits, research or reports. What they may do inside the KPI pages (view, update any department, assign, import) is the KPI role named **Kpi** at **KPIs > Permissions** (created automatically; Admin can change it).
**Events-only people:** to hide the KPIs menu from a role such as Staff, create a KPI role with the same name (for example `Staff`) at **KPIs > Permissions** and leave all permissions unticked.

Open **KPIs** in the top bar. The tabs along the top switch between the **Dashboard** and **Tracking**; people allowed to manage KPIs also see **KPI list**, **Review items**, **Import / export**, **Departments** and **Permissions**. The KPI pages follow the language button like the rest of the app (KPI and department names stay as they were imported). Admin can also add or edit KPIs, import or export Excel, manage KPI entities and set KPI permissions (Admin menu or the buttons at the top of the KPI pages). By default only Admin can change KPI data; to let another role do more, an Admin creates a KPI role with the same name (for example `HOD`) at **KPI permissions** and ticks what it may do.

## Event attendees (no account)

1. Open the phone **camera** and point it at the QR code on the screen or poster.
2. Type your **full name** and **organization / department**, then press **Check in**.
3. Press **Evaluate this event** and rate it from 1 to 5. Your evaluation is **anonymous**; names are kept only in the attendance list.

## Questions and fixes

| Problem | Answer |
|---|---|
| I cannot sign in | Use your company email and computer password. If it still fails, your account may be disabled, you may have no role yet (ask Admin), or two accounts may share your email (use your username). |
| I forgot my password | Use **Forgot your password?** on the sign-in page. |
| A menu item is missing | Menus depend on your role; ask Admin if you need another role. |
| I cannot edit my request | Only drafts and rejected requests can be edited. |
| An approver is away | The request waits at that step and a reminder is sent after 2 working days. Admin can add people to the step or lower *Approvals needed* in **Admin > Approval flow**. |
| I do not see the Food request button | Food requests are only for approved events, for the person who made the request (and Admin). |
| An attendee sees "Check-in is not open yet" or "closed" | Check-in opens 30 minutes before the start and closes at the end (Admin can change this). |
| An attendee sees "Already checked in" | Each phone checks in once per event; go straight to the evaluation. |
| Emails are not arriving | Admin: **Admin > Notifications** shows each email and its error; fix mail settings and press **Send test email**. |
| My research upload says "Not sent" | The file was saved but the email failed; the research in-charge can still download it from the archive. Tell Admin to check Notifications. |
