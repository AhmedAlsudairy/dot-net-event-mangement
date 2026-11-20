"""End-to-end smoke test against a running Dev instance (Auth:Mode=Dev). Usage: python smoke_test.py"""
import http.cookiejar, re, ssl, sqlite3, struct, urllib.parse, urllib.request, datetime, uuid, os, sys

BASE = "https://localhost:7100"
DB = os.path.join(os.path.dirname(__file__), "src", "GuestEvents", "App_Data", "events.db")
ctx = ssl.create_default_context(); ctx.check_hostname = False; ctx.verify_mode = ssl.CERT_NONE


class NoRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, *a, **k): return None


class Client:
    def __init__(self):
        self.jar = http.cookiejar.CookieJar()
        self.op = urllib.request.build_opener(urllib.request.HTTPCookieProcessor(self.jar), urllib.request.HTTPSHandler(context=ctx))

    def get(self, path, raw=False):
        try:
            r = self.op.open(BASE + path)
        except urllib.error.HTTPError as e:
            r = e
        b = r.read()
        return (r.status if hasattr(r, "status") else r.code), (b if raw else b.decode("utf-8", "replace")), r.geturl()

    def post(self, path, data, token_from=None):
        tp = token_from or path
        _, html, _ = self.get(tp)
        m = re.search(r'name="__RequestVerificationToken"[^>]*value="([^"]+)"', html)
        data = dict(data)
        if m: data["__RequestVerificationToken"] = m.group(1)
        body = urllib.parse.urlencode(data).encode()
        try:
            r = self.op.open(urllib.request.Request(BASE + path, body))
        except urllib.error.HTTPError as e:
            r = e
        return r.status if hasattr(r, "status") else r.code, r.read().decode("utf-8", "replace"), r.geturl()

    def multipart(self, path, fields, token_from):
        _, html, _ = self.get(token_from)
        m = re.search(r'name="__RequestVerificationToken"[^>]*value="([^"]+)"', html)
        fields = dict(fields); fields["__RequestVerificationToken"] = m.group(1)
        bd = "----b" + uuid.uuid4().hex
        parts = []
        for k, v in fields.items():
            parts.append(f'--{bd}\r\nContent-Disposition: form-data; name="{k}"\r\n\r\n{v}\r\n'.encode())
        parts.append(f'--{bd}--\r\n'.encode())
        req = urllib.request.Request(BASE + path, b"".join(parts), {"Content-Type": f"multipart/form-data; boundary={bd}"})
        try:
            r = self.op.open(req)
        except urllib.error.HTTPError as e:
            r = e
        return r.status if hasattr(r, "status") else r.code, r.read().decode("utf-8", "replace"), r.geturl()


fails = 0
def check(name, cond, extra=""):
    global fails
    print(("PASS " if cond else "FAIL ") + name + (" -- " + extra if (extra and not cond) else ""))
    if not cond: fails += 1


def login(u):
    c = Client()
    s, h, url = c.post("/Account/Login", {"username": u, "password": "dev"}, "/Account/Login")
    check(f"login {u}", "/Account/Login" not in url, url)
    return c

now = datetime.datetime.now()
staff, hod, admin, dean = login("staff"), login("hod"), login("admin"), login("dean")
bad = Client(); s, h, _ = bad.post("/Account/Login", {"username": "staff", "password": "wrong"}, "/Account/Login")
check("bad password rejected", "Invalid credentials" in h)

# --- request: starts 10 minutes ago so check-in is open
f = {"Name": "Smoke Workshop", "Type": "workshop", "Presenter": "Dr. Test", "CompanyName": "Acme", "ContactName": "Con Tact",
     "ContactEmail": "contact@acme.test", "Date": now.strftime("%Y-%m-%d"), "StartTime": (now - datetime.timedelta(minutes=10)).strftime("%H:%M"),
     "EndTime": "23:59", "Venue": "Hall A", "ExpectedAttendees": "10", "ContentEn": "English content", "ContentAr": "محتوى عربي للفعالية", "submitAction": "submit"}
s, h, url = staff.multipart("/Events/Save", f, "/Events/Create")
check("create+submit request", "/Events/Details/1" in url, url)
s, h, _ = staff.get("/Events/Details/1"); check("status Submitted", "Submitted" in h)
s, h, _ = admin.get("/Events/Details/1"); check("admin sees request", s == 200)

# staff cannot approve
s, h, url = staff.get("/Approvals"); check("staff blocked from approvals", "Login" in url or "Denied" in url or s in (401, 403), url)

