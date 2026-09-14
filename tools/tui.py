#!/usr/bin/env python3
"""
Heimdall Industrial OT Development Workspace - Interactive TUI Dashboard
Provides a live, reactive terminal console for managing and monitoring:
- PostgreSQL & Redis Database Containers
- ASP.NET Core Backend API & gRPC Telemetry Collector
- Nuxt 4 Web Frontend & Nitro BFF
- Linux Industrial Edge Agent Daemon
- Industrial Fleet Simulator
- Windows 10 LTSC Edge Agent Container (KVM, VNC, ADS, OPC UA, WinRM)

Zero external dependencies - standard library Python 3 (curses + sockets + threads).
"""

import os
import sys
import time
import socket
import urllib.request
import subprocess
import threading
from collections import deque
import signal

try:
    import curses
    HAS_CURSES = True
except ImportError:
    HAS_CURSES = False

ROOT_DIR = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
LOG_DIR = "/tmp/heimdall_logs"
PID_DIR = "/tmp/heimdall_dev_pids"

# Service definitions
SERVICES = [
    {
        "id": "postgres",
        "name": "PostgreSQL Database",
        "type": "Container",
        "host": "127.0.0.1",
        "port": 5432,
        "check_type": "tcp",
        "log_path": os.path.join(LOG_DIR, "postgres.log"),
        "url": "postgresql://localhost:5432/heimdall_dev_db",
        "desc": "PostgreSQL 18 DB with GIN indexes & SSL",
        "docker_service": "postgres",
    },
    {
        "id": "redis",
        "name": "Redis Spool Cache",
        "type": "Container",
        "host": "127.0.0.1",
        "port": 6379,
        "check_type": "tcp",
        "log_path": os.path.join(LOG_DIR, "redis.log"),
        "url": "redis://127.0.0.1:6379",
        "desc": "Redis 7.4 buffer & session cache",
        "docker_service": "redis",
    },
    {
        "id": "backend",
        "name": "Backend REST API (V1)",
        "type": ".NET 10",
        "host": "127.0.0.1",
        "port": 5099,
        "check_type": "http",
        "path": "/swagger/v1/swagger.json",
        "pid_file": os.path.join(PID_DIR, "backend.pid"),
        "log_path": "/tmp/heimdall-backend.log",
        "url": "http://localhost:5099/swagger",
        "desc": "ASP.NET Core 10 Web API & Controllers",
    },
    {
        "id": "grpc",
        "name": "gRPC Collector Stream",
        "type": ".NET 10",
        "host": "127.0.0.1",
        "port": 5001,
        "check_type": "tcp",
        "pid_file": os.path.join(PID_DIR, "backend.pid"),
        "log_path": "/tmp/heimdall-backend.log",
        "url": "grpc://localhost:5001",
        "desc": "Industrial binary telemetry ingestion",
    },
    {
        "id": "frontend",
        "name": "Web Frontend (Nuxt 4)",
        "type": "Node/Bun",
        "host": "127.0.0.1",
        "port": 3000,
        "check_type": "http",
        "path": "/",
        "pid_file": os.path.join(PID_DIR, "frontend.pid"),
        "log_path": "/tmp/heimdall-nuxt.log",
        "url": "http://localhost:3000",
        "desc": "Nuxt 4 + Vite HMR + Tailwind CSS",
    },
    {
        "id": "agent",
        "name": "Linux Edge Agent Daemon",
        "type": ".NET 10",
        "host": "127.0.0.1",
        "port": 5998,
        "check_type": "tcp",
        "pid_file": os.path.join(PID_DIR, "agent.pid"),
        "log_path": "/tmp/heimdall-agent.log",
        "url": "http://localhost:5998",
        "desc": "Local Linux Edge Collector & Daemon",
    },
    {
        "id": "simulator",
        "name": "Edge Fleet Simulator",
        "type": "Python",
        "host": "127.0.0.1",
        "port": 5055,
        "check_type": "tcp",
        "pid_file": os.path.join(PID_DIR, "simulator.pid"),
        "log_path": "/tmp/heimdall-simulator.log",
        "url": "http://localhost:5055/metrics",
        "desc": "Simulates 50+ plant IPCs & PLCs",
    },
    {
        "id": "windows_vnc",
        "name": "Windows Edge Agent (VNC)",
        "type": "KVM / Win10",
        "host": "127.0.0.1",
        "port": 8006,
        "check_type": "tcp",
        "log_path": "/tmp/heimdall-windows.log",
        "url": "http://localhost:8006",
        "desc": "Windows 10 LTSC KVM Docker Container",
        "is_windows": True,
    },
    {
        "id": "windows_ads",
        "name": "TwinCAT ADS Server",
        "type": "OT Protocol",
        "host": "127.0.0.1",
        "port": 48898,
        "check_type": "tcp",
        "log_path": "/tmp/heimdall-windows.log",
        "url": "ams://5.80.201.44.1.1:851:48898",
        "desc": "Beckhoff TwinCAT 3 ADS Simulation Server",
        "is_windows": True,
    },
    {
        "id": "windows_opc",
        "name": "Minimal OPC UA Server",
        "type": "OT Protocol",
        "host": "127.0.0.1",
        "port": 4840,
        "check_type": "tcp",
        "log_path": "/tmp/heimdall-windows.log",
        "url": "opc.tcp://127.0.0.1:4840",
        "desc": "Zero-dependency binary OPC UA endpoint",
        "is_windows": True,
    },
]

