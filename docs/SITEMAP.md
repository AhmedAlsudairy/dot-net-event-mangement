# Sitemap

Five work areas (events, visits, research, admin and the KPI dashboard) sit behind one sign-in, plus a public QR area on its own host. The top bar shows only what the signed-in person may use.

```mermaid
flowchart TD
    subgraph pub["Public host (no sign-in)"]
        P1["/p/{token}<br/>check-in"] --> P2["/p/{token}/evaluate<br/>anonymous evaluation"]
    end

    L["Sign in<br/>/Account/Login<br/>(email + password)"] --> F["Forgot password<br/>/Account/Forgot"]
    L --> H["Help<br/>/Help"]
    L --> HM["Home (by role)<br/>/ - waiting for you, quick actions,<br/>recent requests, upcoming events, KPI snapshot"]
    HM --> EV
    L --> XV
    L --> RS
    L --> AD

    subgraph EV["Events"]
        E1["My events<br/>/Events"] --> E2["Event details<br/>/Events/Details/{id}"]
        E1 --> E3["New event request<br/>/Events/Create"]
        E2 --> E4["Edit and resubmit<br/>/Events/Edit/{id}"]
        E2 --> E5["Food request<br/>/Food/Create?eventId="]
        E2 --> E6["Poster / venue screen<br/>/Events/Poster, /Events/Screen"]
        E2 --> E7["Download image, QR<br/>/Events/DownloadImage, DownloadQr"]
        E8["Pending approvals (people of the current step)<br/>/Approvals"]
    end

    subgraph XV["External visits (bus)"]
        X1["Visit list<br/>/Visits"] --> X2["Visit details<br/>/Visits/Details/{id}"]
        X1 --> X3["New external visit<br/>/Visits/Create"]
        X2 --> X4["Edit and resubmit<br/>/Visits/Edit/{id}"]
        X2 --> X5["Approve or reject<br/>(approvers)"]
        X2 --> X6["Confirm bus<br/>(bus coordinators)"]
    end

    subgraph RS["Research"]
        R1["Research archive / my uploads<br/>/Research"] --> R2["Upload research<br/>/Research/Upload"]
        R1 --> R3["Download file<br/>/Research/Download/{id}"]
    end

    subgraph AD["Admin only"]
        A1["Reports<br/>/Reports"] --> A2["Event report<br/>/Reports/Event/{id}<br/>Excel exports"]
        A3["Food requests<br/>/Food"]
        A4["Settings (9 tabs)<br/>/Admin/Settings"]
        A5["Roles<br/>/Admin/RolesPage"]
        A6["LDAP and email<br/>/Admin/Connections"]
        A7["Audit log<br/>/Admin/Audit"]
        A8["Notifications log<br/>/Admin/Notifications"]
    end

    E7 -. "QR link on an approved event" .-> P1
```

## Who sees what

| Menu item | Staff | HOD / Dean | Media | Research in-charge | Admin |
|---|---|---|---|---|---|
| My events | own | all | approved | - | all |
| New request (event, visit, research) | yes | - | - | - | yes |
| External visits | own | approvers see all submitted | - | - | all |
| Research | own uploads | - | - | archive (all uploads) | archive |
| Pending approvals | only if named or by role in an approval step | yes (default steps) | - | - | only if named in a step |
| Reports, Food requests, Admin menu | - | - | - | - | yes |
| Help | yes | yes | yes | yes | yes |

Visit approvers and bus coordinators are chosen in **Settings > External visits (bus)**, so any person listed there also sees every submitted visit, whatever their role.

## Routes

### Account (anonymous)
| Route | Purpose |
|---|---|
| `/Account/Login` | Sign in with the company email (or username, `DOMAIN\user`, UPN) and password |
| `/Account/Forgot` | Forgot password: reset button (URL from Settings) and the Ask IT form |
| `/Account/Sso` | Windows single sign-on (only when `Auth:Sso` is on) |
| `/Account/Logout`, `/Account/Denied`, `/lang` | Sign out, access denied, language switch |