# reject -> comment required
s, h, _ = hod.post("/Approvals/Decide/1", {"decision": "reject", "comment": ""}, "/Approvals")
s, h, _ = hod.get("/Approvals"); check("pending list shows event", "Smoke Workshop" in h)
s, h, _ = hod.post("/Approvals/Decide/1", {"decision": "reject", "comment": "Need more detail"}, "/Approvals")
s, h, _ = staff.get("/Events/Details/1"); check("rejected with HOD comment", "Need more detail" in h and "Rejected" in h)
s, h, url = staff.multipart("/Events/Save", dict(f, Id="1"), "/Events/Edit/1"); check("resubmit after rejection", "/Events/Details/1" in url)
s, h, _ = staff.get("/Events/Details/1"); check("status Submitted again", "badge bg-warning" in h)

# approve
s, h, _ = hod.post("/Approvals/Decide/1", {"decision": "approve", "comment": "ok"}, "/Approvals")
# --- approval chain (default: step 1 HOD, step 2 Dean): the HOD's approval is NOT enough, the Dean must accept too
q = lambda sql, *a: sqlite3.connect(DB).execute(sql, a).fetchone()
step_of = lambda eid: q("select s.Name from Events e join ApprovalSteps s on s.Id=e.CurrentStepId where e.Id=?", eid)
check("default chain is HOD then Dean", [r[0] for r in sqlite3.connect(DB).execute("select Name from ApprovalSteps order by [Order]")] == ["HOD", "Dean"])
check("after HOD approval the event waits at the Dean step", q("select Status from Events where Id=1")[0] == 1 and step_of(1)[0] == "Dean", str(step_of(1)))
s, h, _ = staff.get("/Events/Details/1"); check("requester sees waiting at Dean, no image yet", "Waiting for approval at the step" in h and "Dean" in h and "Download image" not in h)
s, h, _ = hod.get("/Approvals"); check("HOD no longer sees it in pending", "Smoke Workshop" not in h)
s, h, _ = hod.post("/Approvals/Decide/1", {"decision": "approve", "comment": ""}, "/Approvals"); check("HOD cannot give the Dean's approval", q("select Status from Events where Id=1")[0] == 1 and step_of(1)[0] == "Dean")
s, h, _ = dean.get("/Approvals"); check("Dean sees it with the HOD's approval", "Smoke Workshop" in h and "Step 2 of 2" in h)
s, h, _ = staff.get("/Approvals"); check("staff cannot open approvals", "Smoke Workshop" not in h and s in (200, 403))
s, h, _ = dean.post("/Approvals/Decide/1", {"decision": "approve", "comment": "final ok"}, "/Approvals")
check("Dean approval makes it Approved (status 2)", q("select Status from Events where Id=1")[0] == 2)
stg = [r[0] for r in sqlite3.connect(DB).execute("select StepName from Approvals where EventRequestId=1 and Approved=1 order by Id")]; check("both approval steps recorded", stg == ["HOD", "Dean"], str(stg))
# Dean rejects: needs a comment, goes back to the requester, who can fix and resubmit to the HOD
s, h, url = staff.multipart("/Events/Save", dict(f, Id="0", Name="Dean reject test", submitAction="submit"), "/Events/Create"); did = int(url.rstrip("/").split("/")[-1])
hod.post(f"/Approvals/Decide/{did}", {"decision": "approve", "comment": ""}, "/Approvals")
dean.post(f"/Approvals/Decide/{did}", {"decision": "reject", "comment": ""}, "/Approvals"); check("Dean reject without comment refused", q("select Status from Events where Id=?", did)[0] == 1 and step_of(did)[0] == "Dean")
dean.post(f"/Approvals/Decide/{did}", {"decision": "reject", "comment": "Budget not approved"}, "/Approvals")
r = q("select e.Status, a.StepName from Events e join Approvals a on a.EventRequestId=e.Id where e.Id=? and a.Approved=0", did); check("Dean rejection recorded (status Rejected, step Dean)", r == (3, "Dean"), str(r))
s, h, _ = staff.get(f"/Events/Details/{did}"); check("requester sees the Dean's comment", "Budget not approved" in h and "Rejected" in h)
s, h, _ = staff.get("/Events/Details/1"); check("approved", "badge bg-success" in h and "Download image" in h)
s, img, _ = staff.get("/Events/DownloadImage/1", raw=True)
check("event image is PNG", img[:8] == b"\x89PNG\r\n\x1a\n", repr(img[:20]))
if img[:8] == b"\x89PNG\r\n\x1a\n":
    w, hh = struct.unpack(">II", img[16:24]); check("image size", (w, hh) == (1080, 1500), f"{w}x{hh}")
    open(os.path.join(os.path.dirname(__file__), "sample-event.png"), "wb").write(img)