ENDPOINTS = [
    ("Web UI", "http://localhost:3000", "Production Dashboard"),
    ("Swagger API", "http://localhost:5099/swagger", "REST OpenAPI v1"),
    ("gRPC Ingestion", "localhost:5001", "Telemetry Spool Stream"),
    ("Windows VNC", "http://localhost:8006", "Win10 Desktop View"),
    ("Agent Config API", "http://localhost:5998", "Edge Agent Web UI"),
    ("TwinCAT ADS", "localhost:48898", "AMS NetId 5.80.201.44.1.1:851"),
    ("OPC UA", "opc.tcp://localhost:4840", "ns=2;s=Line1.* Monitored Nodes"),
]


class HeimdallTuiState:
    def __init__(self):
        self.services = [dict(s) for s in SERVICES]
        for s in self.services:
            s["online"] = False
            s["latency_ms"] = 0.0
            s["last_check"] = 0.0
            s["status_text"] = "CHECKING"
        self.selected_idx = 0
        self.log_buffers = {s["id"]: deque(maxlen=400) for s in self.services}
        self.active_log_id = "backend"
        self.is_paused = False
        self.fullscreen_logs = False
        self.show_help = False
        self.show_quit_dialog = False
        self.banner_msg = "Heimdall TUI Ready. Press [?] for help, [b] to detach."
        self.banner_time = time.time()
        self.start_time = time.time()
        self.running = True
        self.lock = threading.Lock()

    def set_banner(self, msg):
        with self.lock:
            self.banner_msg = msg
            self.banner_time = time.time()


STATE = HeimdallTuiState()


def check_tcp(host, port, timeout=0.8):
    t0 = time.time()
    try:
        with socket.create_connection((host, int(port)), timeout=timeout):
            return True, round((time.time() - t0) * 1000, 1)
    except Exception:
        return False, 0.0


def check_http(host, port, path="/", timeout=1.0):
    t0 = time.time()
    url = f"http://{host}:{port}{path}"
    try:
        req = urllib.request.Request(url, headers={"User-Agent": "Heimdall-TUI/1.0"})
        with urllib.request.urlopen(req, timeout=timeout) as resp:
            ok = resp.status in (200, 301, 302)
            return ok, round((time.time() - t0) * 1000, 1)
    except Exception:
        return False, 0.0


def health_check_worker():
    while STATE.running:
        for svc in STATE.services:
            if not STATE.running:
                break
            if svc["check_type"] == "http":
                ok, lat = check_http(svc["host"], svc["port"], svc.get("path", "/"))
            else:
                ok, lat = check_tcp(svc["host"], svc["port"])

            with STATE.lock:
                svc["online"] = ok
                svc["latency_ms"] = lat
                svc["last_check"] = time.time()
                if ok:
                    svc["status_text"] = "ONLINE"
                else:
                    if svc.get("is_windows"):
                        svc["status_text"] = "OFFLINE"
                    else:
                        svc["status_text"] = "OFFLINE"
        time.sleep(1.2)


