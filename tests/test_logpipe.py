#!/usr/bin/env python3
"""tools/logpipe.py: size rotation at line boundaries, backup count, age limit, rotate-on-start.

Run: python3 -m unittest tests/test_logpipe.py   (no services needed)
"""

import os
import subprocess
import sys
import tempfile
import time
import unittest

ROOT_DIR = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
LOGPIPE = os.path.join(ROOT_DIR, "tools", "logpipe.py")


def run_pipe(logfile, data, *args):
    subprocess.run([sys.executable, LOGPIPE, logfile, *args], input=data, check=True, timeout=30)


class LogPipeTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.log = os.path.join(self.tmp.name, "sub", "service.log")

    def tearDown(self):
        self.tmp.cleanup()

    def files(self):
        return sorted(os.listdir(os.path.dirname(self.log)))

    def test_rotates_by_size_at_line_boundaries_and_keeps_n_backups(self):
        lines = [("line %05d " % i + "x" * 90 + "\n").encode() for i in range(2000)]  # ~200 KB
        run_pipe(self.log, b"".join(lines), "--max-mb", str(16 / 1024), "--backups", "3")

        self.assertEqual(self.files(), ["service.log", "service.log.1", "service.log.2", "service.log.3"])
        for name in self.files():
            path = os.path.join(os.path.dirname(self.log), name)
            self.assertLessEqual(os.path.getsize(path), 16 * 1024)
            with open(path, "rb") as f:
                content = f.read()
            self.assertTrue(content.endswith(b"\n"), name + " must end at a line boundary")
            self.assertTrue(content.startswith(b"line "), name + " must start at a line boundary")
        with open(self.log, "rb") as f:
            self.assertTrue(f.read().endswith(lines[-1]))

    def test_rotates_the_previous_run_at_start_unless_append(self):
        run_pipe(self.log, b"first run\n")
        run_pipe(self.log, b"second run\n")
        with open(self.log) as f:
            self.assertEqual(f.read(), "second run\n")
        with open(self.log + ".1") as f:
            self.assertEqual(f.read(), "first run\n")

        run_pipe(self.log, b"third run\n", "--append")
        with open(self.log) as f:
            self.assertEqual(f.read(), "second run\nthird run\n")

    def test_exits_when_the_service_in_the_pidfile_is_gone_even_if_the_pipe_stays_open(self):
        os.makedirs(os.path.dirname(self.log))
        pidfile = os.path.join(self.tmp.name, "service.pid")
        service = subprocess.Popen(["sleep", "30"])
        with open(pidfile, "w") as f:
            f.write(str(service.pid))
        read_end, write_end = os.pipe()  # this test keeps write_end open, like a lingering MSBuild node
        pipe = subprocess.Popen([sys.executable, LOGPIPE, self.log, "--pidfile", pidfile], stdin=read_end)
        os.close(read_end)
        try:
            os.write(write_end, b"hello\n")
            time.sleep(2.5)  # one idle poll latches the PID
            self.assertIsNone(pipe.poll(), "must keep running while the service runs")
            service.kill()
            service.wait()
            self.assertEqual(pipe.wait(timeout=10), 0)
        finally:
            os.close(write_end)
            if pipe.poll() is None:
                pipe.kill()
            service.kill()
        with open(self.log) as f:
            self.assertEqual(f.read(), "hello\n")

    def test_deletes_backups_older_than_the_age_limit(self):
        os.makedirs(os.path.dirname(self.log))
        for index, age_days in ((1, 1), (2, 10), (3, 30)):
            path = "%s.%d" % (self.log, index)
            with open(path, "w") as f:
                f.write("old\n")
            stamp = time.time() - age_days * 86400
            os.utime(path, (stamp, stamp))
        run_pipe(self.log, b"new\n", "--append", "--max-age-days", "7", "--backups", "5")
        self.assertEqual(self.files(), ["service.log", "service.log.1"])


if __name__ == "__main__":
    unittest.main()
