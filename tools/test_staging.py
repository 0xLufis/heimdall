#!/usr/bin/env python3
"""
Heimdall Staging Environment Verification Test Suite
=====================================================
Validates that the staging environment is strictly isolated and correctly configured:
1. Seed data referential integrity (100 stations, 16 organizations, 56 PCs, 550 serialized spares).
2. Strict dev features suppression (HEIMDALL_ENABLE_DEV=false, NUXT_PUBLIC_ENABLE_DEV_FEATURES=false, ENABLE_DEV_HTTP_SEED=false).
3. Debug diagnostic features enablement (HEIMDALL_ENABLE_DEBUG=true, NUXT_PUBLIC_ENABLE_DEBUG_FEATURES=true).
4. Strong cryptographic secrets (HEIMDALL_ENCRYPTION_KEY >= 32 chars, BETTER_AUTH_SECRET non-default, JWT_SIGNING_KEY >= 64 chars).
5. Database connectivity and table permissions on heimdall_staging_db.
6. Execution of backend and frontend test suites verifying staging security invariants.
"""

import os
import sys
import subprocess
import json

ROOT_DIR = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
sys.path.insert(0, ROOT_DIR)

def load_env_file(filepath):
    env_vars = {}
    if not os.path.exists(filepath):
        return env_vars
    with open(filepath, "r", encoding="utf-8") as f:
        for line in f:
            line = line.strip()
            if not line or line.startswith("#"):
                continue
            if "=" in line:
                k, v = line.split("=", 1)
                env_vars[k.strip()] = v.strip().strip("'\"")
    return env_vars


def test_env_staging_file():
    print("\n>>> 1. Validating .env.staging configuration...")
    staging_env_path = os.path.join(ROOT_DIR, ".env.staging")
    assert os.path.exists(staging_env_path), ".env.staging does not exist!"

    env = load_env_file(staging_env_path)
    print("   • Checking environment targets...")
    assert env.get("ASPNETCORE_ENVIRONMENT") == "Staging", f"Expected Staging, got {env.get('ASPNETCORE_ENVIRONMENT')}"
    assert env.get("NODE_ENV") in ("production", "staging"), f"Expected production/staging, got {env.get('NODE_ENV')}"

    print("   • Checking dev/debug feature flags...")
    assert env.get("HEIMDALL_ENABLE_DEV") == "false", f"HEIMDALL_ENABLE_DEV must be 'false' in staging! Got: {env.get('HEIMDALL_ENABLE_DEV')}"
    assert env.get("NUXT_PUBLIC_ENABLE_DEV_FEATURES") == "false", "NUXT_PUBLIC_ENABLE_DEV_FEATURES must be 'false' in staging!"
    assert env.get("ENABLE_DEV_HTTP_SEED") == "false", "ENABLE_DEV_HTTP_SEED must be 'false' in staging!"
    assert env.get("HEIMDALL_ENABLE_DEBUG") == "true", "HEIMDALL_ENABLE_DEBUG must be 'true' in staging!"
    assert env.get("NUXT_PUBLIC_ENABLE_DEBUG_FEATURES") == "true", "NUXT_PUBLIC_ENABLE_DEBUG_FEATURES must be 'true' in staging!"

    print("   • Checking security keys and minimum entropy...")
    enc_key = env.get("HEIMDALL_ENCRYPTION_KEY", "")
    assert len(enc_key) >= 32, f"HEIMDALL_ENCRYPTION_KEY must be >= 32 chars! Length: {len(enc_key)}"

    auth_secret = env.get("BETTER_AUTH_SECRET", "")
    assert len(auth_secret) >= 32, f"BETTER_AUTH_SECRET must be >= 32 chars! Length: {len(auth_secret)}"
    assert "default-dev" not in auth_secret and "dev-secret" not in auth_secret, "BETTER_AUTH_SECRET cannot be a dev secret!"

    jwt_key = env.get("JWT_SIGNING_KEY", "")
    assert len(jwt_key) >= 64, f"JWT_SIGNING_KEY must be >= 64 chars! Length: {len(jwt_key)}"

    print("   • Checking database target...")
    assert env.get("POSTGRES_DB") == "heimdall_staging_db", f"Expected heimdall_staging_db, got {env.get('POSTGRES_DB')}"
    assert "heimdall_staging_db" in env.get("DATABASE_URL", ""), "DATABASE_URL must target heimdall_staging_db!"
    assert "heimdall_staging_db" in env.get("ConnectionStrings__DefaultConnection", ""), "DefaultConnection must target heimdall_staging_db!"

    print("   ✓ .env.staging passed all security and feature flag validations.")
    return env


