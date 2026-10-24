"""
Fake Active Directory for TESTING ONLY (no real security). Speaks enough LDAP for the app:
simple bind, search (and/or/not/equality/present/substring filters, base/one/sub scope ignored = subtree), unbind.
It reproduces the awkward situations of a real AD: nested and circular groups, UPN/mail logins, disabled / locked /
expired-password / expired accounts, duplicate accounts, computer accounts, a comma inside a DN, Arabic names,
"no such base DN", and optional TLS (self-signed certificate) for LDAPS tests.

Run:  python fake_ad.py [port] [--tls cert.pem key.pem tlsport]
      python fake_ad.py                 -> plain LDAP on 127.0.0.1:1389
      python fake_ad.py 1389 --tls c.pem k.pem 1636   -> also LDAPS on 127.0.0.1:1636

App settings: host 127.0.0.1, port 1389 (LDAPS 1636), base DN DC=test,DC=local,
service account CN=svc-events,OU=Service,DC=test,DC=local / svcpass. See testtools/README.md for the user table.
"""
import socketserver, ssl, sys, threading

BASE = "DC=test,DC=local"


def gdn(name): return f"CN={name},OU=Groups,{BASE}"


# group name -> list of parent group names (groups inside groups)
GROUP_PARENTS = {
    "Events-Staff": [], "Events-HOD": [], "Events-Media": [], "Events-Admin": [], "Research-InCharge": [],
    "Marketing-All": ["Events-Staff"],                       # nested one level
    "Deep-3": ["Deep-2"], "Deep-2": ["Deep-1"], "Deep-1": ["Events-HOD"],   # nested three levels
    "Loop-A": ["Loop-B"], "Loop-B": ["Loop-A", "Events-Media"],              # circular + one real parent
}
FILETIME_2019 = 132000000000000000   # an account that expired in 2019


def user(sam, display, mail, dept, pw, groups, uac=512, upn=None, ou="Users", dn=None, bind_error=None, expires=None, classes=None):
    attrs = {
        "objectClass": classes or ["top", "person", "organizationalPerson", "user"], "sAMAccountName": [sam], "displayName": [display], "cn": [display],
        "mail": [mail], "department": [dept], "memberOf": [gdn(g) for g in groups], "userAccountControl": [str(uac)],
        "userPrincipalName": [upn or f"{sam}@test.local"],
    }
    if expires: attrs["accountExpires"] = [str(expires)]
    return {"dn": dn or f"CN={display},OU={ou},{BASE}", "pw": pw, "bind_error": bind_error, "attrs": attrs}


def data775(): return "80090308: LdapErr: DSID-0C09044E, comment: AcceptSecurityContext error, data 775, v4563"
def data532(): return "80090308: LdapErr: DSID-0C09044E, comment: AcceptSecurityContext error, data 532, v4563"


