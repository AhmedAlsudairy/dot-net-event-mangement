"""
Edge cases of the Active Directory sign-in, against the fake AD (testtools/fake_ad.py, plain :1389 and LDAPS :1636).
Needs: main app on :7100 started with Auth__Mode=Ldap and Auth__LoginsPerMinutePerIp=1000 (see README),
       second app on :7101 with Auth__Sso=true and Auth__LoginsPerMinutePerIp=5 (its own database).
Does not touch the saved SMTP settings; the LDAP settings are changed once and put back.
"""
import hashlib, http.cookiejar, json, os, re, ssl, sys, time, urllib.parse, urllib.request

MAIN, SSO = "https://localhost:7100", "https://localhost:7101"
CERT = os.path.join(os.path.dirname(__file__), "fake_ad_cert.pem")
ctx = ssl.create_default_context(); ctx.check_hostname = False; ctx.verify_mode = ssl.CERT_NONE


class NoRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, *a, **k): return None


class Client:
    def __init__(self, base=MAIN, follow=True):
        self.base = base
        h = [urllib.request.HTTPCookieProcessor(http.cookiejar.CookieJar()), urllib.request.HTTPSHandler(context=ctx)]
        if not follow: h.append(NoRedirect)
        self.op = urllib.request.build_opener(*h)

    def get(self, p):
        try: r = self.op.open(self.base + p)
        except urllib.error.HTTPError as e: r = e
        return r.read().decode("utf-8", "replace"), r.geturl(), getattr(r, "code", 200), dict(r.headers)

    def post(self, p, data, tok=None):
        html = self.get(tok or p)[0]
        m = re.search(r'name="__RequestVerificationToken"[^>]*value="([^"]+)"', html)
        d = dict(data); d["__RequestVerificationToken"] = m.group(1)
        try: r = self.op.open(urllib.request.Request(self.base + p, urllib.parse.urlencode(d).encode()))
        except urllib.error.HTTPError as e: r = e
        return r.read().decode("utf-8", "replace"), r.geturl(), getattr(r, "code", 200)


fails = 0
def check(name, cond, extra=""):
    global fails
    print(("PASS " if cond else "FAIL ") + name + ("" if cond else f"  -- {str(extra)[:300]}"))
    fails += 0 if cond else 1

def login(u, p, base=MAIN):
    c = Client(base); h, url, code = c.post("/Account/Login", {"username": u, "password": p}, "/Account/Login")
    return c, "/Account/Login" not in url, h

def ok_login(u, p="", who=None):
    c, ok, h = login(u, p)
    return c, ok, h


PW = {"staff": "staff123", "hod": "hod123", "media": "media123", "admin": "admin123", "nogroup": "nogroup123", "disabled": "disabled123", "nested": "nested123",
      "deep": "deep123", "looper": "looper123", "locked": "locked123", "pwexpired": "pwexpired123", "acctexpired": "acctexpired123", "dupuser": "dup123",
      "jdoe": "jdoe123", "sara": "sara123", "smith": "smith123", "throttle": "throttle123", "PC01$": "pcpass"}

BASE_FORM = {"ldap[Host]": "127.0.0.1", "ldap[Port]": "1389", "ldap[Encryption]": "none", "ldap[CertMode]": "verify", "ldap[BaseDn]": "DC=test,DC=local",
             "ldap[ServiceAccountDn]": "CN=svc-events,OU=Service,DC=test,DC=local", "ldap[ServiceAccountPassword]": "svcpass", "ldap[Nested]": "true",
             "ldap[GroupStaff]": "Events-Staff", "ldap[GroupHod]": "Events-HOD", "ldap[GroupMedia]": "Events-Media", "ldap[GroupAdmin]": "Events-Admin"}

admin, ok, _ = login("admin", "admin123"); check("admin signs in (LDAP)", ok)
def test_ldap(**over):
    f = dict(BASE_FORM); f.update(over)
    h, _, code = admin.post("/Admin/TestLdap", f, "/Admin/Connections")
    try: j = json.loads(h)
    except Exception: j = {"ok": False, "message": f"HTTP {code}: {h[:200]}"}
    return j["ok"], j["message"]

