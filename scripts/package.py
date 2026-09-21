#!/usr/bin/env python3
"""
Heimdall Standalone Cross-Platform Packaging Tool
Generates standalone production distributions for Linux and Windows edge nodes.
"""

import os
import sys
import shutil
import subprocess
import argparse
from pathlib import Path

ROOT_DIR = Path(__file__).resolve().parent.parent
DIST_DIR = ROOT_DIR / "dist"
STAGING_DIR = DIST_DIR / "staging"


def run_cmd(cmd, cwd=None):
    print(f"--> Running: {' '.join(cmd)}")
    result = subprocess.run(cmd, cwd=cwd or str(ROOT_DIR), check=True)
    return result.returncode


def package_agent_linux():
    print("=== Packaging Agent for linux-x64 ===")
    out_dir = STAGING_DIR / "heimdall-agent-linux-x64"
    if out_dir.exists():
        shutil.rmtree(out_dir)
    out_dir.mkdir(parents=True, exist_ok=True)

    agent_proj = ROOT_DIR / "agent" / "App.Agent.Daemon" / "App.Agent.Daemon.csproj"
    run_cmd([
        "dotnet", "publish", str(agent_proj),
        "-c", "Release",
        "-r", "linux-x64",
        "--self-contained", "true",
        "-p:PublishSingleFile=true",
        "-p:PublishTrimmed=false",
        "-o", str(out_dir)
    ])

    # Rename executable to standard 'heimdall-agent'
    original_bin = out_dir / "App.Agent.Daemon"
    target_bin = out_dir / "heimdall-agent"
    if original_bin.exists():
        original_bin.rename(target_bin)
    os.chmod(target_bin, 0o755)

    # Copy service and configuration templates
    pkg_agent_dir = ROOT_DIR / "packaging" / "agent"
    for item in ["heimdall-agent.service", "install-service.sh", "uninstall-service.sh", "default-config.json", "agent.env.example"]:
        src = pkg_agent_dir / item
        if src.exists():
            dst = out_dir / item
            shutil.copy2(src, dst)
            if item.endswith(".sh"):
                os.chmod(dst, 0o755)

    # Create tarball
    archive_base = DIST_DIR / "heimdall-agent-linux-x64"
    shutil.make_archive(str(archive_base), "gztar", root_dir=str(STAGING_DIR), base_dir="heimdall-agent-linux-x64")
    print(f"✓ Created: {archive_base}.tar.gz")


def package_agent_windows():
    print("=== Packaging Agent for win-x64 ===")
    out_dir = STAGING_DIR / "heimdall-agent-win-x64"
    if out_dir.exists():
        shutil.rmtree(out_dir)
    out_dir.mkdir(parents=True, exist_ok=True)

    agent_proj = ROOT_DIR / "agent" / "App.Agent.Daemon" / "App.Agent.Daemon.csproj"
    run_cmd([
        "dotnet", "publish", str(agent_proj),
        "-c", "Release",
        "-r", "win-x64",
        "--self-contained", "true",
        "-p:PublishSingleFile=true",
        "-p:PublishTrimmed=false",
        "-o", str(out_dir)
    ])

    original_exe = out_dir / "App.Agent.Daemon.exe"
    target_exe = out_dir / "HeimdallAgent.exe"
    if original_exe.exists():
        original_exe.rename(target_exe)

    pkg_agent_dir = ROOT_DIR / "packaging" / "agent"
    for item in ["install-service.ps1", "uninstall-service.ps1", "default-config.json"]:
        src = pkg_agent_dir / item
        if src.exists():
            shutil.copy2(src, out_dir / item)

    archive_base = DIST_DIR / "heimdall-agent-win-x64"
    shutil.make_archive(str(archive_base), "zip", root_dir=str(STAGING_DIR), base_dir="heimdall-agent-win-x64")
    print(f"✓ Created: {archive_base}.zip")


def package_backend():
    print("=== Packaging Backend API ===")
    out_dir = STAGING_DIR / "heimdall-backend"
    if out_dir.exists():
        shutil.rmtree(out_dir)
    out_dir.mkdir(parents=True, exist_ok=True)

    backend_proj = ROOT_DIR / "backend" / "App.Backend.Api" / "App.Backend.Api.csproj"
    run_cmd([
        "dotnet", "publish", str(backend_proj),
        "-c", "Release",
        "-o", str(out_dir),
        "/p:UseAppHost=false"
    ])

    archive_base = DIST_DIR / "heimdall-backend"
    shutil.make_archive(str(archive_base), "gztar", root_dir=str(STAGING_DIR), base_dir="heimdall-backend")
    print(f"✓ Created: {archive_base}.tar.gz")


def package_frontend():
    print("=== Packaging Frontend Web App ===")
    out_dir = STAGING_DIR / "heimdall-frontend"
    if out_dir.exists():
        shutil.rmtree(out_dir)
    out_dir.mkdir(parents=True, exist_ok=True)

    web_dir = ROOT_DIR / "frontend" / "web"
    run_cmd(["bun", "run", "build"], cwd=str(web_dir))
    shutil.copytree(web_dir / ".output", out_dir / ".output")

    archive_base = DIST_DIR / "heimdall-frontend"
    shutil.make_archive(str(archive_base), "gztar", root_dir=str(STAGING_DIR), base_dir="heimdall-frontend")
    print(f"✓ Created: {archive_base}.tar.gz")


def main():
    parser = argparse.ArgumentParser(description="Heimdall Packaging Tool")
    parser.add_argument("target", nargs="?", default="agent", choices=["agent", "backend", "frontend", "all"],
                        help="Target to package (default: agent)")
    args = parser.parse_args()

    DIST_DIR.mkdir(parents=True, exist_ok=True)
    STAGING_DIR.mkdir(parents=True, exist_ok=True)

    if args.target in ("agent", "all"):
        package_agent_linux()
        package_agent_windows()
    if args.target in ("backend", "all"):
        package_backend()
    if args.target in ("frontend", "all"):
        package_frontend()

    print("\n=== Packaging Completed Successfully ===")


if __name__ == "__main__":
    main()