def log_tail_worker():
    """Tails log files into per-service circular buffers."""
    file_offsets = {}
    while STATE.running:
        for svc in STATE.services:
            log_path = svc.get("log_path")
            if not log_path or not os.path.exists(log_path):
                continue
            try:
                curr_size = os.path.getsize(log_path)
                last_offset = file_offsets.get(log_path, max(0, curr_size - 4096))
                if curr_size > last_offset:
                    with open(log_path, "r", encoding="utf-8", errors="replace") as f:
                        f.seek(last_offset)
                        lines = f.readlines()
                        file_offsets[log_path] = f.tell()
                        if lines:
                            with STATE.lock:
                                buf = STATE.log_buffers[svc["id"]]
                                for line in lines:
                                    clean = line.rstrip("\r\n")
                                    if clean:
                                        buf.append(clean)
                elif curr_size < last_offset:
                    file_offsets[log_path] = 0
            except Exception:
                pass
        time.sleep(0.4)


def execute_action(cmd_args):
    """Executes a command asynchronously without blocking the UI."""
    def _run():
        try:
            res = subprocess.run(cmd_args, cwd=ROOT_DIR, capture_output=True, text=True)
            STATE.set_banner(f"Action '{' '.join(cmd_args)}' finished (code {res.returncode})")
        except Exception as e:
            STATE.set_banner(f"Action error: {e}")
    t = threading.Thread(target=_run, daemon=True)
    t.start()


def toggle_service(svc):
    sid = svc["id"]
    if svc.get("is_windows"):
        toggle_windows_agent()
        return

    if svc["online"]:
        STATE.set_banner(f"Stopping {svc['name']}...")
        execute_action(["./run_dev.sh", "stop", sid])
    else:
        STATE.set_banner(f"Starting {svc['name']}...")
        execute_action(["./run_dev.sh", "restart", sid])


def restart_service(svc):
    sid = svc["id"]
    if svc.get("is_windows"):
        restart_windows_agent()
        return
    STATE.set_banner(f"Restarting {svc['name']}...")
    execute_action(["./run_dev.sh", "restart", sid])


def toggle_windows_agent():
    # Check if windows agent is currently online
    win_online = any(s["online"] for s in STATE.services if s.get("is_windows"))
    if win_online:
        STATE.set_banner("Stopping Windows Agent container...")
        execute_action(["./run_dev.sh", "windows", "stop"])
    else:
        STATE.set_banner("Starting Windows Agent container (win-x64)...")
        execute_action(["./run_dev.sh", "windows", "start"])


def restart_windows_agent():
    STATE.set_banner("Restarting Windows Agent container...")
    execute_action(["./run_dev.sh", "windows", "restart"])


def run_tests_async():
    STATE.set_banner("Launching verification test suite in background...")
    def _run():
        res = subprocess.run(["python3", "tools/dev_manager.py", "test"], cwd=ROOT_DIR, capture_output=True, text=True)
        if res.returncode == 0:
            STATE.set_banner("Verification tests passed! (100% OK)")
        else:
            STATE.set_banner("Verification tests encountered failures. Check logs.")
    threading.Thread(target=_run, daemon=True).start()