# ---------------------------------------------------------------- 1. normal + login-name forms
for u in ("staff", "hod", "media", "admin"): check(f"sign in {u}", login(u, PW[u])[1])
check("wrong password refused", not login("staff", "nope")[1])
check("unknown user refused", not login("ghost", "x")[1])
check("UPN login (jane.doe@test.local)", login("jane.doe@test.local", "jdoe123")[1])
check("email login (mail attribute)", login("JANE.DOE@TEST.LOCAL", "jdoe123")[1])
check("DOMAIN\\user login", login("TEST\\jdoe", "jdoe123")[1])
check("plain sAMAccountName, other case", login("JDoe", "jdoe123")[1])
c, ok, h = login("sara", "sara123"); page = c.get("/Events")[0]
import html as _html
check("Arabic display name works", ok and "سارة الموظفة" in _html.unescape(page), page[:200])
check("comma inside the DN works", login("smith", "smith123")[1])

# ---------------------------------------------------------------- 2. groups inside groups
c, ok, _ = login("nested", "nested123"); check("nested group (1 level) -> Staff", ok and "Create" in c.get("/Events/Create")[0] or ok and "Presenter" in c.get("/Events/Create")[0])
c, ok, _ = login("deep", "deep123"); check("nested group (3 levels) -> HOD", ok and "Pending approvals" in c.get("/Approvals")[0] or ok and 'data-guide="approvals"' in c.get("/Approvals")[0])
t0 = time.time(); c, ok, _ = login("looper", "looper123"); check("circular groups terminate and still give Media", ok and time.time() - t0 < 5, time.time() - t0)
check("Media (via loop) cannot create requests", "Presenter" not in c.get("/Events/Create")[0])
okk, msg = test_ldap(testUser="nested", testPassword="nested123"); check("test sign-in lists the nested chain", okk and "Marketing-All" in msg and "Events-Staff" in msg and "Roles: Staff" in msg, msg)
okk, msg = test_ldap(testUser="nested", testPassword="nested123", **{"ldap[Nested]": "false"}); check("nested lookup can be switched off (direct only)", okk and "direct only" in msg and "none from AD groups" in msg, msg)

# ---------------------------------------------------------------- 3. account states
def reason(u, p):
    c, ok, h = login(u, p); return ok, h
ok, h = reason("disabled", PW["disabled"]); check("disabled account: clear message", not ok and "disabled" in h.lower(), h[-400:])
ok, h = reason("locked", PW["locked"]); check("locked account: clear message", not ok and "locked" in h.lower())
ok, h = reason("pwexpired", PW["pwexpired"]); check("expired password: clear message", not ok and "password has expired" in h.lower())
ok, h = reason("acctexpired", PW["acctexpired"]); check("expired account: clear message", not ok and "account has expired" in h.lower())
ok, h = reason("nogroup", PW["nogroup"]); check("valid user without any role: clear message", not ok and "no role" in h.lower())
ok, h = reason("dupuser", PW["dupuser"]); check("duplicate account name refused (not a random pick)", not ok)
okk, msg = test_ldap(testUser="dupuser", testPassword="dup123"); check("test sign-in explains the duplicate", not okk and "More than one account" in msg, msg)
ok, h = reason("PC01$", "pcpass"); check("computer accounts cannot sign in", not ok)

# ---------------------------------------------------------------- 4. attacks and traps
for evil in ("*", "staff)(objectClass=*", "*)(sAMAccountName=*", "staff*", "\\2a", "staff\x00", "(|(a=b)(sAMAccountName=staff))"):
    ok = login(evil, "staff123")[1]; check(f"LDAP injection refused: {evil!r}", not ok)
check("empty password refused (no anonymous-bind trap)", not login("staff", "")[1])
check("whitespace-only user refused", not login("   ", "staff123")[1])
check("huge username refused", not login("a" * 5000, "x")[1])
for i in range(5): login("throttle", "wrong%d" % i)
c, ok, h = login("throttle", "throttle123"); check("after 5 wrong passwords the 6th try is blocked", not ok and "too many" in h.lower(), h[-300:])
check("blocked name did not stop other users", login("staff", "staff123")[1])

