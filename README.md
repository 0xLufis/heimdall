> [!WARNING]  
> This project is VERY early in development, a good portion is AI assisted or generated, havin said that I check the code AI writes, which for now is only intended to  work for a POC.

# Heimdall

This repository contains the source code for a Bachelor of Science (BSc) Thesis project developed for the University of Pécs, Faculty of Engineering and Information Technology (PTE MIK).

## Project Overview

Heimdall is an industrial management system designed to track, configure, and monitor client PCs operating in production environments (e.g., controlling assembly machines). 

To accommodate highly variable industrial hardware and software, the system leverages a hybrid relational and document-based database architecture. Core domain relationships are strictly structured, while highly dynamic data—such as specific hardware configurations, installed software packages, and custom user-defined data points (e.g., WMIC flags) are managed via native PostgreSQL JSONB columns.

## Architecture & Technology Stack

* **Backend:** .NET 9 Web API
* **Data Access:** Entity Framework Core 9 with Npgsql
* **Frontend:** Nuxt 4.3.1
* **Authentication:** Better-Auth (Server-side via Nuxt)
* **Database:** PostgreSQL 17.9 (Dockerized, SSL-enabled)

## Repository Structure

The repository is organized as a monorepo to separate concerns while sharing database schema definitions between the API and infrastructure tooling.

* `backend/App.Api/`: The .NET 9 Web API entry point and HTTP controllers.
* `backend/App.Infrastructure/`: Data access layers, repositories, and external service integrations.
* `shared/App.Shared/`: Domain entities, JSONB POCOs, and the Entity Framework Core DbContext. This acts as the single source of truth for the database schema.
* `infra/database/`: Docker Compose configurations, initialization scripts, and local SSL certificates for the PostgreSQL database.
* `frontend/`: The Nuxt 4

## Local Development Setup

### 1. Database Initialization
The PostgreSQL database is containerized and requires local SSL certificates and secrets to run.

Navigate to the database infrastructure directory:
```bash
cd infra/database
mkdir -p data logs certs secrets init
echo "postgres" > secrets/pg_user.txt
echo "dev_password" > secrets/pg_pw.txt
openssl req -new -x509 -days 365 -nodes -text -out certs/server.crt \
  -keyout certs/server.key -subj "/CN=localhost"

# Enforce strict permissions required by PostgreSQL
sudo chmod 600 certs/server.key
sudo chown 999:999 certs/server.key certs/server.crt
docker compose up -d