ENTRIES = [
    {"dn": f"CN=svc-events,OU=Service,{BASE}", "pw": "svcpass", "bind_error": None,
     "attrs": {"objectClass": ["top", "person", "user"], "sAMAccountName": ["svc-events"], "cn": ["svc-events"], "userAccountControl": ["512"]}},
    user("staff", "Sara Staff", "staff@test.local", "Marketing", "staff123", ["Events-Staff"]),
    user("hod", "Hamad HOD", "hod@test.local", "Management", "hod123", ["Events-HOD"]),
    user("media", "Mona Media", "media@test.local", "Media", "media123", ["Events-Media"]),
    user("admin", "Adam Admin", "admin@test.local", "IT", "admin123", ["Events-Admin", "Events-Staff"]),
    user("share1", "Share One", "shared@test.local", "A", "share123", ["Events-Staff"]),    # two people with the same email:
    user("share2", "Share Two", "shared@test.local", "B", "share123", ["Events-Staff"]),    # cannot sign in by email, only by username
    user("research", "Rana Research", "research@test.local", "Research", "research123", ["Research-InCharge"]),
    user("nogroup", "Nora NoGroup", "nogroup@test.local", "Finance", "nogroup123", []),
    user("disabled", "Dana Disabled", "disabled@test.local", "HR", "disabled123", ["Events-Staff"], uac=514),
    # --- edge cases
    user("nested", "Nadia Nested", "nested@test.local", "Marketing", "nested123", ["Marketing-All"]),        # Staff only through a group
    user("deep", "Deema Deep", "deep@test.local", "Management", "deep123", ["Deep-3"]),                        # HOD through 3 levels
    user("looper", "Layla Looper", "looper@test.local", "Media", "looper123", ["Loop-A"]),                    # Media through a circular chain
    user("locked", "Lina Locked", "locked@test.local", "HR", "locked123", ["Events-Staff"], bind_error=data775()),
    user("pwexpired", "Pam Expired", "pwexpired@test.local", "HR", "pwexpired123", ["Events-Staff"], bind_error=data532()),
    user("acctexpired", "Alex Expired", "acctexpired@test.local", "HR", "acctexpired123", ["Events-Staff"], expires=FILETIME_2019),
    user("dupuser", "Dup One", "dup1@test.local", "A", "dup123", ["Events-Staff"], ou="DeptA"),
    user("dupuser", "Dup Two", "dup2@test.local", "B", "dup123", ["Events-Staff"], ou="DeptB"),
    user("jdoe", "Jane Doe", "jane.doe@test.local", "Sales", "jdoe123", ["Events-Staff"], upn="jane.doe@test.local"),
    user("sara", "سارة الموظفة", "sara@test.local", "التسويق", "sara123", ["Events-Staff"]),                  # Arabic name and department
    user("smith", "Smith, John", "smith@test.local", "Legal", "smith123", ["Events-Staff"], dn=f"CN=Smith\\, John,OU=Users,{BASE}"),  # comma inside the DN
    user("throttle", "Theo Throttle", "throttle@test.local", "IT", "throttle123", ["Events-Staff"]),
    user("PC01$", "PC01", "pc01@test.local", "IT", "pcpass", ["Events-Staff"], classes=["top", "person", "organizationalPerson", "user", "computer"]),
] + [{"dn": gdn(g), "pw": None, "bind_error": None, "attrs": {"objectClass": ["top", "group"], "cn": [g], "memberOf": [gdn(p) for p in parents]}}
     for g, parents in GROUP_PARENTS.items()]


