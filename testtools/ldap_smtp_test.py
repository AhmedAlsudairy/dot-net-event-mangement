"""Checks real LDAP sign-in (fake AD on :1389) and real SMTP sending (Mailpit on :1025). App must run with Auth__Mode=Ldap (see README)."""
import http.cookiejar, json, re, ssl, sys, urllib.parse, urllib.request

BASE = "https://localhost:7100"; MAILPIT = "http://127.0.0.1:8025"
ctx = ssl.create_default_context(); ctx.check_hostname = False; ctx.verify_mode = ssl.CERT_NONE

class Client:
    def __init__(self):
        self.op = urllib.request.build_opener(urllib.request.HTTPCookieProcessor(http.cookiejar.CookieJar()), urllib.request.HTTPSHandler(context=ctx))
    def get(self, p):
        try: r = self.op.open(BASE + p)
        except urllib.error.HTTPError as e: r = e
        return r.read().decode("utf-8", "replace"), r.geturl()
    def post(self, p, data, tok=None):
        html, _ = self.get(tok or p)
        m = re.search(r'name="__RequestVerificationToken"[^>]*value="([^"]+)"', html)
        d = dict(data); d["__RequestVerificationToken"] = m.group(1)
        try: r = self.op.open(urllib.request.Request(BASE + p, urllib.parse.urlencode(d).encode()))
        except urllib.error.HTTPError as e: r = e
        return r.read().decode("utf-8", "replace"), r.geturl()

fails = 0
def check(n, c, x=""):
    global fails
    print(("PASS " if c else "FAIL ") + n + ("" if c else " -- " + str(x)))
    fails += 0 if c else 1

def login(u, p):
    c = Client(); h, url = c.post("/Account/Login", {"username": u, "password": p}, "/Account/Login")
    return c, "/Account/Login" not in url

for u, p in [("staff", "staff123"), ("hod", "hod123"), ("media", "media123"), ("admin", "admin123")]:
    check(f"LDAP login {u}", login(u, p)[1])
check("wrong password refused", not login("staff", "nope")[1])
check("unknown user refused", not login("ghost", "x")[1])
check("disabled AD account refused", not login("disabled", "disabled123")[1])
check("user without any group refused", not login("nogroup", "nogroup123")[1])

admin, _ = login("admin", "admin123")
h, _ = admin.get("/"); check("admin menu present", "LDAP" in h and "Roles" in h or "الأدوار" in h)
# role assigned in the app lets the group-less user in
admin.post("/Admin/AddRole", {"username": "nogroup", "role": "Staff"}, "/Admin/RolesPage")
check("group-less user allowed after role assignment", login("nogroup", "nogroup123")[1])
# staff sees only Staff pages
st, _ = login("staff", "staff123"); h, _ = st.get("/Admin/Connections"); check("staff cannot open admin pages", "Active Directory" not in h)

# Connections page: test + save + real mail
form = {"ldap[Host]": "127.0.0.1", "ldap[Port]": "1389", "ldap[UseSsl]": "false", "ldap[BaseDn]": "DC=test,DC=local", "ldap[SkipCertValidation]": "false",
        "ldap[ServiceAccountDn]": "CN=svc-events,OU=Service,DC=test,DC=local", "ldap[ServiceAccountPassword]": "svcpass",
        "ldap[GroupStaff]": "Events-Staff", "ldap[GroupHod]": "Events-HOD", "ldap[GroupMedia]": "Events-Media", "ldap[GroupAdmin]": "Events-Admin"}
h, _ = admin.post("/Admin/TestLdap", form, "/Admin/Connections"); j = json.loads(h)
check("LDAP test connection", j["ok"] and h.count("NOT FOUND") == 0, h)
h, _ = admin.post("/Admin/TestLdap", dict(form, testUser="hod", testPassword="hod123"), "/Admin/Connections"); j = json.loads(h)
check("LDAP test sign-in shows roles", j["ok"] and "HOD" in j["message"], h)
h, _ = admin.post("/Admin/TestLdap", dict(form, testUser="hod", testPassword="bad"), "/Admin/Connections"); check("LDAP test sign-in bad password", not json.loads(h)["ok"], h)

smtp = {"smtp[Enabled]": "true", "smtp[Host]": "127.0.0.1", "smtp[Port]": "1025", "smtp[UseStartTls]": "false", "smtp[User]": "", "smtp[Password]": "", "smtp[From]": "events@test.local"}
# (does not save: the SMTP test uses the values on the form, so the real saved settings are left alone)
h, _ = admin.post("/Admin/TestSmtp", {**smtp, "to": "someone@test.local"}, "/Admin/Connections"); check("SMTP test sends", json.loads(h)["ok"], h)
msgs = json.loads(urllib.request.urlopen(MAILPIT + "/api/v1/messages").read())
check("Mailpit received the test email", any("SMTP test" in m["Subject"] for m in msgs["messages"]), msgs.get("total"))

print("\nFAILED:", fails); sys.exit(1 if fails else 0)