### Events
| Route | Who | Purpose |
|---|---|---|
| `/Events` | all roles | List (own events, or all for HOD and Admin) |
| `/Events/Create`, `/Events/Edit/{id}`, `/Events/Save` | Staff, Admin | Create, edit and submit an event request |
| `/Events/Details/{id}` | owner, HOD, Admin, Media (approved) | Details, history, downloads |
| `/Events/DownloadImage/{id}`, `/Events/DownloadQr/{id}` | owner, HOD, Admin, Media | Event image and QR code |
| `/Events/Poster/{id}`, `/Events/Screen/{id}` | Staff, Admin | Printable QR poster, venue screen (rotating QR) |
| `/Approvals` | anyone who may decide at a step | Requests waiting at a step the person may decide (and has not decided yet). Approve or reject; the event is confirmed only after the last step approves |
| `/Admin/ApprovalFlow` | Admin | Edit the approval chain: steps, order, who decides, approvals needed, extra email, on/off |
| `/Food/Create?eventId=` | event owner, Admin | Food request for an approved event |
| `/Food` | Admin | Confirm or reject food requests |

### External visits
| Route | Who | Purpose |
|---|---|---|
| `/Visits` | signed-in roles | Own visits; approvers, bus coordinators and Admin see all submitted |
| `/Visits/Create`, `/Visits/Edit/{id}`, `/Visits/Save` | Staff, Admin | Create, edit and submit |
| `/Visits/Details/{id}` | owner, approvers, bus coordinators, Admin | Details, decision and bus forms |
| `/Visits/Decide/{id}` | listed approvers (or HOD role), Admin | Approve or reject (comment required to reject) |
| `/Visits/ConfirmBus/{id}` | listed bus coordinators, Admin | Confirm the bus and save bus details |

### Research
| Route | Who | Purpose |
|---|---|---|
| `/Research` | Staff (own), Research in-charge and Admin (all) | Archive with search and download |
| `/Research/Upload` | Staff, Admin | Upload a paper or proposal (PDF, DOC, DOCX) |
| `/Research/Download/{id}` | uploader, Research in-charge, Admin | Download a file |

### Reports and administration (Admin)
| Route | Purpose |
|---|---|
| `/Reports`, `/Reports/Event/{id}` | Attendance and evaluation reports, live count, Excel exports |
| `/Admin/Settings` | Nine tabs: Recipients, External visits, Research, Sign-in help, Approval and check-in, QR and public page, Event image, Evaluation, Email templates, Display |
| `/Admin/RolesPage` | Give a role to an email address or username |
| `/Admin/Connections` | LDAP and email: defaults, tests, fetch groups, members, per-role preview, direct assignment |
| `/Admin/Appearance` | Colours, themes, light/dark mode and corner style for the whole platform |
| `/Admin/Audit`, `/Admin/AuditExport`, `/Admin/Notifications` | Audit log (filters, details, CSV / Excel export, clean-up), log of every email sent |

### Public host (no sign-in)
| Route | Purpose |
|---|---|
| `/p/{token}` | Check in at an event (name and organization) |
| `/p/{token}/evaluate` | Anonymous evaluation after check-in |


## KPI dashboard (`/Kpi`)

Same sign-in and top bar as the rest of the app. A row of buttons at the top of each KPI page shows only what the person's permissions allow.

| Route | Needs permission | What it does |
|---|---|---|
| `/Kpi` | any signed-in role | Sends you to the first KPI page you may open |
| `/Kpi/Dashboard` | dashboard.view | Charts and statistics |
| `/Kpi/Tracking` | tracking.view | Table of KPIs, entity assignment, targets, deadlines, status |
| `/Kpi/Reports/Export` | report.export | Excel report |
| `/Kpi/Admin/Kpis`, `/Kpi/Admin/KpiForm` | kpi.manage | List, add, edit, delete KPIs |
| `/Kpi/Admin/Review` | kpi.manage | Review items |
| `/Kpi/Admin/ImportExcel` | kpi.import | Excel import and template |
| `/Kpi/Admin/Entities` | entity.manage | KPI entities (departments) |
| `/Kpi/Admin/Roles` | role.manage | KPI roles and permissions (a role named like a directory role applies to it) |
| `/Kpi/Help/SiteMap` | any | KPI page list |

The Admin menu also has shortcuts to KPI permissions, KPI entities and Excel import. The old KPI login, user and LDAP pages no longer exist: use the normal `/Account/Login`.
