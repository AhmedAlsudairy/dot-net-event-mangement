# Test tools (local testing only - never use in production)

## Mailpit (free SMTP catcher + web inbox)
    testtools\mailpit\mailpit.exe --smtp 127.0.0.1:1025 --listen 127.0.0.1:8025
- SMTP: host `127.0.0.1`, port `1025`, STARTTLS off, **no username** - inbox: http://127.0.0.1:8025
- Other free options: Ethereal (https://ethereal.email, hosted), smtp4dev (Windows app)

## Fake Active Directory (plain LDAP :1389 and LDAPS :1636 with a self-signed certificate)
    openssl req -x509 -newkey rsa:2048 -nodes -keyout testtools\fake_ad_key.pem -out testtools\fake_ad_cert.pem -days 30 -subj "/CN=fake-ad.test.local"
    python testtools\fake_ad.py 1389 --tls testtools\fake_ad_cert.pem testtools\fake_ad_key.pem 1636
Settings: host `127.0.0.1`, port `1389` (LDAPS `1636`), Base DN `DC=test,DC=local`, service account `CN=svc-events,OU=Service,DC=test,DC=local` / `svcpass`.

| user (any of: name, name@test.local, TEST\name) | password | what it tests |
|---|---|---|
| staff / hod / media / admin | staff123 / hod123 / media123 / admin123 | the four roles |
| research | research123 | the Research in-charge role (AD group `Research-InCharge`); lands on the research archive |
| nogroup | nogroup123 | valid AD user without a role -> "no role" message; give a role on the Roles page |
| disabled | disabled123 | disabled account |
| locked / pwexpired / acctexpired | locked123 / pwexpired123 / acctexpired123 | locked out / password expired / account expired (clear messages) |
| nested | nested123 | Staff only through a group inside a group |
| deep | deep123 | HOD through 3 levels of groups |
| looper | looper123 | Media through a circular group chain |
| dupuser | dup123 | two accounts with the same name -> refused |
| jdoe | jdoe123 | also signs in as jane.doe@test.local (UPN / mail) |
| sara | sara123 | Arabic display name and department |
| smith | smith123 | a comma inside the DN |
| throttle | throttle123 | used to test the 5-wrong-passwords block |
| PC01$ | pcpass | computer account -> refused |

## Run the app against them
    $env:ASPNETCORE_ENVIRONMENT='Development'
    $env:Auth__Mode='Ldap'; $env:Ldap__Host='127.0.0.1'; $env:Ldap__Port='1389'; $env:Ldap__UseSsl='false'
    $env:Ldap__BaseDn='DC=test,DC=local'; $env:Ldap__ServiceAccountDn='CN=svc-events,OU=Service,DC=test,DC=local'; $env:Ldap__ServiceAccountPassword='svcpass'
    cd src\GuestEvents; dotnet run --urls https://localhost:7100

## Tests
- `python smoke_test.py` - whole app, dev mode, fresh database (60+ checks).
- `python testtools\ldap_smtp_test.py` - basic LDAP sign-in + email (main app in LDAP mode).
- `python testtools\research_groups_test.py` - "Fetch groups from LDAP", the Research role and the research upload flow (36 checks).
  Start the fake AD on :1390 (`python testtools\fake_ad.py 1390`), Mailpit on :1025, and a separate app on :7102 in LDAP mode with its own database:
  `Ldap__Port=1390`, `ConnectionStrings__Default='Data Source=App_Data/research-test.db'`, `Smtp__Enabled=true`, `Smtp__Host=127.0.0.1`, `Smtp__Port=1025`, `Smtp__UseStartTls=false`, `Auth__LoginsPerMinutePerIp=1000`.
  Delete `App_Data/research` test files and the test database afterwards.
- `python testtools\ldap_edge_test.py` - AD edge cases (63 checks). Needs the main app with `Auth__LoginsPerMinutePerIp=1000`
  and a SECOND app on :7101 with `Auth__Sso=true`, `Auth__LoginsPerMinutePerIp=5` and `ConnectionStrings__Default='Data Source=App_Data/sso-test.db'`
  (it checks the Windows single sign-on challenge and the login rate limit).
- `python testtools\screenshots.py <folder>` - screenshots of the main pages (dev mode).
