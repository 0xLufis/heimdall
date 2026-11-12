#!/usr/bin/env python3
"""
Rotating log pipe for the dev scripts (standard library only).

Copies stdin to LOGFILE and keeps it bounded: the file is rotated when it reaches --max-mb
(LOGFILE.1 is the newest backup, LOGFILE.N the oldest, at most --backups of them) and backups older
than --max-age-days are deleted. By default an existing non-empty LOGFILE is rotated at start, so the
previous run of the service stays available as LOGFILE.1.

    some-service 2>&1 | python3 tools/logpipe.py .logs/frontend.log --max-mb 10 --backups 3

The pipe must never take the service down with it: SIGINT and SIGHUP are ignored (Ctrl-C in the
terminal that launched the dev stack must not cut the service's stdout), write errors (disk full,
directory removed) drop output instead of exiting, and it exits only at EOF, i.e. when the service
closed its output. Defaults can also come from HEIMDALL_LOG_MAX_MB, HEIMDALL_LOG_BACKUPS and
HEIMDALL_LOG_MAX_AGE_DAYS.
"""

import argparse
import os
import select
import signal
import sys
import time

CHUNK = 64 * 1024
POLL_INTERVAL = 2.0           # seconds between --pidfile checks while the pipe is idle
CHECK_INTERVAL = 2.0          # seconds between checks that LOGFILE is still the file being written
RETENTION_INTERVAL = 600.0    # seconds between age checks while data keeps flowing


def _env_number(name, default, cast):
    try:
        return cast(os.environ.get(name, default))
    except (TypeError, ValueError):
        return default


class RotatingLog:
    def __init__(self, path, max_bytes, backups, max_age_seconds, rotate_on_start=True):
        self.path = os.path.abspath(path)
        self.max_bytes = max(1024, int(max_bytes))
        self.backups = max(0, int(backups))
        self.max_age = max_age_seconds
        self.fd = None
        self.size = 0
        self.ino = None
        self.last_check = 0.0
        self.last_retention = 0.0
        self.dropped = 0
        os.makedirs(os.path.dirname(self.path), exist_ok=True)
        if rotate_on_start:
            try:
                if os.path.getsize(self.path) > 0:
                    self._shift_backups()
            except OSError:
                pass
        self._open()
        self._apply_retention()

    # -- file handling -------------------------------------------------------------------------
    def _open(self):
        try:
            os.makedirs(os.path.dirname(self.path), exist_ok=True)
            self.fd = os.open(self.path, os.O_WRONLY | os.O_CREAT | os.O_APPEND, 0o644)
            st = os.fstat(self.fd)
            self.size, self.ino = st.st_size, st.st_ino
        except OSError:
            self.fd = None
        self.last_check = time.monotonic()

    def _close(self):
        if self.fd is not None:
            try:
                os.close(self.fd)
            except OSError:
                pass
            self.fd = None

    def _backup(self, index):
        return "%s.%d" % (self.path, index)

    def _shift_backups(self):
        """LOGFILE -> LOGFILE.1 -> LOGFILE.2 ...; the oldest beyond --backups is deleted."""
        if self.backups == 0:
            try:
                os.unlink(self.path)
            except OSError:
                pass
            return
        for index in range(self.backups, 0, -1):
            src = self.path if index == 1 else self._backup(index - 1)
            try:
                os.replace(src, self._backup(index))
            except OSError:
                pass

    def _apply_retention(self):
        self.last_retention = time.monotonic()
        cutoff = time.time() - self.max_age
        for index in range(1, self.backups + 50):
            backup = self._backup(index)
            try:
                if index > self.backups or os.path.getmtime(backup) < cutoff:
                    os.unlink(backup)
            except OSError:
                if index > self.backups:
                    break

    def rotate(self):
        self._close()
        self._shift_backups()
        self._open()
        self._apply_retention()

    def _still_current(self):
        """Reopen when LOGFILE was deleted or replaced from outside (rm, a second logpipe rotating)."""
        now = time.monotonic()
        if now - self.last_check < CHECK_INTERVAL:
            return
        self.last_check = now
        try:
            st = os.stat(self.path)
            if st.st_ino == self.ino:
                self.size = st.st_size  # follows truncation from outside
                return
        except OSError:
            pass
        self._close()
        self._open()

    # -- writing -------------------------------------------------------------------------------
    def write(self, data):
        if self.fd is None:
            self._open()
        else:
            self._still_current()
        while data:
            room = self.max_bytes - self.size
            if len(data) <= room:
                self._write(data)
                break
            # The limit falls inside this chunk: end the file at a line break, continue in a new one.
            cut = data.rfind(b"\n", 0, max(0, room))   # the last line break that still fits
            if cut < 0:
                cut = data.find(b"\n")                  # else finish the current line first (small overshoot)
            if cut < 0:
                self._write(data)                       # no line break at all: rotation waits for one
                break
            self._write(data[:cut + 1])
            data = data[cut + 1:]
            self.rotate()
        if time.monotonic() - self.last_retention >= RETENTION_INTERVAL:
            self._apply_retention()

    def _write(self, data):
        if self.fd is None:
            self.dropped += len(data)
            return
        view = memoryview(data)
        try:
            while view:
                written = os.write(self.fd, view)
                self.size += written
                view = view[written:]
        except OSError:
            # Disk full or file gone: drop rather than block or kill the service; reopen next time.
            self.dropped += len(view)
            self._close()

    def close(self):
        self._close()