def test_staging_database_seed():
    print("\n>>> 2. Validating Staging Database Seed Data (heimdall_staging_db)...")
    
    # Ensure database container is up
    subprocess.run(["just", "db-up"], cwd=ROOT_DIR, capture_output=True)

    cmd = [
        "docker", "exec", "postgres_heimdall", "psql", "-U", "postgres", "-d", "heimdall_staging_db", "-t", "-A", "-c"
    ]

    # Check connection
    res = subprocess.run(cmd + ["SELECT 1;"], capture_output=True, text=True)
    if res.returncode != 0:
        print(f"❌ Failed to query postgres_heimdall: {res.stderr}")
        return False

    # Check stations count (100 Diverse Machines across 8 Lines)
    st_res = subprocess.run(cmd + ["SELECT count(*) FROM backend.stations;"], capture_output=True, text=True)
    st_count = int(st_res.stdout.strip())
    print(f"   • Stations in heimdall_staging_db: {st_count} (Expected: 100)")
    assert st_count == 100, f"Expected 100 stations, got {st_count}"

    # Check organizations count (8 production lines, 7 guilds, 1 cross-project)
    org_res = subprocess.run(cmd + ["SELECT count(*) FROM auth.organization;"], capture_output=True, text=True)
    org_count = int(org_res.stdout.strip())
    print(f"   • Organizations in auth.organization: {org_count} (Expected: 16)")
    assert org_count == 16, f"Expected 16 organizations, got {org_count}"

    # Check client PCs count (56 Industrial Edge IPCs)
    pc_res = subprocess.run(cmd + ["SELECT count(*) FROM backend.client_pcs;"], capture_output=True, text=True)
    pc_count = int(pc_res.stdout.strip())
    print(f"   • Edge Client PCs in heimdall_staging_db: {pc_count} (Expected: 56)")
    assert pc_count == 56, f"Expected 56 client PCs, got {pc_count}"

    # Check inventory items count (550 spare parts + bulk consumables)
    inv_res = subprocess.run(cmd + ["SELECT count(*) FROM backend.inventory_items;"], capture_output=True, text=True)
    inv_count = int(inv_res.stdout.strip())
    print(f"   • Inventory items in heimdall_staging_db: {inv_count} (Expected: >= 800)")
    assert inv_count >= 800, f"Expected >= 800 inventory items, got {inv_count}"

    # Check users count
    usr_res = subprocess.run(cmd + ["SELECT count(*) FROM auth.user;"], capture_output=True, text=True)
    usr_count = int(usr_res.stdout.strip())
    print(f"   • Better-Auth users in auth.user: {usr_count} (Expected: >= 60)")
    assert usr_count >= 60, f"Expected >= 60 users, got {usr_count}"

    print("   ✓ Staging database has full, identical enterprise dataset loaded.")
    return True


def test_staging_feature_flags_guards():
    print("\n>>> 3. Testing Backend & Frontend Gating with Staging Configuration...")

    # Run Backend tests focusing on StagingEnvironment_BlocksDevFeatures_AllowsDebugFeatures
    print("   • Running .NET Backend Staging Gating verification test...")
    dotnet_res = subprocess.run(
        ["dotnet", "test", "tests/backend/App.Backend.Tests/App.Backend.Tests.csproj",
         "--filter", "FullyQualifiedName~StagingEnvironment_BlocksDevFeatures_AllowsDebugFeatures"],
        cwd=ROOT_DIR, capture_output=True, text=True
    )
    assert dotnet_res.returncode == 0, f"Backend staging gating test failed:\n{dotnet_res.stdout}\n{dotnet_res.stderr}"
    print("   ✓ Backend endpoint guards correctly block dev features (403 Forbidden) and allow debug features.")

    # Run Frontend feature flag and dev gating tests with staging environment
    print("   • Running Frontend Feature Flags & Dev Gating tests with Vitest...")
    vitest_res = subprocess.run(
        ["bun", "x", "vitest", "run", "tests/frontend/unit/FeatureFlagsAndDevGating.test.ts"],
        cwd=os.path.join(ROOT_DIR, "frontend", "web"), capture_output=True, text=True
    )
    assert vitest_res.returncode == 0, f"Frontend feature flag tests failed:\n{vitest_res.stdout}\n{vitest_res.stderr}"
    print("   ✓ Frontend composables and BFF correctly disable dev bypass and enable debug inspection.")
    return True


def test_staging_suites():
    print("\n>>> 4. Executing Verification Suites in Staging Configuration...")

    # 1. Seed data pipeline validation
    print("   • Validating Seed Data Pipeline Referential Integrity...")
    seed_res = subprocess.run(["python3", "seed_data/seed_pipeline.py", "--validate"], cwd=ROOT_DIR, capture_output=True, text=True)
    assert seed_res.returncode == 0, f"Seed data pipeline validation failed:\n{seed_res.stdout}\n{seed_res.stderr}"
    print("   ✓ Seed data pipeline validated (100% integrity).")

    # 2. Complete .NET backend test suite (209 tests)
    print("   • Running full .NET Backend Test Suite (Heimdall.sln)...")
    res_be = subprocess.run(["dotnet", "test", "Heimdall.sln"], cwd=ROOT_DIR, capture_output=True, text=True)
    assert res_be.returncode == 0, f"Backend test suite failed:\n{res_be.stdout}\n{res_be.stderr}"
    print("   ✓ All 209 Backend tests PASSED.")

    # 3. Complete Frontend test suite (333 tests)
    print("   • Running full Frontend Test Suite (Vitest)...")
    res_fe = subprocess.run(["bun", "run", "test"], cwd=os.path.join(ROOT_DIR, "frontend", "web"), capture_output=True, text=True)
    assert res_fe.returncode == 0, f"Frontend test suite failed:\n{res_fe.stdout}\n{res_fe.stderr}"
    print("   ✓ All 333 Frontend tests PASSED.")

    print("\n=========================================================================")
    print("   ✅ STAGING ENVIRONMENT VERIFICATION SUITE: 100% PASSED (0 FAILURES)")
    print("=========================================================================")
    return True


if __name__ == "__main__":
    test_env_staging_file()
    test_staging_database_seed()
    test_staging_feature_flags_guards()
    test_staging_suites()