# ---------------------------------------------------------------- BER helpers
def enc_len(n):
    if n < 128: return bytes([n])
    b = n.to_bytes((n.bit_length() + 7) // 8, "big")
    return bytes([0x80 | len(b)]) + b

def tlv(tag, value): return bytes([tag]) + enc_len(len(value)) + value
def enc_int(n, tag=0x02): return tlv(tag, n.to_bytes(max(1, (n.bit_length() + 8) // 8), "big", signed=True))
def octet(s): return tlv(0x04, s.encode("utf-8") if isinstance(s, str) else s)

def read_tlv(buf, pos):
    tag = buf[pos]; pos += 1
    l = buf[pos]; pos += 1
    if l & 0x80:
        n = l & 0x7F; l = int.from_bytes(buf[pos:pos + n], "big"); pos += n
    return tag, buf[pos:pos + l], pos + l

def children(value):
    out, pos = [], 0
    while pos < len(value):
        t, v, pos = read_tlv(value, pos); out.append((t, v))
    return out


# ---------------------------------------------------------------- filters
def get_vals(entry, attr):
    for k, v in entry["attrs"].items():
        if k.lower() == attr.lower(): return v
    return []

def match(entry, tag, val):
    if tag == 0xA0: return all(match(entry, t, v) for t, v in children(val))
    if tag == 0xA1: return any(match(entry, t, v) for t, v in children(val))
    if tag == 0xA2:
        t, v, _ = read_tlv(val, 0); return not match(entry, t, v)
    if tag == 0xA3:
        (_, a), (_, v) = children(val)[:2]
        return v.decode("utf-8").lower() in [x.lower() for x in get_vals(entry, a.decode())]
    if tag == 0x87: return len(get_vals(entry, val.decode())) > 0
    if tag == 0xA4:
        parts = children(val); attr = parts[0][1].decode()
        for x in get_vals(entry, attr):
            x = x.lower(); ok = True
            for t, v in children(parts[1][1]):
                v = v.decode("utf-8").lower()
                ok &= x.startswith(v) if t == 0x80 else x.endswith(v) if t == 0x82 else v in x
            if ok: return True
        return False
    return False


# ---------------------------------------------------------------- server
class Handler(socketserver.BaseRequestHandler):
    def send(self, msg_id, op): self.request.sendall(tlv(0x30, enc_int(msg_id) + op))
    def result(self, tag, code, msg=""): return tlv(tag, enc_int(code, 0x0A) + octet("") + octet(msg))

    def handle(self):
        buf = b""
        while True:
            try: chunk = self.request.recv(65536)
            except OSError: return        # includes TLS handshake failures
            if not chunk: return
            buf += chunk
            while True:
                if len(buf) < 2: break
                try: _, _, end = read_tlv(buf, 0)
                except IndexError: break
                if end > len(buf): break
                msg, buf = buf[:end], buf[end:]
                if not self.process(msg): return

    def process(self, msg):
        _, body, _ = read_tlv(msg, 0)
        kids = children(body)
        msg_id = int.from_bytes(kids[0][1], "big", signed=True)
        op_tag, op = kids[1]
        if op_tag == 0x42: return False
        if op_tag == 0x60:  # bind
            c = children(op); name = c[1][1].decode("utf-8"); pw = c[2][1].decode("utf-8")
            code, text = 49, "invalid credentials"
            if name == "" and pw == "": code, text = 0, ""          # anonymous bind (a real AD allows it too - the app must never rely on an empty password)
            else:
                for e in ENTRIES:
                    if e["pw"] is not None and e["dn"].lower() == name.lower():
                        if e["pw"] == pw: code, text = (49, e["bind_error"]) if e["bind_error"] else (0, "")
            print(f"bind {name!r} -> {code} {text if code else ''}", flush=True)
            self.send(msg_id, self.result(0x61, code, text)); return True
        if op_tag == 0x63:  # search
            c = children(op); base = c[0][1].decode("utf-8"); ftag, fval = c[6]
            want = [a.decode().lower() for _, a in children(c[7][1])]
            if base and not any(e["dn"].lower().endswith(base.lower()) for e in ENTRIES):
                print(f"search base={base!r} -> noSuchObject", flush=True)
                self.send(msg_id, self.result(0x65, 32, "no such object")); return True
            n = 0
            for e in ENTRIES:
                if base and not e["dn"].lower().endswith(base.lower()): continue
                if not match(e, ftag, fval): continue
                attrs = b""
                for k, vals in e["attrs"].items():
                    if want and k.lower() not in want and "*" not in want: continue
                    attrs += tlv(0x30, octet(k) + tlv(0x31, b"".join(octet(v) for v in vals)))
                self.send(msg_id, tlv(0x64, octet(e["dn"]) + tlv(0x30, attrs))); n += 1
            print(f"search base={base!r} -> {n} entries", flush=True)
            self.send(msg_id, self.result(0x65, 0)); return True
        self.send(msg_id, self.result(op_tag + 1, 53, "unsupported")); return True


class Server(socketserver.ThreadingTCPServer):
    allow_reuse_address = True; daemon_threads = True


class TlsServer(Server):
    ctx = None
    def get_request(self):
        sock, addr = self.socket.accept()
        # handshake happens lazily on the first read inside the handler thread, so a failing client cannot block the accept loop
        return self.ctx.wrap_socket(sock, server_side=True, do_handshake_on_connect=False), addr


if __name__ == "__main__":
    args = sys.argv[1:]
    port = int(args[0]) if args and args[0].isdigit() else 1389
    if "--tls" in args:
        i = args.index("--tls"); cert, key, tport = args[i + 1], args[i + 2], int(args[i + 3])
        ctx = ssl.SSLContext(ssl.PROTOCOL_TLS_SERVER); ctx.load_cert_chain(cert, key)
        TlsServer.ctx = ctx
        t = TlsServer(("127.0.0.1", tport), Handler); threading.Thread(target=t.serve_forever, daemon=True).start()
        print(f"Fake AD LDAPS on 127.0.0.1:{tport} (self-signed certificate)", flush=True)
    print(f"Fake AD on 127.0.0.1:{port} (plain LDAP, TEST ONLY). Base DN {BASE}", flush=True)
    Server(("127.0.0.1", port), Handler).serve_forever()