def main(argv=None):
    parser = argparse.ArgumentParser(description="Copy stdin to a size- and age-bounded rotating log file.")
    parser.add_argument("logfile")
    parser.add_argument("--max-mb", type=float, default=_env_number("HEIMDALL_LOG_MAX_MB", 10.0, float),
                        help="rotate when the file reaches this size (default 10, env HEIMDALL_LOG_MAX_MB)")
    parser.add_argument("--backups", type=int, default=_env_number("HEIMDALL_LOG_BACKUPS", 3, int),
                        help="rotated files to keep (default 3, env HEIMDALL_LOG_BACKUPS)")
    parser.add_argument("--max-age-days", type=float, default=_env_number("HEIMDALL_LOG_MAX_AGE_DAYS", 7.0, float),
                        help="delete backups older than this (default 7, env HEIMDALL_LOG_MAX_AGE_DAYS)")
    parser.add_argument("--append", action="store_true", help="continue the existing file instead of rotating it at start")
    parser.add_argument("--pidfile", help="also exit when the service whose PID this file holds is gone (see ServiceWatch)")
    args = parser.parse_args(argv)

    # Only EOF ends the pipe; a terminal Ctrl-C or hang-up must not cut the service's output.
    for sig in (signal.SIGINT, signal.SIGHUP):
        signal.signal(sig, signal.SIG_IGN)
    signal.signal(signal.SIGTERM, lambda *_: sys.exit(0))

    log = RotatingLog(args.logfile, args.max_mb * 1024 * 1024, args.backups,
                      args.max_age_days * 86400, rotate_on_start=not args.append)
    watch = ServiceWatch(args.pidfile) if args.pidfile else None
    stdin = sys.stdin.fileno()
    try:
        while True:
            if watch is not None:
                try:
                    ready, _, _ = select.select([stdin], [], [], POLL_INTERVAL)
                except InterruptedError:
                    continue
                if not ready:
                    if watch.service_gone():
                        break
                    continue
            try:
                chunk = os.read(stdin, CHUNK)
            except InterruptedError:
                continue
            except OSError:
                break
            if not chunk:
                break
            log.write(chunk)
    finally:
        log.close()
    return 0


class ServiceWatch:
    """
    EOF alone is not enough: processes the service spawned can inherit its stdout and outlive it
    (`dotnet run` leaves MSBuild worker nodes with /nodeReuse:true running for minutes), keeping the pipe
    open. The dev scripts write the service PID to a PID file right after starting it; the first PID read
    from a file written after this pipe started is latched, and the pipe exits once that process is gone,
    or the file was removed (stopped) or names another process (restarted).
    """

    def __init__(self, path):
        self.path = path
        self.started = time.time()
        self.pid = None

    def _read(self):
        try:
            if os.path.getmtime(self.path) < self.started - 5:
                return None  # left over from an earlier run: not ours (yet)
            with open(self.path) as f:
                return int(f.read().strip() or 0) or None
        except (OSError, ValueError):
            return None

    def service_gone(self):
        current = self._read()
        if self.pid is None:
            self.pid = current
            return False
        if current != self.pid:
            return True
        try:
            os.kill(self.pid, 0)
        except ProcessLookupError:
            return True
        except PermissionError:
            pass
        return False


if __name__ == "__main__":
    sys.exit(main())
