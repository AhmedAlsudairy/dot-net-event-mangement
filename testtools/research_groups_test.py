"""Checks "Fetch groups from LDAP", the Research role and the research upload flow against the fake AD and Mailpit.
Start (see README): fake AD on :1390, Mailpit on :1025/:8025, and the app in LDAP mode on :7102 with its own database. Usage: python research_groups_test.py"""
import http.cookiejar, json, re, ssl, sys, urllib.parse, urllib.request, uuid

BASE = "https://localhost:7102"; MAILPIT = "http://127.0.0.1:8025"; AD_PORT = "1390"
ctx = ssl.create_default_context(); ctx.check_hostname = False; ctx.verify_mode = ssl.CERT_NONE

class Client:
    def __init__(self):
        self.op = urllib.request.build_opener(urllib.request.HTTPCookieProcessor(http.cookiejar.CookieJar()), urllib.request.HTTPSHandler(context=ctx))
    def _open(self, req):
        try: r = self.op.open(req)
        except urllib.error.HTTPError as e: r = e
        return r.read(), r.geturl(), r.status if hasattr(r, "status") else r.code, r.headers
    def get(self, p):
        b, u, s, h = self._open(BASE + p); return b.decode("utf-8", "replace"), u
    def raw(self, p):
        b, u, s, h = self._open(BASE + p); return b, s, h
    def token(self, page):
        html, _ = self.get(page)
        return re.search(r'name="__RequestVerificationToken"[^>]*value="([^"]+)"', html).group(1)
    def post(self, p, data, tok=None):
        d = dict(data); d["__RequestVerificationToken"] = self.token(tok or p)
        b, u, s, h = self._open(urllib.request.Request(BASE + p, urllib.parse.urlencode(d).encode()))
        return b.decode("utf-8", "replace"), u
    def upload(self, p, fields, filename, content, tok):
        boundary = uuid.uuid4().hex
        parts = []
        for k, v in dict(fields, __RequestVerificationToken=self.token(tok)).items():
            parts.append(f'--{boundary}\r\nContent-Disposition: form-data; name="{k}"\r\n\r\n{v}\r\n'.encode())
        parts.append(f'--{boundary}\r\nContent-Disposition: form-data; name="file"; filename="{filename}"\r\nContent-Type: application/octet-stream\r\n\r\n'.encode() + content + b"\r\n")
        parts.append(f"--{boundary}--\r\n".encode())
        req = urllib.request.Request(BASE + p, b"".join(parts), {"Content-Type": f"multipart/form-data; boundary={boundary}"})
        b, u, s, h = self._open(req); return b.decode("utf-8", "replace"), u

fails = 0
def check(n, c, x=""):
    global fails
    print(("PASS " if c else "FAIL ") + n + ("" if c else " -- " + str(x)[:300]))
    fails += 0 if c else 1

def login(u, p):
    c = Client(); h, url = c.post("/Account/Login", {"username": u, "password": p}, "/Account/Login")
    return c, "/Account/Login" not in url, h, url

# ---- sign-in with the new group
admin, ok, _, _ = login("admin", "admin123"); check("admin signs in", ok)
res, ok, h, url = login("research", "research123")
check("research user signs in (group Research-InCharge)", ok)
check("research user lands on the archive", "/Research" in url, url)
check("research menu shows archive", "bi-journal-text" in h or "Research" in h)

# ---- fetch groups
form = {"ldap[Host]": "127.0.0.1", "ldap[Port]": AD_PORT, "ldap[Encryption]": "none", "ldap[BaseDn]": "DC=test,DC=local", "ldap[CertMode]": "verify",
        "ldap[ServiceAccountDn]": "CN=svc-events,OU=Service,DC=test,DC=local", "ldap[ServiceAccountPassword]": "svcpass",
        "ldap[GroupStaff]": "Events-Staff", "ldap[GroupHod]": "Events-HOD", "ldap[GroupMedia]": "Events-Media", "ldap[GroupAdmin]": "Events-Admin", "ldap[GroupResearch]": "Research-InCharge"}
h, _ = admin.post("/Admin/FetchGroups", form, "/Admin/Connections"); j = json.loads(h)
check("fetch groups ok", j["ok"], h)
for g in ["Events-Staff", "Events-HOD", "Events-Media", "Events-Admin", "Research-InCharge", "Marketing-All", "Deep-3", "Loop-A"]:
    check(f"group listed: {g}", g in j["groups"], j["groups"])