s, qr, _ = staff.get("/Events/DownloadQr/1", raw=True); check("QR png", qr[:4] == b"\x89PNG")
con = sqlite3.connect(DB)
subj = [r[0] for r in con.execute("select Subject from Notifications")]
check("emails logged: final approval mails, step notices and requester updates", len(subj) >= 8, str(len(subj)))
check("the next step's people are emailed when a step approves", sum(1 for x in subj if x.startswith("Waiting for your approval (Dean)")) >= 2 and sum(1 for x in subj if "moved to the next approval step" in x) >= 2, str([x for x in subj if "Dean" in x or "next" in x][:4]))
token = con.execute("select Token from QRCodes where EventRequestId=1").fetchone()[0]
check("token unguessable length", len(token) >= 30, token)
s, h, _ = staff.get("/Events/Details/1")

# food (only approved events)
s, h, url = staff.post("/Food/Create", {"EventRequestId": "1", "People": "10", "Meal": "Lunch", "DeliveryTime": "12:00", "Location": "Hall A", "Notes": "no nuts"}, "/Food/Create?eventId=1")
check("food request", "/Events/Details/1" in url, url)
s, h, _ = admin.get("/Food"); check("admin sees food request", "no nuts" in h)
admin.post("/Food/SetStatus/1", {"status": "Confirmed"}, "/Food")
n = con.execute("select count(*) from Notifications").fetchone()[0]; check("food mail logged", n >= 6, str(n))

# --- public: anonymous, different clients
a = Client()
s, h, _ = a.get("/p/not-a-token"); check("bad token -> 404", s == 404)
s, h, _ = a.get(f"/p/{token}"); check("public page loads w/o login", s == 200 and "Full name" in h)
s, h, _ = a.get(f"/p/{token}/evaluate"); check("evaluate before check-in blocked", "check in first" in h)
s, h, _ = a.post(f"/p/{token}/checkin", {"fullName": "Ali Test", "organization": "Dept X", "email": "", "phone": "", "website": ""}, f"/p/{token}")
check("check-in works", "You are checked in" in h, h[-400:])
s, h, _ = a.get(f"/p/{token}"); check("second scan -> already checked in", "Already checked in" in h)
s, h, _ = a.post(f"/p/{token}/evaluate", {"overall": "5", "presenter": "4", "content": "4", "venueOrg": "3", "food": "", "comments": "great"}, f"/p/{token}/evaluate")
check("evaluation submitted", "Thank you for your feedback" in h, h[-300:])
s, h, _ = a.post(f"/p/{token}/evaluate", {"overall": "1", "presenter": "1", "content": "1", "venueOrg": "1"}, f"/p/{token}/evaluate")
check("second evaluation blocked", "Thank you" in h or "already" in h.lower())
check("only one evaluation stored", con.execute("select count(*) from Evaluations").fetchone()[0] == 1)

b = Client()  # honeypot bot
s, h, _ = b.post(f"/p/{token}/checkin", {"fullName": "Bot", "organization": "Bot", "website": "http://spam"}, f"/p/{token}")
check("honeypot: bot not stored", con.execute("select count(*) from Attendances where FullName='Bot'").fetchone()[0] == 0)

# anonymity: evaluation and attendance carry different hashes and no link
ah = con.execute("select DeviceHash from Attendances limit 1").fetchone()[0]; eh = con.execute("select DeviceHash from Evaluations limit 1").fetchone()[0]
check("attendance/evaluation hashes differ", ah != eh)
cols = [r[1] for r in con.execute("pragma table_info(Evaluations)")]
check("no attendance link column", not any("ttendance" in c for c in cols), str(cols))

# reports
s, h, _ = admin.get("/Reports"); check("reports page", s == 200 and "Smoke Workshop" in h)
s, h, _ = admin.get("/Reports/Event/1"); check("event report lists attendee", "Ali Test" in h)
s, x, _ = admin.get("/Reports/ExportAttendance/1", raw=True); check("excel export", x[:2] == b"PK")
s, h, _ = admin.get("/Reports/Live/1"); check("live count json", '"attendees":1' in h, h)
s, h, _ = staff.get("/Reports"); check("staff blocked from reports", "Smoke Workshop" not in h)
s, h, _ = admin.get("/Admin/Settings"); check("settings page", s == 200 and "AdminMailbox" in h)
s, h, _ = admin.get("/Admin/Audit"); check("audit has approvals", "Approve" in h and "Reject" in h)

# arabic toggle
s, h, _ = staff.post("/lang", {"lang": "ar", "returnUrl": "/"}, "/Events")
s, h, _ = staff.get("/Events"); check("arabic RTL", 'dir="rtl"' in h)

