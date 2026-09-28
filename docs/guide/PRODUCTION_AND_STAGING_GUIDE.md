# Heimdall Production & Staging Operations Guide

This guide details how to manage, deploy, and operate the Heimdall Industrial OT Platform in **Staging** and **Production** environments using the `just` command runner, documents the recommended **filesystem installation directory structure**, and provides an exhaustive reference for all **required configuration variables**.

---

## 1. Overview & Architecture Matrix

Heimdall supports three primary lifecycle environments:

| Environment | Dev Features (`HEIMDALL_ENABLE_DEV`) | Debug Features (`HEIMDALL_ENABLE_DEBUG`) | Database Name | Auth / Encryption Keys | Primary Purpose |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **Development** | `true` (Enabled) | `true` (Enabled) | `heimdall_dev_db` | Standard Dev Keys | Rapid local iteration with Vite HMR, Swagger UI, simulated AD OUs, and dev personas. |
| **Staging** | **`false` (Disabled)** | **`true` (Enabled)** | `heimdall_staging_db` | Non-Default Entropy | Mirror production security & architecture; full enterprise seed data; verbose diagnostics & inspection enabled. |
| **Production** | **`false` (Disabled)** | **`false` (Disabled)** | `heimdall_db` | Cryptographic Entropy | Live plant floor operation; zero mock bypasses; strict zero-trust authentication; production telemetry ingestion. |

---

## 2. Using `just` for Production & Staging Environments

