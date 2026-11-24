# VitalQ - Infrastructure as Code (Azure Bicep)

This directory contains the declarative **Azure Bicep** Infrastructure as Code (IaC) configuration for the VitalQ hospital platform. It automates provisioning of the full cloud environment across Azure Container Apps, Serverless Azure SQL, Redis Distributed Cache, Application Insights APM, and Azure Key Vault.

---

## 1. Directory Structure

```
infra/
├── main.bicep                 # Master deployment orchestrator
├── main.parameters.json       # Production environment parameters (Central India, Pune)
├── README.md                  # Operational runbook & deployment guide
└── modules/
    ├── logAnalytics.bicep     # Log Analytics workspace (log-vitalq-prod)
    ├── appInsights.bicep      # Application Insights APM (appi-vitalq-prod)
    ├── keyVault.bicep         # Azure Key Vault with RBAC & Soft-Delete (kv-vitalq-prod)
    ├── sqlServer.bicep        # Serverless Azure SQL Database & Firewall (sql-vitalq-prod)
    ├── containerApps.bicep    # Managed Environment, Redis, Web, and API with Managed Identity
    └── alerts.bicep           # Azure Monitor Metric Alerts & Action Group (ag-vitalq-devops)
```

---

## 2. Prerequisites

1. [Azure CLI](https://learn.microsoft.com/cli/azure/install-azure-cli) (v2.50.0+)
2. [Bicep CLI](https://learn.microsoft.com/azure/azure-resource-manager/bicep/install) (automatically installed via `az bicep install`)
3. Authenticated Azure session:
   ```bash
   az login
   az account set --subscription "<YOUR_SUBSCRIPTION_ID>"
   ```

---

## 3. Deployment Runbook

### Step 1: Pre-Flight Template Validation
Compile the Bicep templates into ARM JSON format to verify syntactic correctness and type-checking:

```bash
az bicep build --file infra/main.bicep
```

### Step 2: What-If Analysis (Pre-Flight Preview)
Run a What-If deployment to preview the exact resources Azure will create or update without applying changes:

```bash
az deployment group what-if \
  --resource-group rg-vitalq-prod-centralindia \
  --template-file infra/main.bicep \
  --parameters infra/main.parameters.json \
  --parameters sqlAdministratorLoginPassword="<STRONG_PASSWORD>"
```

### Step 3: Execute Deployment
Deploy the full stack to Azure:

```bash
az deployment group create \
  --name vitalq-bicep-deployment \
  --resource-group rg-vitalq-prod-centralindia \
  --template-file infra/main.bicep \
  --parameters infra/main.parameters.json \
  --parameters sqlAdministratorLoginPassword="<STRONG_PASSWORD>"
```

---

## 4. Key Architectural Highlights

* **Zero-Secret Active Directory Authentication**: The API service connects to Azure SQL via Microsoft Entra ID Managed Identity (`Authentication=Active Directory Default`).
* **Hardware-Backed Key Vault**: The JWT signing key is stored in Azure Key Vault and mounted directly into the Container App via Managed Identity secret reference (`keyvaultref:`).
* **Serverless Cost Optimization**:
  - `minReplicas = 0`: Scales to zero during off-peak hours to keep hosting costs within Azure's monthly free tier grant.
  - `autoPauseDelay = 60`: Automatically pauses the SQL database compute after 60 minutes of inactivity.
* **Internal Cluster Networking**: Redis runs inside the Container Apps Environment with no external ingress, listening exclusively on internal TCP port 6379.

---

## 5. Production Health Alerting & Metrics (Module 8)

The alerting subsystem [`infra/modules/alerts.bicep`](file:///d:/Projects/VitalQ/infra/modules/alerts.bicep) automates proactive monitoring with automated email notifications via Action Group `ag-vitalq-prod-devops`:

| Alert Rule Name | Target Resource | Metric & Condition | Severity | Clinical / DevOps Impact |
| :--- | :--- | :--- | :--- | :--- |
| **`alert-vitalq-api-5xx-errors`** | Application Insights | `requests/failed` > 5 in 5 min | Severity 1 (Error) | Catches unhandled exceptions, network drops, or backend API crashes immediately. |
| **`alert-vitalq-api-high-memory`**| `ca-vitalq-api` | `MemoryWorkingSetBytes` > 800 MiB | Severity 2 (Warning) | Proactive OOM warning before Linux cgroups terminates container with ExitCode 137. |
| **`alert-vitalq-api-high-cpu`**   | `ca-vitalq-api` | `UsageNanoCores` > 400M (80% of 0.5 vCore) | Severity 2 (Warning) | Identifies walk-in patient surges requiring additional container replica scaling. |
| **`alert-vitalq-sql-high-compute`**| `sqldb-vitalq-prod` | `cpu_percent` > 85% for 10 min | Severity 2 (Warning) | Protects against database lock contention, unindexed table scans, or query bottlenecks. |