# stranger cannot read staff's event details
other = login("media"); s, h, _ = other.get("/Events/Details/1"); check("media can see approved event", s == 200)
s, h, _ = Client().get("/Events/Details/1"); check("anonymous redirected to login", "Sign in" in h or "تسجيل الدخول" in h)

# --- Admin: LDAP / SMTP connection settings
s, h, _ = admin.get("/Admin/Connections"); check("connections page", s == 200 and "Active Directory" in h)
s, h, _ = staff.get("/Admin/Connections"); check("staff blocked from connections", "Active Directory" not in h)
form = {"ldap[Host]": "dc.test.local", "ldap[Port]": "636", "ldap[UseSsl]": "true", "ldap[SkipCertValidation]": "false", "ldap[BaseDn]": "DC=test,DC=local",
        "ldap[ServiceAccountDn]": "CN=svc,DC=test,DC=local", "ldap[ServiceAccountPassword]": "S3cretLdapPw!", "ldap[GroupStaff]": "Events-Staff",
        "ldap[GroupHod]": "Events-HOD", "ldap[GroupMedia]": "Events-Media", "ldap[GroupAdmin]": "Events-Admin",
        "smtp[Enabled]": "false", "smtp[Host]": "127.0.0.1", "smtp[Port]": "1", "smtp[UseStartTls]": "false", "smtp[User]": "u", "smtp[Password]": "S3cretSmtpPw!", "smtp[From]": "events@test.local"}
s, h, _ = admin.post("/Admin/Connections", form, "/Admin/Connections")
s, h, _ = admin.get("/Admin/Connections")
check("saved values shown", "dc.test.local" in h and "127.0.0.1" in h)
check("passwords never shown", "S3cretLdapPw" not in h and "S3cretSmtpPw" not in h and "leave blank to keep" in h)
rows = dict(con.execute("select Key, Value from Settings where Key like 'Ldap:%' or Key like 'Smtp:%'").fetchall())
check("passwords encrypted in DB", rows["Ldap:ServiceAccountPassword"].startswith("enc:") and "S3cret" not in rows["Ldap:ServiceAccountPassword"] and rows["Smtp:Password"].startswith("enc:"))
check("non-secret stored plain", rows["Ldap:Host"] == "dc.test.local")
# blank password keeps the saved one
form2 = dict(form); form2["ldap[ServiceAccountPassword]"] = ""; form2["ldap[Host]"] = "dc2.test.local"
admin.post("/Admin/Connections", form2, "/Admin/Connections")
rows2 = dict(con.execute("select Key, Value from Settings where Key like 'Ldap:%'").fetchall())
check("blank password keeps saved secret", rows2["Ldap:ServiceAccountPassword"] == rows["Ldap:ServiceAccountPassword"] and rows2["Ldap:Host"] == "dc2.test.local")
s, h, _ = admin.post("/Admin/TestLdap", {"ldap[Host]": "127.0.0.1", "ldap[Port]": "1"}, "/Admin/Connections")
check("LDAP test returns failure message (unreachable)", '"ok":false' in h, h[:200])
s, h, _ = admin.post("/Admin/TestSmtp", {"smtp[Host]": "127.0.0.1", "smtp[Port]": "1", "to": "x@test.local"}, "/Admin/Connections")
check("SMTP test returns failure message (unreachable)", '"ok":false' in h, h[:200])
s, h, _ = admin.post("/Admin/TestSmtp", {"to": "bad"}, "/Admin/Connections"); check("SMTP test validates recipient", "valid test recipient" in h)
s, h, _ = admin.get("/Admin/Audit"); check("audit logs connection change without secrets", "ConnectionsChanged" in h and "S3cret" not in h)

# --- Admin: roles assigned inside the app (user has no AD group for it)
m = login("media")
s, h, url = m.get("/Events/Create"); check("media (no Staff role) cannot create requests", "Edit" not in h or "Denied" in url or "Login" in url or s in (401, 403), url)
s, h, _ = admin.get("/Admin/RolesPage"); check("roles page", s == 200 and "Access &amp; roles" in h)
admin.post("/Admin/AddRole", {"username": "MEDIA", "role": "Staff"}, "/Admin/RolesPage")
s, h, _ = admin.get("/Admin/RolesPage"); check("role listed", "MEDIA" in h.split('id="role-Staff"')[1].split('id="role-HOD"')[0])
m2 = login("media")  # new sign-in picks up the assigned role
s, h, url = m2.get("/Events/Create"); check("assigned Staff role works after re-login", s == 200 and "Presenter" in h, url)
admin.post("/Admin/AddRole", {"username": "MEDIA", "role": "Staff"}, "/Admin/RolesPage")
check("duplicate assignment ignored", con.execute("select count(*) from RoleAssignments").fetchone()[0] == 1)
admin.post("/Admin/RemoveRole/1", {}, "/Admin/RolesPage")
m3 = login("media"); s, h, url = m3.get("/Events/Create"); check("removed role no longer works", "Presenter" not in h, url)
s, h, _ = staff.get("/Admin/RolesPage"); check("staff blocked from roles page", "AD username" not in h)
s, h, _ = admin.get("/Admin/Audit"); check("role changes audited", "RoleAssigned" in h and "RoleRemoved" in h)

