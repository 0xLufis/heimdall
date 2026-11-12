#!/usr/bin/env python3
"""
Process control for the dev services started by the justfile (Linux /proc, standard library only).

Nothing here matches command lines: `pkill -f <pattern>` also hit unrelated processes whose command line
contained the text (tool shells, a running `dotnet build`/`dotnet test` of the same project).

Every dev service is started as `setsid <command> &` and its PID file holds that PID. The process is the
leader of a new session, and everything it spawns stays in that session (dotnet watch -> dotnet run -> the
app, MSBuild nodes, the compiler server, Nuxt/Vite workers), also after it has been reparented because its
parent died. Only a process that calls setsid() itself leaves it: tools/logpipe.py does, on purpose (it ends
at EOF). The kernel does not hand out a PID again while a session with that ID still has processes, so the
recorded PID identifies the session even after its leader is gone.

    devproc.py stop NAME --pidfile FILE [--ports 5099,5001,1883] [--grace 10]
        SIGTERM to every process of the recorded session; waits until they have exited, at most --grace
        seconds, but only 1 s once none of them listens on a TCP port any more (`dotnet watch` ignores
        SIGTERM, the app it runs does not); SIGKILL for whatever is left. Fallback: processes still listening
        on --ports (a backend started by hand, an instance orphaned by an older start) are stopped the same
        way, one PID at a time, never their process group. Never signals itself or its own ancestors.
    devproc.py running --pidfile FILE
        Exit 0 when the recorded session leader is alive, 1 otherwise.
    devproc.py ports-free PORTS [--service NAME]
        Exit 0 when nothing listens on PORTS, else name the holders and exit 1.
"""

import argparse
import os
import signal
import sys
import time

PID_START_SLACK = 5.0  # seconds a process may have started after its PID file was written (`echo $!` races)
SETTLE_AFTER_LISTENERS_GONE = 1.0


# -- /proc helpers -------------------------------------------------------------------------------------

def _stat(pid):
    """(state, ppid, pgid, sid, start_ticks) of a process, or None when it does not exist."""
    try:
        with open(f"/proc/{pid}/stat") as f:
            fields = f.read().rsplit(")", 1)[1].split()
        return fields[0], int(fields[1]), int(fields[2]), int(fields[3]), int(fields[19])
    except (OSError, IndexError, ValueError):
        return None


def alive(pid):
    st = _stat(pid)
    return st is not None and st[0] not in ("Z", "X")


def _boot_time():
    with open("/proc/stat") as f:
        for line in f:
            if line.startswith("btime "):
                return float(line.split()[1])
    return 0.0


def start_time(pid):
    """Process start as a Unix timestamp, or None."""
    st = _stat(pid)
    if st is None:
        return None
    return _boot_time() + st[4] / os.sysconf("SC_CLK_TCK")


def _all_pids():
    return [int(d) for d in os.listdir("/proc") if d.isdigit()]


def session_members(sid):
    return sorted(p for p in _all_pids() if (st := _stat(p)) and st[3] == sid and st[0] not in ("Z", "X"))


def cmdline(pid, limit=110):
    """For messages only; never used to select processes."""
    try:
        with open(f"/proc/{pid}/cmdline", "rb") as f:
            text = f.read().replace(b"\0", b" ").decode(errors="replace").strip()
    except OSError:
        return "?"
    return text if len(text) <= limit else text[: limit - 3] + "..."


def protected_pids():
    """This process and its ancestors (the shell or tool that ran the recipe)."""
    pids, pid = set(), os.getpid()
    while pid > 1 and pid not in pids:
        pids.add(pid)
        st = _stat(pid)
        if st is None:
            break
        pid = st[1]
    return pids


