#!/usr/bin/env python3
"""
Heimdall Standalone Cross-Platform Packaging Tool
Generates standalone production distributions for Linux and Windows edge nodes.
Supports Authenticode code signing, semver archive naming, and SHA256SUMS manifests.
"""

import os
import sys
import shutil
import hashlib
import subprocess
import argparse
from pathlib import Path

ROOT_DIR = Path(__file__).resolve().parent.parent
DIST_DIR = ROOT_DIR / "dist"
STAGING_DIR = DIST_DIR / "staging"
VERSION = os.environ.get("VERSION", "1.0.0")


def run_cmd(cmd, cwd=None):
    print(f"--> Running: {' '.join(cmd)}")
    result = subprocess.run(cmd, cwd=cwd or str(ROOT_DIR), check=True)
    return result.returncode


def sign_windows_binary(bin_path: Path):
    cert_file = os.environ.get("SIGN_CERT_FILE")
    cert_pass = os.environ.get("SIGN_CERT_PASSWORD", "")
    timestamp_url = os.environ.get("SIGN_TIMESTAMP_URL", "http://timestamp.digicert.com")

    if not cert_file or not os.path.isfile(cert_file):
        print("    [i] Code signing skipped: SIGN_CERT_FILE not specified or certificate file not found.")
        return

    print(f"--> Signing Windows binary: {bin_path}...")
    if shutil.which("osslsigncode"):
        signed_tmp = bin_path.with_suffix(".exe.signed")
        run_cmd([
            "osslsigncode", "sign",
            "-pkcs12", cert_file,
            "-pass", cert_pass,
            "-h", "sha256",
            "-ts", timestamp_url,
            "-in", str(bin_path),
            "-out", str(signed_tmp)
        ])
        signed_tmp.replace(bin_path)
        print("    ✓ Successfully signed with osslsigncode (SHA256 / Authenticode)")
    elif shutil.which("signtool"):
        run_cmd([
            "signtool", "sign",
            "/f", cert_file,
            "/p", cert_pass,
            "/fd", "SHA256",
            "/tr", timestamp_url,
            "/td", "SHA256",
            "/d", "Heimdall Edge Daemon",
            str(bin_path)
        ])
        print("    ✓ Successfully signed with signtool (SHA256 / Authenticode)")
    else:
        print("    [!] Warning: Neither osslsigncode nor signtool available on host. Binary left unsigned.")


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

    # Ensure canonical binary name heimdall-agent
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

    # Create tarballs (versioned and unversioned)
    versioned_base = DIST_DIR / f"heimdall-agent-v{VERSION}-linux-x64"
    unversioned_base = DIST_DIR / "heimdall-agent-linux-x64"
    shutil.make_archive(str(versioned_base), "gztar", root_dir=str(STAGING_DIR), base_dir="heimdall-agent-linux-x64")
    shutil.copy2(f"{versioned_base}.tar.gz", f"{unversioned_base}.tar.gz")
    print(f"✓ Created: {versioned_base}.tar.gz")


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

    # Ensure canonical binary name heimdall-agent.exe
    original_exe = out_dir / "App.Agent.Daemon.exe"
    target_exe = out_dir / "heimdall-agent.exe"
    if original_exe.exists():
        original_exe.rename(target_exe)

    # Sign binary if configured
    sign_windows_binary(target_exe)

    # Provide HeimdallAgent.exe alias for backwards compatibility
    shutil.copy2(target_exe, out_dir / "HeimdallAgent.exe")

    pkg_agent_dir = ROOT_DIR / "packaging" / "agent"
    for item in ["install-service.ps1", "uninstall-service.ps1", "default-config.json"]:
        src = pkg_agent_dir / item
        if src.exists():
            shutil.copy2(src, out_dir / item)

    versioned_base = DIST_DIR / f"heimdall-agent-v{VERSION}-win-x64"
    unversioned_base = DIST_DIR / "heimdall-agent-win-x64"
    shutil.make_archive(str(versioned_base), "zip", root_dir=str(STAGING_DIR), base_dir="heimdall-agent-win-x64")
    shutil.copy2(f"{versioned_base}.zip", f"{unversioned_base}.zip")
    print(f"✓ Created: {versioned_base}.zip")


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

    versioned_base = DIST_DIR / f"heimdall-backend-v{VERSION}"
    unversioned_base = DIST_DIR / "heimdall-backend"
    shutil.make_archive(str(versioned_base), "gztar", root_dir=str(STAGING_DIR), base_dir="heimdall-backend")
    shutil.copy2(f"{versioned_base}.tar.gz", f"{unversioned_base}.tar.gz")
    print(f"✓ Created: {versioned_base}.tar.gz")


def package_frontend():
    print("=== Packaging Frontend Web App ===")
    out_dir = STAGING_DIR / "heimdall-frontend"
    if out_dir.exists():
        shutil.rmtree(out_dir)
    out_dir.mkdir(parents=True, exist_ok=True)

    web_dir = ROOT_DIR / "frontend" / "web"
    run_cmd(["bun", "run", "build"], cwd=str(web_dir))
    shutil.copytree(web_dir / ".output", out_dir / ".output")

    versioned_base = DIST_DIR / f"heimdall-frontend-v{VERSION}"
    unversioned_base = DIST_DIR / "heimdall-frontend"
    shutil.make_archive(str(versioned_base), "gztar", root_dir=str(STAGING_DIR), base_dir="heimdall-frontend")
    shutil.copy2(f"{versioned_base}.tar.gz", f"{unversioned_base}.tar.gz")
    print(f"✓ Created: {versioned_base}.tar.gz")


def generate_sha256sums():
    print("--> Generating SHA256SUMS manifest...")
    manifest_path = DIST_DIR / "SHA256SUMS"
    entries = []
    for archive in sorted(DIST_DIR.glob("*")):
        if archive.is_file() and archive.name != "SHA256SUMS" and (archive.name.endswith(".tar.gz") or archive.name.endswith(".zip")):
            hasher = hashlib.sha256()
            with open(archive, "rb") as f:
                while chunk := f.read(65536):
                    hasher.update(chunk)
            entries.append(f"{hasher.hexdigest()}  {archive.name}")
    
    with open(manifest_path, "w") as f:
        f.write("\n".join(entries) + "\n")
    print(f"✓ Generated SHA256SUMS with {len(entries)} release archives.")


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

    generate_sha256sums()
    print("\n=== Packaging Completed Successfully ===")


if __name__ == "__main__":
    main()