# --- guidance: help page, per-page guides, next-step box, poster, setup checklist
staff.post("/lang", {"lang": "en", "returnUrl": "/"}, "/Events")  # staff was switched to Arabic above
s, h, _ = staff.get("/Help"); check("help page opens", s == 200 and "How it works" in h or "كيف يعمل" in h)
check("help page has every role tab", all(x in h for x in ("h-staff", "h-hod", "h-media", "h-admin", "h-attendee", "h-faq")))
s, h, _ = admin.get("/Help?tab=admin"); check("admin help tab is default for admin", 'class="tab-pane fade show active" id="h-admin"' in h)
s, h, _ = Client().get("/Help"); check("help needs sign-in", "Sign in" in h or "تسجيل الدخول" in h)
for path, who, key in [("/Events", staff, "events"), ("/Events/Create", staff, "create"), ("/Approvals", hod, "approvals"), ("/Admin/Settings", admin, "settings"),
                       ("/Admin/Connections", admin, "connections"), ("/Admin/RolesPage", admin, "roles"), ("/Reports", admin, "reports"), ("/Reports/Event/1", admin, "report-event"),
                       ("/Food", admin, "food-admin"), ("/Admin/Audit", admin, "audit"), ("/Admin/Notifications", admin, "notifications"), ("/Food/Create?eventId=1", staff, "food-create")]:
    s, h, _ = who.get(path); check(f"guide panel on {path}", f'data-guide="{key}"' in h, s)
s, h, _ = staff.get("/Events/Details/1"); check("next-step box for requester (approved)", "What to do next" in h or "ما التالي" in h)
s, h, _ = staff.get("/Events/Details/1"); check("next-step mentions check-in window", "check in from" in h or "تسجيل الحضور من" in h)
s, h, _ = admin.get("/Events/Details/1"); check("next-step box for admin", "What to do next" in h)
s, h, _ = staff.get("/Events/Poster/1"); check("poster page with QR and both languages", s == 200 and "Scan to check in" in h and "امسح لتسجيل الحضور" in h and "Events/DownloadQr/1" in h)
s, h, _ = Client().get("/Events/Poster/1"); check("poster needs sign-in", "Scan to check in" not in h)
s, h, _ = admin.get("/"); check("admin setup checklist shown on home", "Finish the setup" in h and "Check the approval flow" in h)
s, h, _ = staff.get("/"); check("setup checklist hidden from staff", "Finish the setup" not in h)
# --- role home pages
check("staff home: greeting + quick actions", "Good " in h and "New event request" in h and "My recent requests" in h)
s, h, _ = hod.get("/"); check("hod home loads", s == 200 and "Head of department" in h and "My recent requests" not in h)
s, h, _ = dean.get("/"); check("dean home loads", s == 200 and "Dean" in h)
s, h, _ = admin.get("/Home/Error"); check("error page renders", s == 500 and "Something went wrong" in h)
# --- people who work only in the KPI pages (role Kpi) / only on the events side
kpi = login("kpi")
s, h, url = kpi.get("/"); check("kpi-only user lands on the KPI dashboard", "/Kpi/Dashboard" in url and s == 200)
check("kpi-only menu has KPIs, no events", "My events" not in h and "External visits" not in h and "bi-bullseye" in h)
for path in ("/Events", "/Visits", "/Approvals", "/Research", "/Reports"):
    s, h, url = kpi.get(path); check("kpi-only blocked from " + path, "/Account/Denied" in url or "/Account/Login" in url)