def _listening_inodes(ports=None):
    """{socket inode: port} of TCP sockets in LISTEN state, optionally only for the given ports."""
    result = {}
    for table in ("/proc/net/tcp", "/proc/net/tcp6"):
        try:
            with open(table) as f:
                next(f, None)
                for line in f:
                    parts = line.split()
                    if len(parts) < 10 or parts[3] != "0A":
                        continue
                    port = int(parts[1].rsplit(":", 1)[1], 16)
                    if ports is None or port in ports:
                        result[int(parts[9])] = port
        except OSError:
            pass
    return result


def _socket_inodes(pid):
    inodes = set()
    try:
        for fd in os.listdir(f"/proc/{pid}/fd"):
            try:
                target = os.readlink(f"/proc/{pid}/fd/{fd}")
            except OSError:
                continue
            if target.startswith("socket:["):
                inodes.add(int(target[8:-1]))
    except OSError:
        pass
    return inodes


def listeners(ports):
    """{port: [pid, ...]} for the given ports; a port held by a process we cannot inspect (another user,
    a container's port proxy) maps to an empty list."""
    wanted = _listening_inodes(set(ports))
    found = {port: [] for port in sorted(set(wanted.values()))}
    if not wanted:
        return found
    for pid in _all_pids():
        for inode in _socket_inodes(pid) & wanted.keys():
            if pid not in found[wanted[inode]]:
                found[wanted[inode]].append(pid)
    return found


def _launchers(pid, protected, depth=4):
    """Ancestors of pid in its own session, without the session leader (an interactive shell) and protected."""
    st = _stat(pid)
    result = set()
    while st is not None and depth > 0:
        parent = st[1]
        pst = _stat(parent)
        if parent <= 1 or pst is None or pst[3] != st[3] or parent == pst[3] or parent in protected:
            break
        result.add(parent)
        st, depth = pst, depth - 1
    return result


def _listens_on(pids, ports):
    """Whether any of pids listens on one of ports (on any TCP port when ports is empty)."""
    inodes = _listening_inodes(set(ports) if ports else None)
    return any(_socket_inodes(p) & inodes.keys() for p in pids)


# -- PID file ------------------------------------------------------------------------------------------

def recorded(pidfile):
    """
    What the PID file stands for: ("session", sid), ("pid", pid) for a PID file whose process is not a
    session leader (written by something else than the justfile), or None (no file, stale, or the PID was
    reused by a process that started after the file was written).
    """
    try:
        with open(pidfile) as f:
            pid = int(f.read().strip() or 0)
        written = os.path.getmtime(pidfile)
    except (OSError, ValueError):
        return None
    if pid <= 1:
        return None
    st = _stat(pid)
    if st is None or st[0] in ("Z", "X"):
        # Leader gone; whatever is left of its session is still ours (the session keeps the PID reserved).
        return ("session", pid) if session_members(pid) else None
    started = start_time(pid)
    if started is not None and started > written + PID_START_SLACK:
        return None  # the PID now belongs to an unrelated, newer process
    return ("session", pid) if st[3] == pid else ("pid", pid)


def is_running(pidfile):
    rec = recorded(pidfile)
    return rec is not None and alive(rec[1])


# -- stopping ------------------------------------------------------------------------------------------

def _signal(pid, sig):
    try:
        os.kill(pid, sig)
        return True
    except (ProcessLookupError, PermissionError):
        return False


def terminate(collect, grace, protected, ports=()):
    """
    SIGTERM everything collect() returns (re-evaluated while waiting, so late children are caught), wait
    until it is gone (at most `grace` s; SETTLE_AFTER_LISTENERS_GONE s once none of them listens on `ports`
    any more: the servers have shut down, what is left is tooling such as dotnet watch, which ignores
    SIGTERM), then SIGKILL the rest. Returns (pids signalled, pids that needed SIGKILL).
    """
    signalled, killed = set(), set()
    start = time.monotonic()
    listeners_gone_at = None
    while True:
        pids = [p for p in collect() if p not in protected]
        for pid in pids:
            if pid not in signalled and _signal(pid, signal.SIGTERM):
                signalled.add(pid)
        if not pids:
            return signalled, killed
        now = time.monotonic()
        if listeners_gone_at is None and not _listens_on(pids, ports):
            listeners_gone_at = now
        if now - start >= grace or (listeners_gone_at is not None and now - listeners_gone_at >= SETTLE_AFTER_LISTENERS_GONE):
            break
        time.sleep(0.05)
    deadline = time.monotonic() + 3
    while time.monotonic() < deadline:
        pids = [p for p in collect() if p not in protected]
        if not pids:
            break
        for pid in pids:
            if _signal(pid, signal.SIGKILL):
                killed.add(pid)
        time.sleep(0.05)
    return signalled, killed


