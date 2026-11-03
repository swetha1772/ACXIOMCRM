# AcxiomCRM - Enterprise Cloud CRM Solution

[![CI - Build and Test AcxiomCRM](https://github.com/OWNER/REPOSITORY/actions/workflows/ci.yml/badge.svg)](https://github.com/OWNER/REPOSITORY/actions)
![.NET 8](https://img.shields.io/badge/.NET-8.0-blue.svg)
![Architecture](https://img.shields.io/badge/Architecture-Clean%20Layered-emerald.svg)
![Database](https://img.shields.io/badge/Database-SQLite%20%2F%20EF%20Core-blueviolet.svg)
![Bootstrap](https://img.shields.io/badge/Frontend-Bootstrap%205%20%2B%20Chart.js-purple.svg)

A production-ready, clean, layered **ASP.NET Core 8 MVC** application built strictly according to the **AcxiomCRM Functional Specification**.

---

## 1. System Architecture

The application strictly implements the **Clean Layered Architecture** mandated by Section 3 of the specification:

```
                  Users / Web Browsers / REST Clients
                                 │
                                 ▼
                     Presentation Layer (MVC & Razor)
     (Bootstrap 5, Chart.js, Unobtrusive Client Validation, Anti-Forgery)
                                 │
                                 ▼
                        Application Layer
       (Controllers, DTOs, ViewModels, Workflow Orchestration)
                                 │
                                 ▼
                      Domain / Business Layer
       (Entities, Business Validation Rules, Status Transitions, Scoping)
                                 │
                                 ▼
                      Data Access Layer (EF Core)
        (ApplicationDbContext, IdentityDbContext, Fluent API Mappings)
                                 │
                                 ▼
                        Database Layer (SQLite)
       (acxiomcrm.db with unique composite indices, cascades & relationships)
                                 │
                                 ▼
       Cross-Cutting Services: Security + Logging + Scoping + Audit Trail
```

---

## 2. Default Seeded Credentials

When launched for the first time, the database (`acxiomcrm.db`) automatically seeds roles, users, and comprehensive CRM demo records:

| Role | Username / Email | Password | Access Scope |
| :--- | :--- | :--- | :--- |
| **Administrator** | `admin@acxiom.com` | `Admin@123` | Full access: User administration, account unlock, password reset, audit log explorer, and all CRM data. |
| **Sales Manager** | `manager@acxiom.com` | `Manager@123` | Full CRM visibility: Team pipeline, department leads, opportunities, follow-ups, and all 8 reports. |
| **Sales Executive** | `sales@acxiom.com` | `Sales@123` | Scoped access: Strictly restricted to owned leads, customers, deals, and activities. |

---

## 3. Local Development & Setup

### Prerequisites
- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Git

### Build & Run
```bash
# Clone the repository
git clone https://github.com/YOUR_USERNAME/AcxiomCRM.git
cd AcxiomCRM

# Build solution
dotnet build AcxiomCRM.sln

# Run web application
dotnet run --project AcxiomCRM/AcxiomCRM.csproj --urls "http://localhost:5000"
```

Open your browser at **`http://localhost:5000`** and log in with any seeded credentials.

---

## 4. Automated Acceptance Testing

A dedicated test suite validates all 14 Acceptance Criteria specified in Section 17.19:

```bash
dotnet run --project AcxiomCRM.Tests/AcxiomCRM.Tests.csproj
```

**Test Verification Summary:**
- [x] Customer Validation (Required Name, Email format, Phone format)
- [x] Customer Duplicate Email & Phone Prevention
- [x] Opportunity Amount $\le$ 0 Rejected
- [x] Opportunity Probability $>$ 100 Rejected
- [x] Opportunity Expected Close Date in Past Rejected
- [x] Follow-Up Date Before Today Rejected for Planned Task
- [x] Role-Based Scoping (`SalesExecutive` vs `Manager` / `Admin`)
- [x] Lead Conversion to Customer & Opportunity Workflow + Audit Logging
- [x] Weighted Pipeline Derivation Formula ($\text{Amount} \times \frac{\text{Probability}}{100}$)
- [x] REST API DTOs & Validation

---

## 5. Deployment Options

### Option A: 1-Click Deploy on Render (Recommended Free/Easy Tier)
1. Fork or push this repository to GitHub.
2. Sign in to [Render](https://render.com).
3. Click **New +** $\to$ **Web Service**.
4. Select your GitHub repository.
5. Render detects the **Dockerfile** automatically.
   - **Environment:** Docker
   - **Plan:** Free
   - **Port:** `8080` (already configured in Dockerfile)
6. Click **Deploy Web Service**. Your live URL will be active in minutes.

---

### Option B: Deploy on Railway
1. Push this repository to GitHub.
2. Go to [Railway](https://railway.app) and click **New Project** $\to$ **Deploy from GitHub repo**.
3. Select this repository. Railway will detect the multi-stage `Dockerfile` and build it automatically.
4. Add a volume mount (optional) at `/app` if you wish to persist the SQLite database across restarts.

---

### Option C: Docker Container (Self-Hosted / Cloud VPS)
```bash
# Build Docker image
docker build -t acxiomcrm .

# Run container on port 8080
docker run -d -p 8080:8080 --name crm-app acxiomcrm
```
Access the application at `http://localhost:8080`.

---

### Option D: Microsoft Azure App Service
```bash
az webapp up --name acxiom-crm-app --resource-group crm-rg --plan crm-plan --os-type Linux --runtime "DOTNETCORE:8.0"
```

---

## 6. REST API Documentation

AcxiomCRM exposes clean RESTful endpoints returning proper HTTP status codes (`200 OK`, `201 Created`, `400 BadRequest`, `404 NotFound`, `409 Conflict`).

Once the app is running, navigate to **`/api-docs`** for interactive REST API documentation and example JSON payloads.

| Method | Endpoint | Description |
| :--- | :--- | :--- |
| `POST` | `/api/auth/login` | Authenticate API client and establish session cookie |
| `POST` | `/api/auth/logout` | Revoke session cookie |
| `GET` | `/api/customers` | List scoped customers (Search / Pagination) |
| `POST` | `/api/customers` | Create new customer (with uniqueness enforcement) |
| `GET` | `/api/leads` | List scoped leads (Filter by Status / Priority) |
| `POST` | `/api/leads` | Create a new lead |
| `GET` | `/api/opportunities` | List scoped opportunities with weighted amounts |
| `POST` | `/api/opportunities` | Create an opportunity (Stage & probability validation) |
| `GET` | `/api/followups` | List upcoming and overdue follow-ups |
| `POST` | `/api/followups` | Schedule a follow-up (Past date rejection) |
| `GET` | `/api/reports/pipeline` | Pipeline summary grouped by stage with weighted totals |

---

## 7. License

Licensed under the MIT License.