s, h, url = kpi.get("/Kpi/Tracking"); check("kpi-only can open tracking", s == 200 and "/Kpi/Tracking" in url)
s, h, _ = kpi.get("/Kpi/Admin/Kpis"); check("kpi-only can manage KPIs (role Kpi)", s == 200 and "KPI list" in h)
s, h, url = kpi.get("/Kpi/Admin/Roles"); check("kpi-only cannot open KPI permissions", "/Account/Denied" in url)
s, h, _ = staff.get("/"); check("staff menu has events and KPIs", "My events" in h and "bi-bullseye" in h)
s, h, url = staff.get("/Account/Login"); check("login page lists test accounts in dev mode", True)
anon = Client()
# --- logo: nothing uploaded yet, only the Admin can change it
s, h, _ = anon.get("/brand/logo"); check("no logo uploaded yet -> 404", s == 404)
s, h, url = staff.post("/Admin/RemoveLogo", {}, "/Account/Me"); check("staff cannot remove/upload the logo", "/Account/Denied" in url or s in (401, 403))
# --- the narrower KPI roles
def kpi_user(u): return login(u)
for u, can_manage, can_import, edit_any, tracking_btn in (("kpimanager", True, True, False, False), ("kpieditor", False, False, True, True), ("kpiofficer", False, False, False, True), ("kpiviewer", False, False, False, False)):
    c = kpi_user(u); s, h, url = c.get("/"); check(u + " lands on KPI dashboard", "/Kpi/Dashboard" in url)
    s, h, url = c.get("/Events"); check(u + " has no events access", "/Account/Denied" in url)
    s, h, url = c.get("/Kpi/Admin/Kpis"); check(u + (" can" if can_manage else " cannot") + " manage KPIs", (s == 200 and "KPI list" in h and "/Account/Denied" not in url) == can_manage)
    s, h, url = c.get("/Kpi/Admin/ImportExcel"); check(u + (" can" if can_import else " cannot") + " import", ("/Account/Denied" not in url) == can_import)
    s, h, url = c.get("/Kpi/Admin/Roles"); check(u + " cannot open KPI permissions", "/Account/Denied" in url)
    s, h, url = c.get("/Kpi/Tracking"); check(u + " can view tracking", s == 200 and "/Kpi/Tracking" in url)
s, h, _ = login("kpiofficer").get("/Account/Me"); check("my access: officer sees role + department", s == 200 and "Department KPI officer" in h and "Your department:" in h and "Needs:" in h)
s, h, _ = staff.get("/Account/Me"); check("my access: staff can request, cannot open reports", s == 200 and "Needs:" in h and "Admin" in h)
s, h, _ = anon.get("/Account/Me"); check("my access requires sign-in", "/Account/Login" in _ or s in (401, 403))
s, h, _ = staff.get("/"); check("home shows the signed-in-as strip", "You are signed in as" in h and "What can I open?" in h)
s, h, _ = anon.get("/Account/Login"); check("login explains what each role sees", "What will I see after signing in?" in h)
s, h, _ = login("kpiofficer").get("/Kpi/Tracking"); check("dept officer is scoped to one department", "alert-info" in h)
s, h, _ = login("kpieditor").get("/Kpi/Tracking"); check("kpi editor is not scoped", "alert-info" not in h)
s, h, _ = admin.get("/Admin/RolesPage"); check("access & roles page: every role card", s == 200 and all(('id="role-' + r + '"') in h for r in ("Staff", "HOD", "Dean", "Media", "Research", "Kpi", "KpiManager", "KpiEditor", "KpiOfficer", "KpiViewer", "Admin")) and "Approves step 1: HOD" in h)
s, h, _ = staff.get("/Admin/RolesPage"); check("access & roles page denied to staff", "/Account/Denied" in _ or s in (401, 403))
s, h, _ = anon.get("/Account/Login"); check("app name shown on login", "Campus Hub" in h and "Guest Events" not in h)
s, h, _ = anon.get("/Account/Login"); check("login shows test accounts + password in dev mode", "dev-user" in h and "<code>dev</code>" in h)
s, h, _ = staff.get("/Events"); check("help link in menu", "/Help" in h)
s, h, _ = Client().get("/Account/Login"); check("login has plain-language help", "company email address" in h)

# --- multi-day events
def last_id(): return sqlite3.connect(DB).execute("select max(Id) from Events").fetchone()[0]
today = now.date(); fmt = lambda d: d.strftime("%Y-%m-%d")
s, h, _ = staff.get("/Events/Create"); check("form has One day / Multiple days switch", 'id="dur1"' in h and 'id="durN"' in h and 'name="EndDate"' in h)
before = last_id()
s, h, url = staff.multipart("/Events/Save", dict(f, Name="Bad dates", Date=fmt(today), EndDate=fmt(today - datetime.timedelta(days=2))), "/Events/Create")
check("end before start is rejected", last_id() == before and "cannot be before" in h, url)
s, h, url = staff.multipart("/Events/Save", dict(f, Name="Too long", Date=fmt(today), EndDate=fmt(today + datetime.timedelta(days=90))), "/Events/Create")
check("more than 60 days is rejected", last_id() == before and "at most 60 days" in h)

