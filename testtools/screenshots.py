"""Takes screenshots of the main pages (dev mode on https://localhost:7100). Usage: python screenshots.py <out_dir>"""
import os, sqlite3, sys
from playwright.sync_api import sync_playwright

OUT = sys.argv[1]; os.makedirs(OUT, exist_ok=True)
BASE = "https://localhost:7100"
DB = os.path.join(os.path.dirname(__file__), "..", "src", "GuestEvents", "App_Data", "events.db")

def login(b, user, pw, **kw):
    ctx = b.new_context(ignore_https_errors=True, viewport={"width": 1280, "height": 800}, **kw)
    pg = ctx.new_page(); pg.goto(BASE + "/Account/Login")
    pg.fill("input[name=username]", user); pg.fill("input[name=password]", pw)
    pg.click("button.btn-lg"); pg.wait_for_load_state("networkidle")
    return ctx, pg

def shot(pg, name, full=True):
    pg.wait_for_load_state("networkidle"); pg.wait_for_timeout(300)
    pg.screenshot(path=os.path.join(OUT, name + ".png"), full_page=full); print("shot", name)

with sync_playwright() as p:
    b = p.chromium.launch(channel="msedge")
    # login page (anonymous)
    ctx = b.new_context(ignore_https_errors=True, viewport={"width": 1280, "height": 800}); pg = ctx.new_page()
    pg.goto(BASE + "/Account/Login"); shot(pg, "01-login"); ctx.close()

    # staff: create a second event (submitted) through the real form
    ctx, pg = login(b, "staff", "dev")
    pg.goto(BASE + "/Events/Create"); shot(pg, "02-new-event")
    pg.fill("input[name=Name]", "Leadership Workshop"); pg.fill("input[name=Presenter]", "Dr. Layla Hassan"); pg.fill("input[name=CompanyName]", "Gulf Training Co.")
    pg.fill("input[name=ContactName]", "Omar Saleh"); pg.fill("input[name=ContactEmail]", "omar@gulftraining.example"); pg.fill("input[name=Venue]", "Main Hall")
    pg.fill("textarea[name=ContentEn]", "Practical leadership skills for team leads."); pg.fill("textarea[name=ContentAr]", "مهارات قيادية عملية لقادة الفرق.")
    pg.click("button[value=submit]"); pg.wait_for_load_state("networkidle"); shot(pg, "03-event-submitted")
    pg.goto(BASE + "/Events"); shot(pg, "04-events-list")
    pg.goto(BASE + "/Events/Details/1"); shot(pg, "05-event-approved")
    pg.goto(BASE + "/Food/Create?eventId=1"); shot(pg, "06-food-request")
    # arabic
    pg.goto(BASE + "/Events"); pg.click("button[name=lang]"); pg.wait_for_load_state("networkidle"); shot(pg, "07-events-list-arabic")
    pg.goto(BASE + "/Events/Details/1"); shot(pg, "08-event-details-arabic")
    ctx.close()

    ctx, pg = login(b, "hod", "dev"); pg.goto(BASE + "/Approvals"); shot(pg, "09-approvals"); ctx.close()

    ctx, pg = login(b, "admin", "dev")
    for path, name in [("/Admin/Settings", "10-settings"), ("/Admin/Connections", "11-connections"), ("/Admin/RolesPage", "12-roles"),
                       ("/Reports", "13-reports"), ("/Reports/Event/1", "14-report-event"), ("/Food", "15-food-admin"), ("/Admin/Audit", "16-audit")]:
        pg.goto(BASE + path); shot(pg, name)
    ctx.close()

    # guidance
    ctx, pg = login(b, "admin", "dev")
    pg.goto(BASE + "/Events"); shot(pg, "30-admin-home-checklist")
    pg.goto(BASE + "/Help"); shot(pg, "31-help-admin")
    pg.goto(BASE + "/Help?tab=overview"); shot(pg, "32-help-overview")
    pg.goto(BASE + "/Help?tab=faq"); pg.click("#faqAcc .accordion-button >> nth=0"); shot(pg, "33-help-faq")
    pg.goto(BASE + "/Admin/Settings"); shot(pg, "34-settings-guide")
    pg.goto(BASE + "/Events/Poster/1"); shot(pg, "35-poster")
    ctx.close()
    ctx, pg = login(b, "staff", "dev")
    pg.goto(BASE + "/Events/Details/1"); shot(pg, "36-details-nextstep")
    pg.goto(BASE + "/Events/Create"); shot(pg, "37-create-guide")
    ctx.close()

    # public pages on a phone
    token = sqlite3.connect(DB).execute("select Token from QRCodes where EventRequestId=1").fetchone()[0]
    ctx = b.new_context(ignore_https_errors=True, viewport={"width": 390, "height": 800}, device_scale_factor=2, is_mobile=True, has_touch=True)
    pg = ctx.new_page()
    pg.goto(f"{BASE}/p/{token}"); shot(pg, "20-public-checkin")
    pg.fill("input[name=fullName]", "Khalid Ali"); pg.fill("input[name=organization]", "Finance"); pg.click("button.btn-lg"); shot(pg, "21-public-checked-in")
    pg.goto(f"{BASE}/p/{token}/evaluate"); shot(pg, "22-public-evaluate")
    for n in ("overall", "presenter", "content", "venueOrg"): pg.click(f"label[for={n}-5]")
    pg.click("button.btn-lg"); shot(pg, "23-public-thanks")
    pg.goto(f"{BASE}/p/{token}"); shot(pg, "24-public-already")
    ctx.close()
    b.close()
