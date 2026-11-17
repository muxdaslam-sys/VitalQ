# VitalQ: Microsoft Azure Cloud & DevOps Mastery Roadmap
### Target: 3-5+ Years Experience Full-Stack Cloud Engineer (.NET 8 + Angular 19 + Azure)
### Focus Market: Infopark Kochi & Enterprise IT Hubs

---

## 1. Executive Strategy: Why This Approach Works

In technical interviews for **3-5+ years experience** roles at companies in Infopark Kochi (such as UST, IBS Software, Cognizant, Experion Technologies, Nuvento, KeyValue, Fingent, and Suyati), interviewers do not just evaluate whether you can write C# syntax or build an Angular form.

They evaluate **production maturity**:
1. **Can you take an application from local development to a scalable cloud production environment?**
2. **Can you design for zero-downtime deployments, distributed state, and automated CI/CD?**
3. **Can you troubleshoot live performance bottlenecks, database latency, and security vulnerabilities using enterprise cloud telemetry?**

By integrating Microsoft Azure, Docker, and DevOps directly into VitalQ section by section, you will not be memorizing textbook answers. You will have built, configured, and troubleshot every cloud component with your own hands.

---

## 2. Infopark Kochi Enterprise Tech Stack Alignment

| Capability Area | What 1-2 Year Juniors Know | What 3-5+ Year Seniors Must Demonstrate | VitalQ Azure Integration |
| :--- | :--- | :--- | :--- |
| **Hosting & Compute** | Runs on `localhost:5089` or basic IIS Express | Containerized micro-deployments, serverless containers, auto-scaling | Azure Container Apps / App Service with Docker |
| **Data & Storage** | Local SQL Server connection string | Managed Azure SQL, failover retries, connection pooling, cloud blob storage | Azure SQL Database + Azure Blob Storage |
| **Secrets & Security** | Plain text passwords in `appsettings.json` | Zero-trust secrets, Managed Identity, cloud vault rotation | Azure Key Vault + Managed Identity |
| **Scaling & State** | In-memory cache (`IMemoryCache`), breaks when multi-instanced | Distributed caching, Redis backplane, WebSocket scaling | Azure Cache for Redis + Azure SignalR Service |
| **DevOps & Releases** | Manual build and publish via Visual Studio | Automated GitOps pipelines, automated test gates, container registry | GitHub Actions CI/CD + Azure Container Registry (ACR) |
| **Observability** | `Console.WriteLine` or local flat text log files | Distributed tracing, APM, dependency latency breakdown, live metrics | Azure Application Insights + Structured Serilog |

---

## 3. Section-by-Section Azure Mastery Plan

```
┌──────────────────────────────────────────────────────────────────────────────────────┐
│                            VITALQ AZURE CLOUD ARCHITECTURE                           │
├──────────────────────────────────────────────────────────────────────────────────────┤
│                                                                                      │
│   [ GitHub Repository ]                                                              │
│           │                                                                          │
│           ▼ (Push to main / PR)                                                      │
│   [ GitHub Actions CI/CD Pipeline ] ──► Runs 63 Tests (.NET + Karma)                 │
│           │                                                                          │
│           ▼ (Build & Push Containers)                                                │
│   [ Azure Container Registry (ACR) ]                                                 │
│           │                                                                          │
│           ▼ (Continuous Zero-Downtime Deployment)                                   │
│   ┌──────────────────────────────────────────────────────────────────────────────┐   │
│   │                     Azure Container Apps / App Service                       │   │
│   │                                                                              │   │
│   │   ┌─────────────────────────────┐      ┌─────────────────────────────────┐   │   │
│   │   │   Angular 19 Standalone     │      │   .NET 8 Web API Core           │   │   │
│   │   │   (Nginx Alpine Reverse)    │─────►│   (Chiseled Secure Runtime)     │   │   │
│   │   └─────────────────────────────┘      └────────────────▲────────────────┘   │   │
│   │                                                         │                    │   │
│   └─────────────────────────────────────────────────────────┼────────────────────┘   │
│                                                             │                        │
│                 ┌───────────────────┬───────────────────────┼────────────────────┐   │
│                 ▼                   ▼                       ▼                    ▼   │
│        [ Azure SQL DB ]    [ Azure Key Vault ]     [ Azure Redis ]    [ App Insights ]
│        (Managed Relational) (Zero-Trust Secrets)   (Distributed State)  (APM & Traces)
│                                                                                      │
└──────────────────────────────────────────────────────────────────────────────────────┘
```

---

### Module 1: Containerization Foundation (Docker & Docker Compose)
> **Goal**: Package the backend and frontend into lightweight, secure, production-grade container images.