def stop(name, pidfile, ports, grace, log=print):
    protected = protected_pids()
    rec = recorded(pidfile)
    summary = []
    if rec is not None:
        kind, ident = rec
        collect = (lambda: session_members(ident)) if kind == "session" else (lambda: [ident] if alive(ident) else [])
        signalled, killed = terminate(collect, grace, protected, ports)
        if signalled:
            summary.append(f"{kind} {ident}: {len(signalled)} process(es)" + (f", {len(killed)} needed SIGKILL" if killed else ""))
    try:
        if os.path.exists(pidfile) and (rec is None or not any(alive(p) for p in session_members(rec[1]))):
            os.unlink(pidfile)
    except OSError:
        pass

    # Fallback: whoever still listens on the service's ports (only that process, not its group).
    held = listeners(ports) if ports else {}
    owners = sorted({p for pids in held.values() for p in pids} - protected)
    foreign = [port for port, pids in held.items() if not pids]
    if owners:
        launchers = set()
        for pid in owners:
            log(f"  port {', '.join(str(p) for p, ps in held.items() if pid in ps)} still served by PID {pid} ({cmdline(pid)}): stopping it")
            launchers |= _launchers(pid, protected)
        signalled, killed = terminate(lambda: [p for p in owners if alive(p)], grace, protected, ports)
        summary.append(f"port owners {owners}" + (f" ({len(killed)} needed SIGKILL)" if killed else ""))
        time.sleep(0.5)
        for pid in sorted(p for p in launchers if alive(p)):
            log(f"  note: PID {pid} ({cmdline(pid)}) started it and is still running (not started by this recipe);"
                " a dotnet watch would start the service again on the next file change")
    for port in foreign:
        log(f"  port {port} is held by a process of another user or a container; not touched")
    log(f"{name} stopped" + (f" ({'; '.join(summary)})." if summary else " (nothing was running)."))
    return 0


def ports_free(ports, service, log=print):
    held = listeners(ports)
    if not held:
        return 0
    for port, pids in held.items():
        who = ", ".join(f"PID {p} ({cmdline(p)})" for p in pids) or "a process of another user or a container"
        log(f"✗ Port {port} is already in use by {who}.")
    log(f"  Not starting {service}: run `just stop {service}` (stops the owner of these ports as well) or free the port.")
    return 1


def _ports(text):
    return [int(p) for p in text.replace(" ", "").split(",") if p] if text else []


def main(argv=None):
    parser = argparse.ArgumentParser(description="Stop and inspect the justfile's dev services by session and port.")
    sub = parser.add_subparsers(dest="cmd", required=True)
    p_stop = sub.add_parser("stop")
    p_stop.add_argument("name")
    p_stop.add_argument("--pidfile", required=True)
    p_stop.add_argument("--ports", default="")
    p_stop.add_argument("--grace", type=float, default=10.0)
    p_run = sub.add_parser("running")
    p_run.add_argument("--pidfile", required=True)
    p_free = sub.add_parser("ports-free")
    p_free.add_argument("ports")
    p_free.add_argument("--service", default="the service")
    args = parser.parse_args(argv)
    if args.cmd == "stop":
        return stop(args.name, args.pidfile, _ports(args.ports), args.grace)
    if args.cmd == "running":
        return 0 if is_running(args.pidfile) else 1
    return ports_free(_ports(args.ports), args.service)


if __name__ == "__main__":
    sys.exit(main())
