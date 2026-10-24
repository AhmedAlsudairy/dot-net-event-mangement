# User guide

How to use Guest Events, by role. The same short guides are inside the app under **Help**, in English and Arabic. Setup is in [SETUP.md](SETUP.md); every page is listed in [SITEMAP.md](SITEMAP.md).

## For everyone

- **Sign in** with your **company email address** and the password you use for your computer. Your short username also works.
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
5. Press **Save draft** to finish later, or **Submit for approval**.
6. The HOD decides. You get an email when it is approved or rejected. If it is rejected, read the comment, press **Edit & resubmit** and send it again.
7. After approval open the event: **Download image**, **Download QR**, **Print QR poster** or **Venue screen** (rotating QR), and press **Food request** if you need food.

You can edit a request only while it is a **Draft** or **Rejected**. A submitted request is locked until the HOD decides.

### Request food for an approved event
Open the event, press **Food request**, enter the number of people, the meal, the delivery time, the location and notes. Admin confirms or rejects it and you get an email.

### Ask for a bus (external visit)
1. **New request > External visit (bus)**.
2. Enter the visit name, destination (and address), purpose, **date**, **departure and return time**, pick-up point, grade or class, number of students, number of accompanying staff, and the supervisor's name and phone.
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
- Press **Approve** (a comment is optional). The system creates the image and QR code and sends all the emails.
- Or write a comment and press **Reject**. A comment is required.
- You get a reminder email after 2 working days. If Admin named one HOD in Settings, only that person can decide.
- You can also approve **external visits** when Admin has not listed specific approvers: open **External visits**, open the visit and press **Approve** or **Reject**.

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

### First-time setup (about 10 minutes)
1. **Admin > LDAP & Email:** press **Use standard defaults**, enter the server, service account and password, press **Test LDAP** (add a test user to try a full sign-in), then set up mail and press **Send test email**. Then **Save**.
2. **Who gets each role:** press **Fetch groups from LDAP**. **Members** shows who is in a group (with emails and groups inside it). A role button adds the group to that role; a role can have several groups. **Preview people per role** shows exactly who would get each role, and lets you add a single person by email or username. Then **Save**.
3. **Admin > Settings:** Recipients (admin mailbox, media officer, HOD), **External visits (bus)** (approvers, bus coordinators, bus email, minimum notice), **Research** (research in-charge email), **Sign-in help** (password reset page and IT helpdesk email).
4. **Admin > Roles:** give roles to people who are not in an AD group yet, by email or username.
5. Check the **Event image** and **Email templates** tabs and adjust wording and colours.
6. Test: create a request as Staff, approve it as HOD, and check the emails and the image.

The home page shows a setup checklist until the basics are done.

### Everyday tasks
- **Food requests:** confirm or reject each one.
- **Reports:** live attendance, scores per event and presenter, Excel exports.
- **Notifications:** check every automatic email was sent. **Audit log:** who approved, uploaded, downloaded, changed roles or settings.
- **Roles:** roles apply from the person's next sign-in. Remove a direct role with the trash button; group roles are changed by changing the group or the role's group list.
- Protect the public QR page: check-in is open from 30 minutes before the start until the end; one check-in per device; optional rotating QR on the venue screen (Settings > QR and public page).

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
| The HOD is away | The request waits and a reminder is sent after 2 working days. Admin can change who decides in Settings or give the role to someone else. |
| I do not see the Food request button | Food requests are only for approved events, for the person who made the request (and Admin). |
| An attendee sees "Check-in is not open yet" or "closed" | Check-in opens 30 minutes before the start and closes at the end (Admin can change this). |
| An attendee sees "Already checked in" | Each phone checks in once per event; go straight to the evaluation. |
| Emails are not arriving | Admin: **Admin > Notifications** shows each email and its error; fix mail settings and press **Send test email**. |
| My research upload says "Not sent" | The file was saved but the email failed; the research in-charge can still download it from the archive. Tell Admin to check Notifications. |