Heimdall uses [just](https://github.com/casey/just) as its unified command orchestrator across development, testing, staging, and production.

### 2.1 Prerequisites
Ensure the target server or runner has the following packages:
- **`just`** (`1.50+`)
- **Docker Engine** (`24.0+`) & **Docker Compose v2** (`docker compose` plugin)
- **.NET 10 SDK** (for bare-metal/systemd agent deployments)
- **Bun** or **Node.js 20+** (if building frontend outside containers)

### 2.2 Production Commands Reference

| Command | Action | Description |
| :--- | :--- | :--- |
| `just prod-build` | `docker compose -f docker-compose.prod.yml build` | Compiles optimized container images for backend, frontend, and edge agent. |
| `just prod-up` | `docker compose -f docker-compose.prod.yml up -d` | Launches production PostgreSQL, Redis, ASP.NET Core backend, and Nuxt 4 frontend in detached mode. |
| `just prod-down` | `docker compose -f docker-compose.prod.yml down` | Gracefully stops all production services and disconnects container networks. |
| `just prod-restart` | `just prod-down && sleep 1 && just prod-up` | Safely restarts the entire production stack. |
| `just prod-status` | `docker compose -f docker-compose.prod.yml ps` | Displays healthcheck statuses, port mappings, and container uptimes. |
| `just prod-logs [svc]`| `docker compose -f docker-compose.prod.yml logs -f [svc]` | Streams production logs for `backend`, `frontend`, `postgres`, `redis`, or all services. |

#### Step-by-Step Production Deployment:
```bash
# 1. Switch to production configuration
cp .env.example /etc/heimdall/heimdall.env
# Edit secrets in /etc/heimdall/heimdall.env (see Section 4)

# 2. Build production containers
just prod-build

# 3. Boot production stack
just prod-up

# 4. Verify container health
just prod-status

# 5. Tail application logs
just prod-logs backend
```

---

### 2.3 Staging Commands Reference

The Staging environment provides an identical configuration to production with **dev features disabled**, but enables **diagnostic debug inspection** and comes pre-seeded with the enterprise plant dataset (100 machines, 16 organizations, 56 edge IPCs, 550 spare parts).

| Command | Action | Description |
| :--- | :--- | :--- |
| `just staging-up` | `just db-staging-seed && docker compose -f docker-compose.staging.yml up -d` | Automatically verifies seed data and brings up the staging environment stack. |
| `just staging-down` | `docker compose -f docker-compose.staging.yml down` | Stops the staging stack. |
| `just staging-restart`| `just staging-down && just staging-up` | Restarts the staging stack. |
| `just staging-seed` | Seeds `heimdall_staging_db` | Applies `seed_data/incremental_seed.sql` to populate 100 machines and enterprise seed data. |
| `just staging-status` | `docker compose -f docker-compose.staging.yml ps` | Checks container status for staging. |
| `just test-staging` | `python3 tools/test_staging.py` | Executes the complete verification test suite under staging configuration (DB counts, dev gating, backend & frontend unit tests). |
| `just staging-logs` | `docker compose -f docker-compose.staging.yml logs -f` | Streams staging logs. |

---

## 3. Installation Directory Structure

For bare-metal, systemd, or containerized Linux server installations, Heimdall follows the Linux Filesystem Hierarchy Standard (FHS):

```
/
├── opt/
│   └── heimdall/                             # Application root directory (owned by heimdall:heimdall, 0755)
│       ├── justfile                          # Master command orchestrator
│       ├── docker-compose.prod.yml           # Production Docker Compose stack
│       ├── docker-compose.staging.yml        # Staging Docker Compose stack
│       ├── backend/                          # Backend ASP.NET Core source / compiled binaries
│       ├── frontend/                         # Nuxt 4 web dashboard build artifacts
│       ├── agent/                            # Industrial edge agent binaries
│       ├── seed_data/                        # Enterprise plant dataset and seed scripts
│       │   ├── inventory_seed.csv            # 100 machines, 56 PCs, 550 serialized stock parts
│       │   ├── incremental_seed.sql          # Transactional PostgreSQL database seed script
│       │   └── seed_pipeline.py              # Data pipeline and referential integrity validator
│       ├── scripts/                          # Packaging and release automation scripts
│       └── tools/                            # TUI dashboard, dev manager, and test suites
│           ├── tui.py                        # Terminal dashboard (interfacing with just)
│           ├── dev_manager.py                # Healthcheck and service topology checker
│           └── test_staging.py               # Staging verification suite
│
├── etc/
│   └── heimdall/                             # Platform configuration & secrets (0700, root:heimdall)
│       ├── heimdall.env                      # Production / Staging environment file (chmod 0600)
│       ├── agent.env                         # Industrial edge agent daemon configuration
│       ├── certs/                            # Cryptographic keys and TLS certificates (0700)
│       │   ├── ca.crt                        # Internal Certificate Authority certificate
│       │   ├── server.crt                    # Server public TLS certificate
│       │   └── server.key                    # Server private key (chmod 0600)
│       └── redis.conf                        # Production Redis security configuration
│
├── var/
│   ├── lib/
│   │   └── heimdall/                         # Persistent state & database volumes (0750, heimdall:heimdall)
│   │       ├── postgres_data/                # PostgreSQL database files
│   │       ├── redis_data/                   # Redis AOF (Append-Only File) storage
│   │       ├── agent_spool/                  # Offline edge telemetry spool queue (SQLite / RocksDB)
│   │       └── uploads/                      # Maintenance ticket attachments & CAD blueprints
│   │
│   └── log/
│       └── heimdall/                         # Centralized application logs (0755, heimdall:heimdall)
│           ├── backend.log                   # ASP.NET Core API access and error logs
│           ├── frontend.log                  # Nitro BFF SSR logs
│           ├── agent.log                     # Edge agent collection and dispatch logs
│           └── postgresql.log                # Database transaction and query audit logs
│
└── etc/systemd/system/                       # Systemd daemon units (for non-containerized hosts)
    ├── heimdall-backend.service              # ASP.NET Core 10 API daemon
    ├── heimdall-agent.service                # Edge Telemetry Collector daemon
    └── heimdall-frontend.service             # Nuxt / Node.js web server daemon
```

### 3.1 Permissions & Security Hardening
1. **Service User & Group**:
   ```bash
   sudo useradd -r -s /usr/sbin/nologin -d /opt/heimdall heimdall
   sudo groupadd heimdall
   ```
2. **Configuration Protection**:
   ```bash
   sudo chown -R root:heimdall /etc/heimdall
   sudo chmod 750 /etc/heimdall
   sudo chmod 600 /etc/heimdall/*.env
   sudo chmod 600 /etc/heimdall/certs/*.key
   ```
3. **Data Directories**:
   ```bash
   sudo chown -R heimdall:heimdall /var/lib/heimdall /var/log/heimdall
   sudo chmod 750 /var/lib/heimdall
   ```

---

## 4. Required Configuration Reference

Every environment requires specific variables defined in `.env` (or `/etc/heimdall/heimdall.env`). Templates are provided at:
- **`.env.example`**: General reference template with comments and instructions.
- **`.env.dev`**: Ready-to-use local development configuration.
- **`.env.staging`**: Staging environment configuration (dev features disabled, debug enabled).

### 4.1 Environment Targets & Feature Flags

| Variable | Staging | Production | Description |
| :--- | :--- | :--- | :--- |
| `ASPNETCORE_ENVIRONMENT` | `Staging` | `Production` | Controls ASP.NET Core hosting model, exception pages, and bundle optimizations. |
| `NODE_ENV` | `production` | `production` | Nuxt 4 SSR mode and Vite production bundling. |
| `DOTNET_ENVIRONMENT` | `Staging` | `Production` | Worker daemon environment flag for Edge Agent. |
| `HEIMDALL_ENABLE_DEV` | **`false`** | **`false`** | **Critical Security Guard**. When `false`, disables Swagger/OpenAPI docs, simulated AD OU endpoints, dev login bypass, and dev database resets. |
| `HEIMDALL_ENABLE_DEBUG` | **`true`** | **`false`** | Enables verbose diagnostic dumps and telemetry inspection without enabling dev bypasses. |
| `NUXT_PUBLIC_ENABLE_DEV_FEATURES` | **`false`** | **`false`** | Hides dev tools and persona spoofing UI elements in the frontend client. |
| `NUXT_PUBLIC_ENABLE_DEBUG_FEATURES` | **`true`** | **`false`** | Exposes diagnostic inspection dialogs in the frontend client. |
| `ENABLE_DEV_HTTP_SEED` | **`false`** | **`false`** | Disables `/api/dev/seed-admin` HTTP endpoint. Staging is seeded via direct database SQL. |

---

### 4.2 Cryptographic & Security Keys

> [!CAUTION]
> In Staging and Production, default development keys are rejected on startup. You MUST supply cryptographically secure random keys.

| Variable | Minimum Length | Purpose & Generation |
| :--- | :--- | :--- |
| `HEIMDALL_ENCRYPTION_KEY` | 32 characters | AES-256-GCM database field encryption (e.g. SVG floor plans, license keys). Generate with: `openssl rand -hex 16` |
| `BETTER_AUTH_SECRET` | 32 characters | Better-Auth user session signature and cookie encryption. Generate with: `openssl rand -hex 32` |
| `JWT_SIGNING_KEY` | 64 characters | HMAC-SHA256 signature key for backend API access tokens. Generate with: `openssl rand -base64 48` |
| `HEIMDALL_AGENT_KEY` | 32 characters | Pre-shared key for edge IPC agents connecting to the central MQTT/gRPC telemetry broker. |

---

### 4.3 Database & Cache Configuration

| Variable | Staging Example | Production Example | Description |
| :--- | :--- | :--- | :--- |
| `POSTGRES_DB` | `heimdall_staging_db` | `heimdall_db` | Relational database schema name. |
| `POSTGRES_USER` | `heimdall_admin` | `heimdall_admin` | PostgreSQL admin username. |
| `POSTGRES_PASSWORD` | Strong password | Cryptographically random password | PostgreSQL master user password. |
| `DATABASE_URL` | `postgresql://...:5432/heimdall_staging_db` | `postgresql://...:5432/heimdall_db?sslmode=require` | Connection URI used by Drizzle ORM and Nitro BFF. |
| `ConnectionStrings__DefaultConnection` | `Host=...;Database=heimdall_staging_db;...` | `Host=...;Database=heimdall_db;SslMode=Require;...` | Connection string used by ASP.NET Core EF Core. |
| `REDIS_PASSWORD` | Strong password | Cryptographically random password | In-memory spool cache auth secret. |
| `REDIS_URL` | `redis://:...@redis:6379` | `rediss://:...@redis:6379` | Cache connection URI for Nitro BFF. |
| `REDIS_CONNECTION_STRING` | `redis:6379,password=...` | `redis:6379,password=...,ssl=true` | StackExchange.Redis connection string for backend. |

---

### 4.4 Networking, Ports & Reverse Proxy

| Variable | Default Port | Description |
| :--- | :--- | :--- |
| `WEB_PORT` / `PORT` | `3000` | Nuxt 4 web application port. Reverse proxy terminates SSL and forwards here. |
| `BACKEND_HTTP_PORT` | `5099` | ASP.NET Core REST API & SignalR hub port. |
| `BACKEND_ALT_PORT` | `5001` | Secondary gRPC / cleartext telemetry ingestion port. |
| `MQTT_PORT` | `1883` | Embedded or external MQTT telemetry transport port. |
| `ALLOWED_ORIGINS` | Comma-separated list | Strict CORS policy domain allowlist (e.g. `https://heimdall.plant.corp`). |
| `BACKEND_API_URL` | `http://backend:5099` | Internal network URL used by the Nitro BFF to reach the .NET backend API. |
| `SIGNALR_HUB_URL` | `https://heimdall.plant.corp/hubs/maintenance` | Public or proxy URL for live maintenance web socket subscriptions. |

#### Recommended Nginx Reverse Proxy Snippet:
```nginx
server {
    listen 443 ssl http2;
    server_name heimdall.plant.corp;

    ssl_certificate /etc/heimdall/certs/server.crt;
    ssl_certificate_key /etc/heimdall/certs/server.key;

    # Frontend Web Dashboard & Nitro BFF
    location / {
        proxy_pass http://127.0.0.1:3000;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }

    # SignalR Real-Time Maintenance WebSockets
    location /hubs/ {
        proxy_pass http://127.0.0.1:5099/hubs/;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection "upgrade";
        proxy_set_header Host $host;
        proxy_cache_bypass $http_upgrade;
    }

    # REST API & gRPC Collectors
    location /api/ {
        proxy_pass http://127.0.0.1:5099/api/;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
    }
}
```

---

## 5. Verification & Health Monitoring

To verify any deployment:
```bash
# Check service topology and latency across all subsystems
just status

# Execute the staging verification suite
just test-staging

# Monitor live log streams
just logs backend
```

In production, health endpoints can be probed by external orchestrators (Kubernetes / Prometheus / Consul):
- **Web Frontend Probe**: `http://localhost:3000/` (HTTP 200)
- **Backend API Probe**: `http://localhost:5099/health` or `http://localhost:5099/api/v1/assets`
- **Database Probe**: `pg_isready -h localhost -p 5432 -U postgres`
- **Redis Probe**: `redis-cli -a <password> ping` (PONG)