def curses_tui(stdscr):
    # Initialize curses settings
    curses.curs_set(0)
    stdscr.nodelay(True)
    stdscr.timeout(100)

    # Initialize colors if available
    has_colors = curses.has_colors()
    if has_colors:
        curses.start_color()
        curses.use_default_colors()
        curses.init_pair(1, curses.COLOR_GREEN, -1)   # Online
        curses.init_pair(2, curses.COLOR_RED, -1)     # Offline
        curses.init_pair(3, curses.COLOR_YELLOW, -1)  # Warning / Starting
        curses.init_pair(4, curses.COLOR_CYAN, -1)    # Info / Headers
        curses.init_pair(5, curses.COLOR_BLUE, -1)    # Selection
        curses.init_pair(6, curses.COLOR_BLACK, curses.COLOR_WHITE) # Inverted
        curses.init_pair(7, curses.COLOR_MAGENTA, -1) # Accent

    COLOR_ONLINE = curses.color_pair(1) if has_colors else curses.A_BOLD
    COLOR_OFFLINE = curses.color_pair(2) if has_colors else curses.A_DIM
    COLOR_WARN = curses.color_pair(3) if has_colors else curses.A_BOLD
    COLOR_CYAN = curses.color_pair(4) if has_colors else curses.A_BOLD
    COLOR_HEADER = curses.color_pair(6) if has_colors else curses.A_REVERSE
    COLOR_ACCENT = curses.color_pair(7) if has_colors else curses.A_BOLD

    while STATE.running:
        try:
            max_y, max_x = stdscr.getmaxyx()
            if max_y < 15 or max_x < 60:
                stdscr.clear()
                stdscr.addstr(0, 0, f"Terminal window too small ({max_x}x{max_y}). Min: 60x15.")
                stdscr.refresh()
                key = stdscr.getch()
                if key in (ord('q'), ord('Q'), 27):
                    break
                time.sleep(0.1)
                continue

            stdscr.erase()

            # 1. Header Banner
            uptime_sec = int(time.time() - STATE.start_time)
            uptime_str = f"{uptime_sec // 3600:02d}:{(uptime_sec % 3600) // 60:02d}:{uptime_sec % 60:02d}"
            curr_time = time.strftime("%H:%M:%S")
            header_text = f"  HEIMDALL INDUSTRIAL OT WORKSPACE  |  UPTIME: {uptime_str}  |  {curr_time}  "
            
            # Count online services
            online_count = sum(1 for s in STATE.services if s["online"])
            total_count = len(STATE.services)
            status_summary = f"[{online_count}/{total_count} ONLINE]"

            header_line = header_text + status_summary.rjust(max_x - len(header_text) - 2)
            try:
                stdscr.attron(COLOR_HEADER | curses.A_BOLD)
                stdscr.addstr(0, 0, header_line[:max_x].ljust(max_x))
                stdscr.attroff(COLOR_HEADER | curses.A_BOLD)
            except curses.error:
                pass

            # 2. Main Layout Sizing
            services_h = min(len(STATE.services) + 4, max_y - 8)
            if STATE.fullscreen_logs:
                services_h = 0

            # Draw Services Table if not fullscreen logs
            curr_y = 1
            if not STATE.fullscreen_logs:
                box_title = " ── INDUSTRIAL OT SERVICES & HEALTH ─────────────────────────────────"
                try:
                    stdscr.attron(COLOR_CYAN)
                    stdscr.addstr(curr_y, 0, box_title[:max_x])
                    stdscr.attroff(COLOR_CYAN)
                except curses.error:
                    pass
                curr_y += 1

                # Table Header
                col_fmt = "  {:<3} {:<24} {:<12} {:<22} {:<9} {:<10}"
                tbl_hdr = col_fmt.format("#", "SERVICE NAME", "TYPE", "TARGET", "LATENCY", "STATUS")
                try:
                    stdscr.attron(curses.A_UNDERLINE | curses.A_BOLD)
                    stdscr.addstr(curr_y, 0, tbl_hdr[:max_x].ljust(max_x))
                    stdscr.attroff(curses.A_UNDERLINE | curses.A_BOLD)
                except curses.error:
                    pass
                curr_y += 1

                for idx, svc in enumerate(STATE.services):
                    if curr_y >= max_y - 6:
                        break
                    is_sel = (idx == STATE.selected_idx)
                    target = f"{svc['host']}:{svc['port']}"
                    lat_str = f"{svc['latency_ms']}ms" if svc['online'] else "---"
                    status_str = "● ONLINE" if svc['online'] else "○ OFFLINE"

                    line_str = col_fmt.format(f"[{idx+1}]", svc["name"], svc["type"], target, lat_str, status_str)

                    # Highlight row if selected
                    attr = curses.A_REVERSE if is_sel else curses.A_NORMAL
                    color = COLOR_ONLINE if svc["online"] else COLOR_OFFLINE

                    try:
                        if is_sel:
                            stdscr.attron(attr | curses.A_BOLD)
                            stdscr.addstr(curr_y, 0, line_str[:max_x].ljust(max_x))
                            stdscr.attroff(attr | curses.A_BOLD)
                        else:
                            prefix = line_str[:54]
                            stdscr.addstr(curr_y, 0, prefix[:max_x])
                            stdscr.attron(color | curses.A_BOLD)
                            stdscr.addstr(curr_y, 54, status_str)
                            stdscr.attroff(color | curses.A_BOLD)
                    except curses.error:
                        pass
                    curr_y += 1

            # 3. Log Stream / Details Pane
            log_title = f" ── LIVE LOG STREAM: {STATE.services[STATE.selected_idx]['name']} "
            if STATE.is_paused:
                log_title += "[PAUSED] "
            log_title += "──────────────────────────────────────────────────"
            try:
                stdscr.attron(COLOR_CYAN)
                stdscr.addstr(curr_y, 0, log_title[:max_x])
                stdscr.attroff(COLOR_CYAN)
            except curses.error:
                pass
            curr_y += 1

            log_h = max(2, max_y - curr_y - 2)
            active_id = STATE.services[STATE.selected_idx]["id"]
            with STATE.lock:
                buf = list(STATE.log_buffers.get(active_id, deque()))

            # Display last `log_h` lines
            visible_lines = buf[-log_h:] if len(buf) > log_h else buf
            for line in visible_lines:
                if curr_y >= max_y - 2:
                    break
                try:
                    clean_line = line[:max_x - 1]
                    # Format log colors
                    if "ERR" in clean_line or "fail" in clean_line.lower() or "error" in clean_line.lower():
                        stdscr.attron(COLOR_OFFLINE)
                        stdscr.addstr(curr_y, 0, clean_line)
                        stdscr.attroff(COLOR_OFFLINE)
                    elif "warn" in clean_line.lower():
                        stdscr.attron(COLOR_WARN)
                        stdscr.addstr(curr_y, 0, clean_line)
                        stdscr.attroff(COLOR_WARN)
                    elif "info" in clean_line.lower() or "ready" in clean_line.lower():
                        stdscr.attron(COLOR_ONLINE)
                        stdscr.addstr(curr_y, 0, clean_line)
                        stdscr.attroff(COLOR_ONLINE)
                    else:
                        stdscr.addstr(curr_y, 0, clean_line)
                except curses.error:
                    pass
                curr_y += 1

            # 4. Banner Line
            banner_y = max_y - 2
            try:
                banner_str = f" {STATE.banner_msg}"
                stdscr.attron(COLOR_ACCENT | curses.A_BOLD)
                stdscr.addstr(banner_y, 0, banner_str[:max_x].ljust(max_x))
                stdscr.attroff(COLOR_ACCENT | curses.A_BOLD)
            except curses.error:
                pass

            # 5. Hotkeys Footer
            footer_y = max_y - 1
            footer_text = " [Tab/↑↓]Select [s]Start/Stop [r]Restart [R]All [w]WinAgent [l]Logs [t]Test [b]Detach [q]Quit [?]Help"
            try:
                stdscr.attron(COLOR_HEADER | curses.A_BOLD)
                stdscr.addstr(footer_y, 0, footer_text[:max_x].ljust(max_x))
                stdscr.attroff(COLOR_HEADER | curses.A_BOLD)
            except curses.error:
                pass

            # 6. Overlays (Quit Dialog, Help Modal)
            if STATE.show_quit_dialog:
                draw_quit_modal(stdscr, max_y, max_x)
            elif STATE.show_help:
                draw_help_modal(stdscr, max_y, max_x)

            stdscr.refresh()

            # Handle user input
            key = stdscr.getch()
            if key != -1:
                handle_key(key)

        except Exception as ex:
            STATE.set_banner(f"TUI Render notice: {ex}")
            time.sleep(0.1)


