# Heimdall High Availability (HA) & Deployment Architecture

This document defines high availability clustering, stateless frontend operation behind reverse proxies, Redis session synchronization, and container orchestration guidelines.

---

## 1. High Availability Cluster Architecture

```
                                      CLIENTS / OT BROWSERS
                                                |
                                                v
                            +---------------------------------------+
                            | Reverse Proxy Layer (HAProxy / Traefik)|
                            | - SSL Termination (TLS 1.3)           |
                            | - mTLS Client Certificate Passthrough |
                            | - Path-based Routing / Load Balancing |
                            +-------------------+-------------------+
                                                |
                     +--------------------------+--------------------------+
                     |                                                     |
                     v                                                     v
       +----------------------------+                        +----------------------------+
       | Nuxt 4 / Nitro Node #1     |                        | Nuxt 4 / Nitro Node #2     |
       | - Stateless Web SSR / BFF  |                        | - Stateless Web SSR / BFF  |
       | - Port 3000                |                        | - Port 3000                |
       +--------------+-------------+                        +--------------+-------------+
                      |                                                     |
                      +--------------------------+--------------------------+
                                                 |
                                                 v
                                    +--------------------------+
                                    | Redis Cluster (HA)       |
                                    | - Distributed Session    |
                                    | - PubSub / Telemetry Hub |
                                    +--------------------------+
                                                 |
                                                 v
                                    +--------------------------+
                                    | PostgreSQL Master/Replica|
                                    | - TimescaleDB Telemetry  |
                                    +--------------------------+
```

---

## 2. Stateless Frontend & Redis Session Sharing

The Nuxt 4 / Nitro frontend operates in a strictly stateless configuration:
- **Session Tokens**: Better-Auth session tokens are stored in HTTP-only cookies and validated against the shared Redis cluster.
- **WebSocket / SSE**: SignalR and real-time event streams utilize Redis backplane for horizontal message fanout.
- **Cache Storage**: Telemetry cache, active directory mappings, and transient tokens are persisted in Redis.

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