check("groups are sorted", j["groups"] == sorted(j["groups"], key=str.lower))
check("message says how many", re.search(r"Found \d+ group", j["message"]) is not None, j["message"])
h, _ = admin.post("/Admin/FetchGroups", dict(form, **{"ldap[ServiceAccountPassword]": "wrong"}), "/Admin/Connections"); j2 = json.loads(h)
check("fetch groups with a wrong service password fails clearly", not j2["ok"] and j2["groups"] == [], h)
h, _ = admin.post("/Admin/FetchGroups", dict(form, **{"ldap[Host]": "127.0.0.1", "ldap[Port]": "1"}), "/Admin/Connections"); j3 = json.loads(h)
check("fetch groups with an unreachable server fails clearly", not j3["ok"], h)
st, _, _, _ = login("staff", "staff123")
h, _ = st.get("/Admin/Connections"); check("staff cannot open the connections page", "Fetch groups" not in h)
page, _ = admin.get("/Admin/Connections"); check("connections page has the fetch button and research box", "Fetch groups from LDAP" in page and "ldap[GroupResearch]" in page)

# ---- research upload flow (staff uploads, research in-charge sees it, nobody approves)
PDF = b"%PDF-1.4\n1 0 obj<<>>endobj\ntrailer<<>>\n%%EOF\n"
h, url = st.upload("/Research/Upload", {"Title": "Water quality study", "DocType": "research", "Authors": "S. Staff", "Summary": "A short summary"}, "study.pdf", PDF, "/Research/Upload")
check("staff upload accepted", "/Research" in url and "/Upload" not in url and "Water quality study" in h, url)
h, url = st.upload("/Research/Upload", {"Title": "Fake pdf", "DocType": "research"}, "fake.pdf", b"not a pdf at all", "/Research/Upload")
check("fake PDF content rejected", "/Upload" in url and "does not match" in h, h[-200:])
h, url = st.upload("/Research/Upload", {"Title": "Script", "DocType": "research"}, "run.exe", b"MZ....", "/Research/Upload")
check("exe rejected", "Only PDF" in h, h[-200:])
h, url = st.upload("/Research/Upload", {"Title": "Proposal", "DocType": "proposal"}, "prop.docx", b"PK\x03\x04docx", "/Research/Upload")
check("docx proposal accepted", "Proposal" in h and "/Upload" not in url, url)

rc, _, _, _ = login("research", "research123")
h, _ = rc.get("/Research")
check("research in-charge sees both uploads in the archive", "Water quality study" in h and "prop.docx" in h)
check("archive shows uploader and sent status", "Sara Staff" in h and ("Sent" in h))
body, status, hdr = rc.raw("/Research/Download/1")
check("research in-charge downloads the PDF", status == 200 and body.startswith(b"%PDF"), status)
h, _ = st.get("/Research"); check("staff sees own uploads", "Water quality study" in h)
other, _, _, _ = login("hod", "hod123")
h, _ = other.get("/Research"); check("hod sees nothing from others", "Water quality study" not in h)
body, status, _ = other.raw("/Research/Download/1"); check("hod cannot download someone else's file", status == 404, status)
h, _ = rc.get("/Research/Upload"); check("research in-charge alone cannot upload (staff only)", "Upload and send" not in h)
h, _ = rc.get("/Events"); check("no approve/reject on research", "Approve" not in h)

# ---- email reached the research in-charge with the attachment
msgs = json.loads(urllib.request.urlopen(MAILPIT + "/api/v1/messages").read())["messages"]
m = [x for x in msgs if "Water quality study" in x["Subject"] and "recorded" not in x["Subject"]]
check("email sent for the upload", len(m) >= 1, [x["Subject"] for x in msgs][:5])
if m:
    full = json.loads(urllib.request.urlopen(MAILPIT + "/api/v1/message/" + m[0]["ID"]).read())
    check("email has the PDF attached with its original name", any(a["FileName"] == "study.pdf" for a in full.get("Attachments", [])), full.get("Attachments"))
check("uploader got a receipt", any("Research recorded: Water quality study" in x["Subject"] for x in msgs))

# ---- settings: research in-charge email
page, _ = admin.get("/Admin/Settings"); check("settings has the Research tab", "ResearchEmail" in page)
h, _ = admin.post("/Admin/Settings", {"values[ResearchEmail]": "in.charge@test.local"}, "/Admin/Settings")
st2, _, _, _ = login("staff", "staff123")
st2.upload("/Research/Upload", {"Title": "Second study", "DocType": "research"}, "second.pdf", PDF, "/Research/Upload")
msgs = json.loads(urllib.request.urlopen(MAILPIT + "/api/v1/messages").read())["messages"]
m = [x for x in msgs if x["Subject"].endswith("Second study") and "Research paper" in x["Subject"]]
check("upload goes to the configured research email", m and any("in.charge@test.local" in t["Address"] for t in m[0]["To"]), m[:1])

print("\nFAILED:", fails); sys.exit(1 if fails else 0)