s, h, url = staff.multipart("/Events/Save", dict(f, Name="Three-day course", Date=fmt(today), EndDate=fmt(today + datetime.timedelta(days=2))), "/Events/Create")
mid = last_id(); check("multi-day request created", mid == before + 1 and f"/Events/Details/{mid}" in url, url)
row = sqlite3.connect(DB).execute("select Date, EndDate from Events where Id=?", (mid,)).fetchone(); check("end date stored", row[1] is not None and row[1][:10] == fmt(today + datetime.timedelta(days=2)), str(row))
s, h, _ = staff.get(f"/Events/Details/{mid}"); check("details show range, days and daily times", fmt(today + datetime.timedelta(days=2)) in h and "3 days" in h and "Daily" in h)
hod.post(f"/Approvals/Decide/{mid}", {"decision": "approve", "comment": ""}, "/Approvals"); dean.post(f"/Approvals/Decide/{mid}", {"decision": "approve", "comment": ""}, "/Approvals")
s, img, _ = staff.get(f"/Events/DownloadImage/{mid}", raw=True); check("multi-day image generated", img[:4] == b"\x89PNG")
open(os.path.join(os.path.dirname(__file__), "docs", "sample-multiday.png"), "wb").write(img)
s, h, _ = staff.get(f"/Events/Details/{mid}"); check("next-step explains daily check-in", "3-day event" in h and "once per day" in h)
tok = sqlite3.connect(DB).execute("select Token from QRCodes where EventRequestId=?", (mid,)).fetchone()[0]
p = Client()
s, h, _ = p.get(f"/p/{tok}"); check("public page shows the range", fmt(today + datetime.timedelta(days=2)) in h and "Full name" in h)
s, h, _ = p.post(f"/p/{tok}/checkin", {"fullName": "Multi Day", "organization": "X", "website": ""}, f"/p/{tok}"); check("day-1 check-in works", "You are checked in" in h)
s, h, _ = p.get(f"/p/{tok}"); check("second scan the same day -> already", "Already checked in" in h)
days = sqlite3.connect(DB).execute("select Day from Attendances where EventRequestId=?", (mid,)).fetchall()
check("check-in stored with its day", len(days) == 1 and days[0][0][:10] == fmt(today), str(days))
s, h, _ = p.post(f"/p/{tok}/evaluate", {"overall": "5", "presenter": "5", "content": "5", "venueOrg": "5"}, f"/p/{tok}/evaluate"); check("evaluation after check-in", "Thank you" in h)
s, h, _ = admin.get(f"/Reports/Event/{mid}"); check("report shows per-day counts", "Multi Day" in h and fmt(today) in h)
s, x, _ = admin.get(f"/Reports/ExportAttendance/{mid}", raw=True); check("excel export for multi-day", x[:2] == b"PK")

# future multi-day event: not open yet, but the page still explains when it opens
s, h, url = staff.multipart("/Events/Save", dict(f, Name="Next week course", Date=fmt(today + datetime.timedelta(days=3)), EndDate=fmt(today + datetime.timedelta(days=5)), StartTime="09:00", EndTime="11:00"), "/Events/Create")
fid = last_id(); hod.post(f"/Approvals/Decide/{fid}", {"decision": "approve", "comment": ""}, "/Approvals"); dean.post(f"/Approvals/Decide/{fid}", {"decision": "approve", "comment": ""}, "/Approvals")
ftok = sqlite3.connect(DB).execute("select Token from QRCodes where EventRequestId=?", (fid,)).fetchone()[0]
s, h, _ = Client().get(f"/p/{ftok}"); check("future multi-day: not open yet + opening time", "not open yet" in h and "Opens at" in h and fmt(today + datetime.timedelta(days=3)) in h, h[-300:])
s, h, _ = Client().post(f"/p/{ftok}/checkin", {"fullName": "Too Early", "organization": "X", "website": ""}, f"/p/{ftok}"); check("early check-in refused", "not open yet" in h)
check("early check-in not stored", sqlite3.connect(DB).execute("select count(*) from Attendances where FullName='Too Early'").fetchone()[0] == 0)

# one-day event unchanged
s, h, _ = staff.get("/Events/Details/1"); check("one-day event shows no day badge", "3 days" not in h and "Daily" not in h)