def draw_quit_modal(stdscr, max_y, max_x):
    box_w = min(68, max_x - 4)
    box_h = 8
    start_y = max(1, (max_y - box_h) // 2)
    start_x = max(1, (max_x - box_w) // 2)

    for i in range(box_h):
        stdscr.addstr(start_y + i, start_x, " " * box_w, curses.A_REVERSE)

    title = "─── HEIMDALL DEV TERMINATION PROMPT ───"
    stdscr.addstr(start_y + 1, start_x + (box_w - len(title)) // 2, title, curses.A_REVERSE | curses.A_BOLD)
    
    msg1 = "Choose how to exit the development session:"
    stdscr.addstr(start_y + 3, start_x + 3, msg1, curses.A_REVERSE)

    opts = "  [q] Stop All & Quit   |   [b] Detach (Keep Running)   |   [c] Cancel"
    stdscr.addstr(start_y + 5, start_x + (box_w - len(opts)) // 2, opts, curses.A_REVERSE | curses.A_BOLD)


def draw_help_modal(stdscr, max_y, max_x):
    box_w = min(72, max_x - 4)
    box_h = 16
    start_y = max(1, (max_y - box_h) // 2)
    start_x = max(1, (max_x - box_w) // 2)

    for i in range(box_h):
        stdscr.addstr(start_y + i, start_x, " " * box_w, curses.A_REVERSE)

    title = "─── HEIMDALL WORKSPACE KEYBOARD SHORTCUTS ───"
    stdscr.addstr(start_y + 1, start_x + (box_w - len(title)) // 2, title, curses.A_REVERSE | curses.A_BOLD)

    shortcuts = [
        ("Tab / Up / Down / 1-9", "Navigate and select industrial service"),
        ("s / Space", "Toggle Start / Stop for highlighted service"),
        ("r", "Restart highlighted service with hot-reload"),
        ("R", "Restart ALL development services"),
        ("w", "Toggle Windows 10 LTSC Agent KVM container"),
        ("l", "Toggle Fullscreen logs vs split overview"),
        ("p", "Pause / Resume live log auto-scrolling"),
        ("c", "Clear current service log buffer"),
        ("t", "Run verification test suite in background"),
        ("b", "Detach to background (leave services online)"),
        ("q / Esc", "Open session quit / detach prompt"),
        ("? / h", "Close this help dialog"),
    ]

    for idx, (k, desc) in enumerate(shortcuts):
        line = f"  {k:<22} : {desc}"
        stdscr.addstr(start_y + 3 + idx, start_x + 2, line[:box_w - 4], curses.A_REVERSE)


def handle_key(key):
    if STATE.show_quit_dialog:
        if key in (ord('q'), ord('Q')):
            STATE.running = False
            # Stop all services
            subprocess.run(["./run_dev.sh", "stop"], cwd=ROOT_DIR)
        elif key in (ord('b'), ord('B'), ord('d'), ord('D')):
            # Detach - leave services running
            STATE.running = False
        elif key in (ord('c'), ord('C'), 27):
            STATE.show_quit_dialog = False
        return

    if STATE.show_help:
        if key in (ord('?'), ord('h'), ord('H'), 27, ord('q'), ord('Q'), ord(' ')):
            STATE.show_help = False
        return

    # Normal Navigation & Actions
    if key in (curses.KEY_UP, ord('k')):
        STATE.selected_idx = (STATE.selected_idx - 1) % len(STATE.services)
    elif key in (curses.KEY_DOWN, ord('j')):
        STATE.selected_idx = (STATE.selected_idx + 1) % len(STATE.services)
    elif key in (ord('\t'), 9):
        STATE.selected_idx = (STATE.selected_idx + 1) % len(STATE.services)
    elif ord('1') <= key <= ord('9'):
        idx = key - ord('1')
        if idx < len(STATE.services):
            STATE.selected_idx = idx
    elif key in (ord('s'), ord(' ')):
        svc = STATE.services[STATE.selected_idx]
        toggle_service(svc)
    elif key in (ord('r'),):
        svc = STATE.services[STATE.selected_idx]
        restart_service(svc)
    elif key in (ord('R'),):
        STATE.set_banner("Restarting all development services...")
        execute_action(["./run_dev.sh", "restart"])
    elif key in (ord('w'), ord('W')):
        toggle_windows_agent()
    elif key in (ord('l'), ord('L')):
        STATE.fullscreen_logs = not STATE.fullscreen_logs
    elif key in (ord('p'), ord('P')):
        STATE.is_paused = not STATE.is_paused
    elif key in (ord('c'), ord('C')):
        active_id = STATE.services[STATE.selected_idx]["id"]
        STATE.log_buffers[active_id].clear()
        STATE.set_banner(f"Cleared log buffer for {STATE.services[STATE.selected_idx]['name']}")
    elif key in (ord('t'), ord('T')):
        run_tests_async()
    elif key in (ord('b'), ord('B'), ord('d'), ord('D')):
        STATE.running = False
    elif key in (ord('q'), ord('Q'), 27):
        STATE.show_quit_dialog = True
    elif key in (ord('?'), ord('h'), ord('H')):
        STATE.show_help = True


def ansi_fallback_loop():
    """Fallback interactive loop for terminals without curses support."""
    print("\033[96mStarting Heimdall ANSI Console Monitor (Curses fallback)...\033[0m")
    try:
        while STATE.running:
            sys.stdout.write("\033[2J\033[H")
            uptime_sec = int(time.time() - STATE.start_time)
            print(f"\033[1;44;97m  HEIMDALL INDUSTRIAL OT WORKSPACE  |  UPTIME: {uptime_sec}s  \033[0m")
            print(f"{'#':<4} {'SERVICE':<26} {'TYPE':<12} {'TARGET':<20} {'STATUS'}")
            print("-" * 72)
            for idx, s in enumerate(STATE.services):
                stat = "\033[92m● ONLINE\033[0m" if s["online"] else "\033[91m○ OFFLINE\033[0m"
                target = f"{s['host']}:{s['port']}"
                print(f"[{idx+1}]  {s['name']:<26} {s['type']:<12} {target:<20} {stat}")
            print("-" * 72)
            print(f"\033[93m{STATE.banner_msg}\033[0m")
            print("\033[90mPress Ctrl+C to exit monitor (services remain online).\033[0m")
            time.sleep(2.0)
    except KeyboardInterrupt:
        print("\n\033[92mExited TUI. Services remain running in background.\033[0m")


def main():
    # Setup signal handlers
    def sig_handler(sig, frame):
        STATE.running = False

    signal.signal(signal.SIGINT, sig_handler)
    signal.signal(signal.SIGTERM, sig_handler)

    # Start background health checker & log tailer
    t_health = threading.Thread(target=health_check_worker, daemon=True)
    t_health.start()

    t_logs = threading.Thread(target=log_tail_worker, daemon=True)
    t_logs.start()

    # Pre-seed log buffers with recent lines if log files exist
    for svc in STATE.services:
        log_path = svc.get("log_path")
        if log_path and os.path.exists(log_path):
            try:
                with open(log_path, "r", encoding="utf-8", errors="replace") as f:
                    lines = f.readlines()[-50:]
                    for line in lines:
                        STATE.log_buffers[svc["id"]].append(line.rstrip("\r\n"))
            except Exception:
                pass

    if HAS_CURSES and sys.stdin.isatty() and sys.stdout.isatty() and os.environ.get("TERM") != "dumb":
        try:
            curses.wrapper(curses_tui)
        except Exception as e:
            print(f"Curses interface exited ({e}). Falling back to terminal output.")
    else:
        ansi_fallback_loop()

    # Final detachment message
    print("\n\033[1;32m✓ Detached from Heimdall TUI.\033[0m")
    print("Services remain running in background daemons and Docker containers.")
    print("Commands:")
    print("  • Reattach TUI:   ./run_dev.sh tui")
    print("  • Check health:   ./run_dev.sh status")
    print("  • Stop services:  ./run_dev.sh stop")
    print("  • Stream logs:    ./run_dev.sh logs <service>")


if __name__ == "__main__":
    main()
