# AcxiomCRM - Enterprise CRM Platform

A production-ready, clean, layered **ASP.NET Core 8 MVC** application built according to the **AcxiomCRM CRM Project Documentation & Functional Specification**.

---

## 1. Executive Summary & Architecture

The application strictly implements the **Clean Layered Architecture** mandated by Section 3 of the specification:

```
                  Users / Browser / REST Clients
                                │
                                ▼
                    Presentation Layer (MVC & Razor)
     (Bootstrap 5, Chart.js, Unobtrusive Validation, Antiforgery Tokens)
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
       (ApplicationDbContext, Repositories, Parameterized Queries)
                                │
                                ▼
                       Database Layer (SQLite)
      (acxiomcrm.db with normalized schema, constraints & unique indexes)
                                │
                                ▼
      Cross-Cutting Services: Security + Logging + Validation + Auditing
```

---

## 2. Directory Structure

```
AcxiomCRM/
├── Controllers/
│   ├── HomeController.cs           # Executive Dashboard (KPIs & Chart.js)
│   ├── AuthController.cs           # Authentication (Login, Register, Logout, Lockout)
│   ├── CustomersController.cs      # Customer Master CRUD & Uniqueness
│   ├── LeadsController.cs          # Lead Capture, Lifecycle & Conversion Workflow
│   ├── OpportunitiesController.cs  # Opportunity Pipeline & Weighted Calculations
│   ├── FollowUpsController.cs      # Follow-up Schedule & Overdue Tracking
│   ├── ActivitiesController.cs     # Touchpoint Logs (Calls, Meetings, Emails, Tasks)
│   ├── ReportsController.cs        # 6 Dedicated Reports & Print/Export
│   ├── UsersController.cs          # Admin User & Role Administration
│   ├── AuditLogsController.cs      # Compliance Security & CRUD Audit Logs
│   └── Api/
│       ├── AuthApiController.cs         # POST /api/auth/login, POST /api/auth/logout
│       ├── CustomersApiController.cs    # GET, POST, PUT, DELETE /api/customers
│       ├── LeadsApiController.cs        # GET, POST /api/leads
│       ├── OpportunitiesApiController.cs# GET, POST /api/opportunities
│       ├── FollowUpsApiController.cs    # GET, POST /api/followups
│       └── ReportsApiController.cs      # GET /api/reports/pipeline
├── Models/
│   ├── ApplicationUser.cs          # Extended IdentityUser
│   ├── Customer.cs                 # Customer Master Data Entity
│   ├── Lead.cs                     # Lead Entity & Conversion Relations
│   ├── Opportunity.cs              # Opportunity Entity & Weighted Derivations
│   ├── FollowUp.cs                 # Follow-Up Entity with Future Date Validation
│   ├── Activity.cs                 # Activity Entity (Call, Meeting, Email, Task)
│   ├── AuditLog.cs                 # Append-only Security & Operation Audit Log
│   └── Attributes/
│       └── NotInPastDateAttribute.cs # Validation attribute rejecting past dates
├── Data/
│   ├── ApplicationDbContext.cs     # EF Core IdentityDbContext with Unique Indexes
│   └── DbInitializer.cs            # Automatic Seed Data for Roles, Users & CRM records
├── Services/
│   ├── IAuditService.cs & AuditService.cs       # Persistent Audit Logging
│   ├── ICrmScopeService.cs & CrmScopeService.cs # Role-Based Query & Entity Scoping
│   └── IDashboardService.cs & DashboardService.cs # Real-time KPI & Chart Aggregations
├── ViewModels/                     # Clean ViewModels with UI Annotations
├── Dtos/                           # Data Transfer Objects for REST APIs
└── Views/                          # Modern Bootstrap 5 Razor Views
```

---

## 3. Preconfigured Demo Accounts

The database automatically seeds roles and users upon initial launch:

| Role | Username / Email | Password | Scope & Permissions |
| :--- | :--- | :--- | :--- |
| **Admin** | `admin@acxiom.com` | `Admin@123` | Full administrative control, all CRM data, User/Role management, Audit Logs |
| **Manager** | `manager@acxiom.com` | `Manager@123` | Team pipeline, leads, opportunities, follow-ups, and executive reports |
| **Sales Executive** | `sales@acxiom.com` | `Sales@123` | Scoped strictly to assigned customer records, leads, deals, and activities |

---

## 4. Key Functional Features & Acceptance Validation