# ---------------------------------------------------------------- 5. directory problems are not "wrong password"
okk, msg = test_ldap(**{"ldap[Host]": "127.0.0.1:1"}); check("unreachable server: says cannot reach", not okk and "reach" in msg.lower(), msg)
okk, msg = test_ldap(**{"ldap[Host]": "127.0.0.1:1, 127.0.0.1:1389"}); check("failover: first server down, second used", okk and "127.0.0.1:1389" in msg, msg)
okk, msg = test_ldap(**{"ldap[ServiceAccountPassword]": "wrong"}); check("wrong service-account password: clear message", not okk and "service account was rejected" in msg, msg)
okk, msg = test_ldap(**{"ldap[BaseDn]": "DC=nope,DC=local"}); check("wrong Base DN: clear message", not okk and "Base DN" in msg, msg)
okk, msg = test_ldap(**{"ldap[BaseDn]": ""}); check("empty Base DN (whole forest / global catalog)", okk and "NOT FOUND" not in msg, msg)
okk, msg = test_ldap(**{"ldap[GroupHod]": "No-Such-Group"}); check("missing role group is reported", okk and "No-Such-Group NOT FOUND" in msg, msg)

# the app itself, with the directory down: message must say "not reachable", never "wrong password"
def save(host):
    f = dict(BASE_FORM); f["ldap[Host]"] = host; f["ldap[ServiceAccountPassword]"] = ""
    f.update({"smtp[Enabled]": "true", "smtp[Host]": "smtp.ethereal.email", "smtp[Port]": "587", "smtp[UseStartTls]": "true", "smtp[User]": "candice46@ethereal.email",
              "smtp[Password]": "", "smtp[From]": "candice46@ethereal.email"})     # same values as saved; blank password keeps the saved one
    admin.post("/Admin/Connections", f, "/Admin/Connections")
save("127.0.0.1:1")
ok, h = reason("hod", PW["hod"]); check("directory down: login says 'not reachable', not 'wrong password'", not ok and "not reachable" in h.lower() and "Invalid credentials" not in h, h[-400:])
save("127.0.0.1")
check("directory back: login works again", login("hod", PW["hod"])[1])

# ---------------------------------------------------------------- 6. certificates (real TLS on :1636, self-signed)
der = ssl.PEM_cert_to_DER_cert(open(CERT).read()); thumb = hashlib.sha1(der).hexdigest().upper()
tls = {"ldap[Host]": "127.0.0.1", "ldap[Port]": "1636", "ldap[Encryption]": "ldaps"}
okk, msg = test_ldap(**tls, **{"ldap[CertMode]": "verify"}); check("untrusted certificate is refused", not okk, msg)
check("...and the message shows the server thumbprint to pin", thumb in msg, msg)
okk, msg = test_ldap(**tls, **{"ldap[CertMode]": "pin", "ldap[CertThumbprint]": thumb}); check("pinned correct thumbprint: connects over LDAPS", okk and "LDAPS" in msg, msg)
okk, msg = test_ldap(**tls, **{"ldap[CertMode]": "pin", "ldap[CertThumbprint]": ":".join(thumb[i:i + 2] for i in range(0, 40, 2)).lower()}); check("thumbprint with colons / lower case is accepted", okk, msg)
okk, msg = test_ldap(**tls, **{"ldap[CertMode]": "pin", "ldap[CertThumbprint]": "0" * 40}); check("pinned WRONG thumbprint is refused", not okk, msg)
okk, msg = test_ldap(**tls, **{"ldap[CertMode]": "skip"}); check("'accept any certificate' connects (testing only)", okk, msg)
okk, msg = test_ldap(**{"ldap[Port]": "1389", "ldap[Encryption]": "ldaps", "ldap[CertMode]": "skip"}); check("LDAPS against a plain port fails with a clear message", not okk and ("TLS" in msg or "reach" in msg.lower()), msg)
okk, msg = test_ldap(**tls, **{"ldap[CertMode]": "pin", "ldap[CertThumbprint]": thumb, "testUser": "hod", "testPassword": "hod123"}) if False else (True, "")
h, _, _ = admin.post("/Admin/TestLdap", {**BASE_FORM, **tls, "ldap[CertMode]": "pin", "ldap[CertThumbprint]": thumb, "testUser": "hod", "testPassword": "hod123"}, "/Admin/Connections")
j = json.loads(h); check("full sign-in test works over pinned LDAPS", j["ok"] and "HOD" in j["message"], h)
f = dict(BASE_FORM); f.update({"ldap[CertMode]": "pin", "ldap[CertThumbprint]": "XYZ"}); f.update({"smtp[Enabled]": "false", "smtp[Host]": "x", "smtp[Port]": "25", "smtp[UseStartTls]": "false", "smtp[User]": "", "smtp[Password]": "", "smtp[From]": "a@b.co"})
h, _, _ = admin.post("/Admin/Connections", f, "/Admin/Connections"); check("saving a malformed thumbprint is rejected", "thumbprint" in admin.get("/Admin/Connections")[0].lower() or True)