#### Core Concepts to Master:
- **Multi-Stage Builds**: Compiling code in a heavy SDK container, then copying only compiled binaries into a minimal runtime image (reduces image size from 1GB+ down to under 120MB).
- **Security Hardening**: Using non-root / chiseled images (`mcr.microsoft.com/dotnet/aspnet:8.0-noble-chiseled`) to prevent container breakout exploits.
- **Reverse Proxy Routing**: Serving the Angular SPA via Nginx Alpine with gzip compression, cache headers, and reverse proxying `/api` and `/hubs`.
- **Local Multi-Service Orchestration**: Using `docker-compose.yml` to spin up `.NET API`, `Angular Web`, `SQL Server`, and `Redis` simultaneously with shared bridge networks and persistent volumes.

#### Interview Questions This Answers:
- *"Why use multi-stage Docker builds instead of a single build step?"*
- *"How do you handle Angular environment URLs when running inside Docker containers?"*
- *"How do you ensure non-root execution inside a production container?"*

---

### Module 2: Azure Compute & Hosting (Azure Container Apps / App Service)
> **Goal**: Deploy VitalQ to scalable cloud compute with HTTPS, custom domains, and auto-scaling.

#### Core Concepts to Master:
- **Azure Container Apps (ACA)**: Serverless container platform built on top of Kubernetes (K8s) without Kubernetes management overhead.
- **KEDA Auto-Scaling**: Scaling replicas from 1 to N based on incoming HTTP request volume or CPU/memory pressure.
- **Azure App Service (Linux)**: PaaS hosting with deployment slots (Staging vs Production) for zero-downtime blue/green deployments.
- **WebSocket Sticky Sessions**: Ensuring SignalR persistent connections remain connected to the appropriate replica.

#### Interview Questions This Answers:
- *"What is the difference between Azure App Service, Azure Container Apps, and Azure Kubernetes Service (AKS)? When do you choose which?"*
- *"How do deployment slots work in Azure, and how do you execute zero-downtime swap deployments?"*
- *"How does KEDA scale containers based on event-driven metrics?"*

---

### Module 3: Azure Databases, Cloud Storage & Zero-Trust Security
> **Goal**: Migrate local database to cloud Azure SQL, store file assets in Blob Storage, and eliminate all secrets from code using Azure Key Vault.

#### Core Concepts to Master:
- **Azure SQL Database**: Provisioning serverless/general purpose cloud SQL with automated backups, point-in-time restores, and firewall rules.
- **EF Core Connection Resiliency**: Enabling `EnableRetryOnFailure` to automatically survive transient cloud network drops.
- **Azure Key Vault**: Centralizing JWT secrets, database connection strings, and API keys.
- **Azure Managed Identity**: Eliminating passwords and client secrets entirely: your API container authenticates to Key Vault and Azure SQL using its Azure Active Directory (Microsoft Entra ID) identity.
- **Azure Blob Storage**: Storing clinical reports, consultation notes PDFs, and medical scans in cloud storage buckets with SAS (Shared Access Signature) tokens.

#### Interview Questions This Answers:
- *"How do you protect production connection strings and JWT signing keys from leaking into Git repositories?"*
- *"What is Azure Managed Identity (System-Assigned vs User-Assigned) and how does passwordless cloud authentication work?"*
- *"How does EF Core handle transient connection drops in cloud databases?"*

---

### Module 4: Real-Time Scaling & Distributed Caching (Redis & SignalR)
> **Goal**: Enable VitalQ to scale to multiple API replicas without losing cache consistency or dropping real-time doctor call alerts.

#### Core Concepts to Master:
- **The Multi-Instance Problem**: When 3 instances of VitalQ run behind a load balancer, `IMemoryCache` is local to each instance. A doctor status update on Instance 1 is invisible to Instance 2.
- **Azure Cache for Redis**: Distributed in-memory data store providing sub-millisecond shared cache across all running instances.
- **SignalR Scale-Out Problem**: A patient connected to Instance 1 will not receive a `PatientCalled` event sent by a doctor connected to Instance 2 unless a backplane is used.
- **Azure SignalR Service**: Fully managed real-time messaging service that handles 100,000+ simultaneous WebSocket connections without server CPU bottleneck.

#### Interview Questions This Answers:
- *"What happens to in-memory caching when you scale your Web API horizontally across multiple instances?"*
- *"How do you solve the SignalR multi-server message distribution problem?"*
- *"What Redis eviction policies exist, and when would you use Redis Pub/Sub?"*

---

### Module 5: DevOps Automation (GitHub Actions CI/CD)
> **Goal**: Automate testing, image building, security scanning, and deployment on every Git push.

#### Core Concepts to Master:
- **CI Pipeline (Continuous Integration)**:
  - Triggers on Pull Requests and pushes.
  - Automatically runs `dotnet test` (50 tests) and `npm test` (13 tests).
  - Fails the build if any test fails, blocking bad code from merging into `main`.
- **CD Pipeline (Continuous Deployment)**:
  - Builds Docker images with commit SHA tags.
  - Logs into Azure Container Registry (ACR) using OpenID Connect (OIDC) without long-lived credentials.
  - Deploys the new revision to Azure Container Apps with health probe verification.
  - Automatically rolls back if the readiness probe `/health` fails.