### 4.1 Authentication & Security (Sections 4.2, 6, 17.4, 17.9)
- **ASP.NET Core Identity**: Secure password hashing with PBKDF2/Argon2. Plain-text passwords are never stored or logged.
- **Password Policy**: Minimum 8 characters, requiring digits, lowercase, and uppercase letters.
- **Account Lockout**: Locks account after 5 consecutive failed login attempts for 15 minutes.
- **Anti-Forgery**: State-changing POST requests are protected with `[ValidateAntiForgeryToken]` and token validation.
- **Audit Logging**: Every login success, failed login, account lockout, logout, role change, and CRUD action writes an append-only audit record.

### 4.2 Role-Based Authorization & Scoping (Sections 7, 17.10)
- **Admin**: Complete access across the organization, including user provisioning, role assignments, and full audit trail exploration.
- **Manager**: Access to team-wide pipelines, conversion stats, and performance reports.
- **SalesExecutive**: Access is automatically scoped to records where `OwnerId == currentUserId` or `AssignedTo == currentUserId`. Direct URL tampering returns `403 Forbidden`.

### 4.3 Validation Matrix (Sections 5, 17.5, 17.6, 17.7, 17.8)
| Field / Rule | Validation Implemented | Behavior / Error Message |
| :--- | :--- | :--- |
| **Customer Name** | Required, max 100 chars | *"Customer Name is required."* |
| **Customer Email** | Required, valid email, unique | *"A customer with this email address already exists."* |
| **Customer Phone** | Required, format regex, unique | *"A customer with this phone number already exists."* |
| **Opportunity Amount** | Required, Range > 0 | *"Opportunity Amount must be greater than 0."* |
| **Probability** | Required, Range 0 to 100 | *"Probability must be between 0 and 100."* |
| **Expected Close Date**| Date >= Today for active deals | *"Expected Close Date cannot be in the past."* |
| **Follow-Up Date** | Date >= Today for planned tasks| *"Follow-up date cannot be earlier than today."* |
| **Lead Status** | Mandatory valid enum set | Must be New, Contacted, Qualified, Unqualified, Converted, or Lost |

### 4.4 Lead-to-Customer Conversion Workflow (Section 8, 17.2)
1. Qualified leads can be converted via the **Convert Lead** workflow.
2. Creates a new **Customer Account** from lead contact details.
3. Automatically provisions an linked **Sales Opportunity** with user-selected pipeline stage, amount, probability, and close date.
4. Marks the lead status as `Converted`, establishes reference keys, and records an audit log entry.
5. Converted leads are protected from reversion.

### 4.5 Executive Dashboard & Visualizations (Sections 4.3, 17.11, 17.12)
- **8 Dynamic KPI Cards**: Total Customers, Total Leads, Open Leads, Total Opportunities, Open Opportunities, Won Deals, Open Pipeline Value, and Weighted Pipeline Value (`Amount * Probability / 100`).
- **Date Filters**: All Time, Today, This Week, This Month.
- **Interactive Chart.js Charts**:
  - *Lead Status Breakdown* (Doughnut chart)
  - *Opportunity Pipeline by Stage* (Bar chart)
  - *Won Revenue Trend* (Line chart for 6-month historical outcomes)
- **Upcoming & Overdue Follow-Ups**: Highlighted alerts with 1-click completion.

### 4.6 REST APIs (Sections 10, 17.14)
- Secured endpoints utilizing clean DTOs (Data Transfer Objects):
  - `POST /api/auth/login`
  - `POST /api/auth/logout`
  - `GET /api/customers`, `GET /api/customers/{id}`, `POST /api/customers`, `PUT /api/customers/{id}`, `DELETE /api/customers/{id}`
  - `GET /api/leads`, `POST /api/leads`
  - `GET /api/opportunities`, `POST /api/opportunities`
  - `GET /api/followups`, `POST /api/followups`
  - `GET /api/reports/pipeline`

---

## 5. How to Run the Project

### Running the Web Application
Open PowerShell or your preferred terminal in the project directory:
```powershell
cd C:\Users\swetha\.gemini\antigravity\scratch\AcxiomCRM
dotnet run
```
Navigate to: **`http://localhost:5000`** (or `https://localhost:5001`)

### Running the Test Suite
```powershell
cd C:\Users\swetha\.gemini\antigravity\scratch\AcxiomCRM.Tests
dotnet run
```
**Test Results**: 10 passed, 0 failed covering all Section 17.19 Acceptance Scenarios!