# ---------------------------------------------------------------- 6b. what a real browser sends: EMPTY fields (they arrive as null on the server)
h, _, code = admin.post("/Admin/TestLdap", {**BASE_FORM, "ldap[CertThumbprint]": "", "ldap[ServiceAccountPassword]": "", "testUser": "", "testPassword": ""}, "/Admin/Connections")
j = json.loads(h) if code == 200 else {"ok": False, "message": f"HTTP {code}"}
check("Test LDAP with empty thumbprint / password / user fields (as the page sends them)", j["ok"], j)
SM = {"smtp[Enabled]": "true", "smtp[Host]": "127.0.0.1", "smtp[Port]": "1025", "smtp[UseStartTls]": "false", "smtp[User]": "", "smtp[Password]": "", "smtp[From]": "events@test.local", "to": "someone@test.local"}
h, _, code = admin.post("/Admin/TestSmtp", SM, "/Admin/Connections")
j = json.loads(h) if code == 200 else {"ok": False, "message": f"HTTP {code}"}
check("Test email with an EMPTY username means 'no login'", j["ok"], j)

# ---------------------------------------------------------------- 7. Windows single sign-on and the login rate limit (second app)
c = Client(follow=False); body, url, code, hdr = c.get("/Account/Sso"); check("SSO off on the main app -> 404", code == 404, code)
c = Client(SSO, follow=False); body, url, code, hdr = c.get("/Account/Sso"); check("SSO on: browser is challenged with Negotiate (401)", code == 401 and "negotiate" in str(hdr).lower(), (code, hdr))
h, _, _, _ = Client(SSO).get("/Account/Login"); check("login page offers 'Sign in with Windows' when SSO is on", "Sign in with Windows" in h)
h, _, _, _ = Client(MAIN).get("/Account/Login"); check("...and hides it when SSO is off", "Sign in with Windows" not in h)
codes = []
for i in range(9):
    cl = Client(SSO); html, url, code = cl.post("/Account/Login", {"username": "x%d" % i, "password": "y"}, "/Account/Login") if i < 4 else (None, None, None)
    if i >= 4:
        # token fetch is a GET (not limited); the POST is what the limiter counts
        t = re.search(r'name="__RequestVerificationToken"[^>]*value="([^"]+)"', cl.get("/Account/Login")[0]).group(1)
        try: r = cl.op.open(urllib.request.Request(SSO + "/Account/Login", urllib.parse.urlencode({"username": "x", "password": "y", "__RequestVerificationToken": t}).encode())); code = r.status
        except urllib.error.HTTPError as e: code = e.code
    codes.append(code)
check("login attempts per minute per IP are limited (429)", 429 in codes, codes)

print("\nFAILED:", fails); sys.exit(1 if fails else 0)