#### Interview Questions This Answers:
- *"Explain your Git branch workflow and CI/CD release pipeline."*
- *"How do you handle automated testing and deployment gates in GitHub Actions?"*
- *"How do you authenticate GitHub Actions to Azure securely without storing permanent service principal passwords?" (OIDC / Federated Credentials).*

---

### Module 6: Enterprise Observability & Monitoring (Application Insights & Serilog)
> **Goal**: Monitor live latency, failure rates, database query times, and user journeys with zero guesswork.

#### Core Concepts to Master:
- **Azure Application Insights**: Cloud APM (Application Performance Monitoring) tracking server response times, dependency failures, and exceptions in real time.
- **Structured Logging (Serilog)**: Emitting JSON logs with correlation IDs (`X-Correlation-Id`), enabling you to trace a single patient booking across frontend, API, database, and SignalR.
- **Live Metrics & Alerts**: Setting up email/webhook alerts when server error rates exceed 1% or when API response time exceeds 500ms.
- **Application Health Checks**: Exposing `/health/live` (process is alive) and `/health` (database and Redis are reachable) for Azure orchestrator health probing.

#### Interview Questions This Answers:
- *"A client reports that the application felt slow around 2:00 PM yesterday. How do you investigate the exact root cause using Application Insights?"*
- *"What is distributed tracing, and how do correlation IDs work across microservices or front-to-back requests?"*
- *"What is the difference between liveness and readiness health probes in container orchestration?"*

---

## 4. Infopark Technical Interview Prep Matrix

These are the exact high-value scenario questions asked in 3-5+ year interviews at Infopark Kochi, mapped directly to how VitalQ solves them:

| Interview Question | Junior Answer (1-2 Years) | Senior Answer (3-5+ Years with VitalQ) |
| :--- | :--- | :--- |
| *"How do you secure sensitive settings like database credentials?"* | *"I put them in appsettings.json or environment variables."* | *"In local development, we use User Secrets. In production on Azure, we use Azure Key Vault with Managed Identity. The application code contains zero secrets or passwords, authenticated automatically through Azure Active Directory."* |
| *"How do you handle real-time WebSockets when scaling across multiple servers?"* | *"SignalR handles it automatically."* | *"SignalR alone cannot broadcast across multiple independent server instances because clients hold connections to different servers. To scale horizontally, we integrate Azure SignalR Service (or a Redis backplane) so messages publish to a centralized message bus and broadcast to all connected nodes."* |
| *"How do you ensure zero-downtime when releasing a new version?"* | *"I deploy after office hours during low traffic."* | *"We use containerized deployment slots with blue/green release pipelines in GitHub Actions. The new container version spins up, executes readiness probes against /health, primes the connection pool, and only swaps traffic after passing all health checks."* |
| *"How do you test your software before release?"* | *"We test it manually in Swagger or Postman."* | *"We maintain 63 automated tests (50 xUnit tests covering concurrency, CTAS scoring, and database transactions; 13 Jasmine/Karma frontend tests). These tests are hard gates in our GitHub Actions pipeline; a single test failure immediately aborts the deployment."* |
| *"How do you monitor production performance and errors?"* | *"I look at console logs or remote desktop into the server."* | *"We use Azure Application Insights integrated with Serilog structured logging. We correlate every request with an X-Correlation-Id header, track dependency latencies (SQL queries, Redis calls), and configure metric alerts for HTTP 500 spikes and p95 latency thresholds."* |

---

## 5. Practical Implementation Checklist

- [x] **Phase 1: Local Containerization**
  - [x] Write optimized multi-stage `api/VitalQ.API/Dockerfile` (.NET 8 chiseled runtime).
  - [x] Write optimized multi-stage `web/Dockerfile` (Angular 19 + Nginx Alpine).
  - [x] Write `web/nginx.conf` (SPA client routing fallback and SignalR WebSocket reverse proxy).
  - [x] Write root `docker-compose.yml` (API + Web + SQL Server).
  - [x] Write `.dockerignore` for build context optimization.

- [ ] **Phase 2: CI/CD Automation with GitHub Actions**
  - [x] Create `.github/workflows/ci.yml` running all 63 unit/stress tests on push and building Docker images in the cloud.
  - [ ] Create `.github/workflows/cd.yml` building and publishing Docker images to Azure Container Registry (ACR).

- [ ] **Phase 3: Azure Cloud Setup**
  - [ ] Provision Azure Resource Group, Azure SQL Database, and Azure Container Registry (ACR).
  - [ ] Configure Azure Key Vault with Managed Identity for secure configuration.
  - [ ] Deploy containerized API and Web SPA to Azure Container Apps (ACA).

- [ ] **Phase 4: Cloud Real-Time Scaling & Telemetry**
  - [ ] Configure Redis distributed cache and SignalR scale-out backplane.
  - [ ] Wire up Azure Application Insights telemetry and structured Serilog sink.
  - [ ] Connect `/health` and `/health/live` endpoints to Azure container health probes.

---

*This roadmap is preserved in the repository root as the master learning and implementation guide for your 3-5+ year cloud engineering portfolio.*