# --- Admin configures the chain: add a 3rd step "Finance" that needs 2 approvals from 3 named people, then check the sequence
adm = admin
adm.post("/Admin/SaveStep", {"id": "0", "name": "Finance", "role": "", "approvers": "media@example.com, research@example.com, fin3@example.com", "required": "2", "notifyEmail": "", "enabled": "true"}, "/Admin/ApprovalFlow")
names = [r[0] for r in sqlite3.connect(DB).execute("select Name from ApprovalSteps order by [Order]")]; check("admin added a third step at the end", names == ["HOD", "Dean", "Finance"], str(names))
s, h, _ = adm.post("/Admin/SaveStep", {"id": "0", "name": "Bad", "role": "", "approvers": "a@x.com", "required": "3", "notifyEmail": "", "enabled": "true"}, "/Admin/ApprovalFlow")
check("needs more approvals than people listed is refused", "Bad" not in [r[0] for r in sqlite3.connect(DB).execute("select Name from ApprovalSteps")])
check("non-admin cannot open the flow page", staff.get("/Admin/ApprovalFlow")[2].find("/Account/Denied") >= 0)
s, h, url = staff.multipart("/Events/Save", dict(f, Id="0", Name="Three step event", submitAction="submit"), "/Events/Create"); tid = int(url.rstrip("/").split("/")[-1])
hod.post(f"/Approvals/Decide/{tid}", {"decision": "approve", "comment": ""}, "/Approvals"); dean.post(f"/Approvals/Decide/{tid}", {"decision": "approve", "comment": ""}, "/Approvals")
check("third step: waits at Finance after HOD and Dean", step_of(tid)[0] == "Finance" and q("select Status from Events where Id=?", tid)[0] == 1)
check("HOD cannot decide at Finance", (hod.post(f"/Approvals/Decide/{tid}", {"decision": "approve", "comment": ""}, "/Approvals"), step_of(tid)[0] == "Finance")[1])
media_c, research_c = login("media"), login("research")
s, h, _ = media_c.get("/Approvals"); check("named Finance approver sees the request", "Three step event" in h and "Step 3 of 3" in h)
media_c.post(f"/Approvals/Decide/{tid}", {"decision": "approve", "comment": ""}, "/Approvals")
check("1 of 2 Finance approvals: still waiting", q("select Status from Events where Id=?", tid)[0] == 1 and step_of(tid)[0] == "Finance")
media_c.post(f"/Approvals/Decide/{tid}", {"decision": "approve", "comment": ""}, "/Approvals")
check("same person cannot approve twice at a step", q("select count(*) from Approvals where EventRequestId=? and StepName='Finance'", tid)[0] == 1)
s, h, _ = media_c.get("/Approvals"); check("request disappears from the list of someone who already approved", "Three step event" not in h)
research_c.post(f"/Approvals/Decide/{tid}", {"decision": "approve", "comment": "ok"}, "/Approvals")
check("2nd Finance approval completes the chain: event Approved", q("select Status from Events where Id=?", tid)[0] == 2)
stg = [r[0] for r in sqlite3.connect(DB).execute("select StepName from Approvals where EventRequestId=? and Approved=1 order by Id", (tid,))]; check("every approval recorded with its step", stg == ["HOD", "Dean", "Finance", "Finance"], str(stg))
# own request: a requester who is also an approver cannot approve it
adm.post("/Admin/SaveStep", {"id": "0", "name": "Self", "role": "", "approvers": "staff@example.com", "required": "1", "notifyEmail": "", "enabled": "true"}, "/Admin/ApprovalFlow")
s, h, url = staff.multipart("/Events/Save", dict(f, Id="0", Name="Own request", submitAction="submit"), "/Events/Create"); oid = int(url.rstrip("/").split("/")[-1])
for c in (hod, dean, media_c, research_c): c.post(f"/Approvals/Decide/{oid}", {"decision": "approve", "comment": ""}, "/Approvals")
media_c.post(f"/Approvals/Decide/{oid}", {"decision": "approve", "comment": ""}, "/Approvals")
staff.post(f"/Approvals/Decide/{oid}", {"decision": "approve", "comment": ""}, "/Approvals")
check("requester cannot approve their own request", q("select Status from Events where Id=?", oid)[0] == 1 and step_of(oid)[0] == "Self")
# move + delete rules
sid = q("select Id from ApprovalSteps where Name='Self'")[0]
adm.post(f"/Admin/MoveStep?id={sid}&dir=-1", {}, "/Admin/ApprovalFlow"); names = [r[0] for r in sqlite3.connect(DB).execute("select Name from ApprovalSteps order by [Order]")]; check("step moved earlier", names.index("Self") == names.index("Finance") - 1, str(names))
adm.post(f"/Admin/DeleteStep?id={sid}", {}, "/Admin/ApprovalFlow"); check("a step with a request waiting cannot be deleted", q("select count(*) from ApprovalSteps where Id=?", sid)[0] == 1)

print("\nFAILED:", fails)
sys.exit(1 if fails else 0)
