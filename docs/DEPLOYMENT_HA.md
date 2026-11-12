# Heimdall High Availability (HA) & Deployment Architecture

This document defines high availability clustering, frontend operation behind reverse proxies, shared session storage, and container orchestration guidelines, and states what the current code does not support yet.

---

## 1. High Availability Cluster Architecture

```
             CLIENTS / OT BROWSERS / KIOSK TERMINALS                  EDGE AGENTS /
                                |                                     FLEET SIMULATOR
                                v                                            |
  +-----------------------------------------------------------+              |
  | Reverse Proxy Layer (e.g. nginx, HAProxy, Traefik)        |              |
  | - TLS termination (TLS 1.3 recommended)                   |              | MQTT 5, TCP 1883
  | - Kiosk client certificates verified here and             |              | (not through the
  |   forwarded to the BFF in X-Client-Cert                   |              | HTTP proxy)
  | - / -> Nuxt nodes (load balanced)                         |              |
  | - /hubs/ -> backend SignalR hub                           |              |
  +-----------------------------+--------------------------+--+              |
                                |                          |                 |
              +-----------------+---------+                |                 |
              v                           v                | SignalR         |
  +------------------------+  +------------------------+   | /hubs/          |
  | Nuxt 4 / Nitro Node #1 |  | Nuxt 4 / Nitro Node #2 |   | maintenance     |
  | - SSR / BFF, port 3000 |  | - SSR / BFF, port 3000 |   |                 |
  | - In-memory stores:    |  | - In-memory stores:    |   |                 |
  |   not stateless yet    |  |   not stateless yet    |   |                 |
  | - Better-Auth: Drizzle |  | - Better-Auth: Drizzle |   |                 |
  |   -> auth schema       |  |   -> auth schema       |   |                 |
  +-----------+------------+  +-----------+------------+   |                 |
              |                           |                |                 |
              +-------------+-------------+                |                 |
                            | REST /api/v1, session bearer |                 |
                            v and X-Organization-Id        v                 v
  +------------------------------------------------------------------------------------------+
  | Heimdall Backend (.NET 10, single instance today)                                        |
  | - REST /api/v1, SignalR /hubs/maintenance (no Redis backplane yet)                       |
  | - Embedded MQTT 5 broker (MQTTnet, agent key) and telemetry ingestion                    |
  +-----------------+-----------------------------------------------+------------------------+
                    v                                               v
  +------------------------------------+    +------------------------------------------------+
  | Redis (HA)                         |    | PostgreSQL primary / replica                   |
  | - backend L2 cache: station        |    | - backend schema (EF Core)                     |
  |   snapshots, change-gate state,    |    | - auth schema (Better-Auth users, sessions,    |
  |   query results                    |    |   organizations)                               |
  +------------------------------------+    +------------------------------------------------+
```

---

## 2. Frontend State & Session Sharing

The target is a stateless Nuxt 4 / Nitro frontend; the current code is not there yet:
- **Session Tokens**: Better-Auth session tokens are stored in HTTP-only cookies and validated against the `auth.session` table in PostgreSQL (Drizzle), which every Nuxt node shares. The backend validates the same bearer token against that table and caches the result for 5 minutes (`BetterAuthHandler`). Redis is not involved.
- **In-memory BFF stores**: Nitro keeps several stores in process memory: the ticket store with its edit history, MFA enrollments, personal Copia keys, automation API keys, the AD governance fallback, asset templates and spare-part policies. They are neither shared between nodes nor persisted, so a second Nuxt node (even with sticky sessions) sees different data. Run a single Nuxt node until these move to the backend or a shared store.
- **WebSocket / SSE**: SignalR has no Redis backplane (`AddSignalR()` only), so a hub event reaches only the clients of the backend instance that sent it. Run a single backend instance, or add a backplane before scaling out.
- **Cache Storage**: Redis holds the backend's L2 cache (station snapshots, PLC change-gate state, cached query results). The Nitro BFF does not use Redis.

---

## 3. Kubernetes Probes & Container Orchestration

Each frontend container provides standard HTTP health and readiness endpoints:

```yaml
livenessProbe:
  httpGet:
    path: /api/health
    port: 3000
  initialDelaySeconds: 10
  periodSeconds: 15

readinessProbe:
  httpGet:
    path: /api/ready
    port: 3000
  initialDelaySeconds: 5
  periodSeconds: 10
```
