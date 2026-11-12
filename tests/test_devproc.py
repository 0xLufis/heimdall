#!/usr/bin/env python3
"""tools/devproc.py: stop by setsid session + port owner, never by command-line text, never itself.

Run: python3 -m unittest tests/test_devproc.py   (Linux; no services needed, touches only processes it starts)
"""

import importlib.util
import os
import signal
import socket
import subprocess
import sys
import tempfile
import time
import unittest

ROOT_DIR = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
_spec = importlib.util.spec_from_file_location("devproc", os.path.join(ROOT_DIR, "tools", "devproc.py"))
devproc = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(devproc)

MARKER = "devproc-test-marker"
# A listening "app" that exits on SIGTERM (it resets the SIG_IGN inherited from the leader).
APP = (
    "import signal, socket, sys, time\n"
    "signal.signal(signal.SIGTERM, signal.SIG_DFL)\n"
    "s = socket.socket(); s.bind(('127.0.0.1', 0)); s.listen()\n"
    "open(sys.argv[1], 'w').write(str(s.getsockname()[1]))\n"
    "time.sleep(120)\n"
)
# A session leader that ignores SIGTERM, like dotnet watch, and runs the app.
LEADER = (
    "import signal, subprocess, sys, time\n"
    "signal.signal(signal.SIGTERM, signal.SIG_IGN)\n"
    "subprocess.Popen([sys.executable, '-c', sys.argv[1], sys.argv[2], sys.argv[3]])\n"
    "time.sleep(120)\n"
)


def wait_for(predicate, timeout=10.0):
    deadline = time.monotonic() + timeout
    while time.monotonic() < deadline:
        if predicate():
            return True
        time.sleep(0.05)
    return False


class DevProcTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.pidfile = os.path.join(self.tmp.name, "svc.pid")
        self.procs = []

    def tearDown(self):
        for p in self.procs:
            try:
                os.killpg(p.pid, signal.SIGKILL) if os.getpgid(p.pid) == p.pid else p.kill()
            except (ProcessLookupError, PermissionError):
                pass
            p.wait(timeout=5)
        self.tmp.cleanup()

    def start_session(self):
        """setsid'd leader + listening app, like `setsid dotnet watch ... &`; returns (leader pid, app port)."""
        portfile = os.path.join(self.tmp.name, "port")
        leader = subprocess.Popen([sys.executable, "-c", LEADER, APP, portfile, MARKER], start_new_session=True)
        self.procs.append(leader)
        with open(self.pidfile, "w") as f:
            f.write(str(leader.pid))
        self.assertTrue(wait_for(lambda: os.path.exists(portfile) and open(portfile).read()))
        return leader.pid, int(open(portfile).read())

    def test_stop_ends_the_whole_session_quickly_and_spares_lookalikes(self):
        sid, port = self.start_session()
        members = devproc.session_members(sid)
        self.assertEqual(len(members), 2)
        # Same command-line text, other session: what `pkill -f` used to hit.
        lookalike = subprocess.Popen([sys.executable, "-c", "import time; time.sleep(120)", MARKER])
        self.procs.append(lookalike)
        self.assertTrue(devproc.is_running(self.pidfile))

        started = time.monotonic()
        devproc.stop("svc", self.pidfile, [port], grace=8, log=lambda *_: None)
        elapsed = time.monotonic() - started

        self.assertEqual(devproc.session_members(sid), [])
        self.assertLess(elapsed, 4.0, "the SIGTERM-ignoring leader is killed shortly after the app is gone")
        self.assertFalse(os.path.exists(self.pidfile))
        self.assertIsNone(lookalike.poll(), "a process outside the session must survive")
        self.assertFalse(devproc.is_running(self.pidfile))

    def test_session_is_stopped_even_after_its_leader_died(self):
        sid, port = self.start_session()
        app = [p for p in devproc.session_members(sid) if p != sid][0]
        os.kill(sid, signal.SIGKILL)  # watcher gone, app orphaned (still in the session)
        self.procs[0].wait(timeout=5)
        self.assertTrue(devproc.alive(app))
        self.assertEqual(devproc.recorded(self.pidfile), ("session", sid))

        devproc.stop("svc", self.pidfile, [port], grace=5, log=lambda *_: None)
        self.assertTrue(wait_for(lambda: not devproc.alive(app), 2))

    def test_pid_file_older_than_its_process_is_ignored(self):
        proc = subprocess.Popen([sys.executable, "-c", "import time; time.sleep(120)"], start_new_session=True)
        self.procs.append(proc)
        with open(self.pidfile, "w") as f:
            f.write(str(proc.pid))
        old = time.time() - 600
        os.utime(self.pidfile, (old, old))  # written long before this process started: PID reused

        self.assertIsNone(devproc.recorded(self.pidfile))
        devproc.stop("svc", self.pidfile, [], grace=2, log=lambda *_: None)
        self.assertIsNone(proc.poll())

    def test_port_owner_fallback_stops_only_the_owner_and_never_this_process(self):
        portfile = os.path.join(self.tmp.name, "port")
        owner = subprocess.Popen([sys.executable, "-c", APP, portfile])
        self.procs.append(owner)
        self.assertTrue(wait_for(lambda: os.path.exists(portfile) and open(portfile).read()))
        port = int(open(portfile).read())
        own = socket.socket()
        own.bind(("127.0.0.1", 0))
        own.listen()
        own_port = own.getsockname()[1]
        try:
            self.assertEqual(devproc.listeners([port, own_port]), {port: [owner.pid], own_port: [os.getpid()]})
            messages = []
            devproc.stop("svc", self.pidfile, [port, own_port], grace=5, log=messages.append)
            self.assertTrue(wait_for(lambda: owner.poll() is not None, 3))
            self.assertTrue(any(f"PID {owner.pid}" in m for m in messages))
            self.assertEqual(devproc.listeners([own_port]), {own_port: [os.getpid()]})
        finally:
            own.close()

    def test_ports_free_reports_holders(self):
        own = socket.socket()
        own.bind(("127.0.0.1", 0))
        own.listen()
        port = own.getsockname()[1]
        try:
            messages = []
            self.assertEqual(devproc.ports_free([port], "svc", log=messages.append), 1)
            self.assertIn(f"PID {os.getpid()}", messages[0])
        finally:
            own.close()
        self.assertEqual(devproc.ports_free([port], "svc", log=lambda *_: None), 0)


if __name__ == "__main__":
    unittest.main()
