#!/usr/bin/env python3
"""
Unit and integration test for Heimdall TUI actions and key handling.
Verifies that no action (like 'r', 'R', 's', 'w', resize, modals) causes
the TUI to crash or unexpectedly detach.
"""

import sys
import os
import time

ROOT_DIR = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
sys.path.insert(0, ROOT_DIR)

from tools.tui import STATE, handle_key, map_service_target, JUST_BIN, SERVICES

def test_tui_keys():
    print("=== Testing Heimdall TUI Keyboard Navigation and Action Stability ===")
    assert STATE.running, "STATE.running should initially be True"
    assert os.path.exists(JUST_BIN), f"Just binary should exist at {JUST_BIN}"

    # 1. Target mapping verification
    print(">> 1. Verifying service ID mapping to just recipes...")
    assert map_service_target("postgres") == "postgres"
    assert map_service_target("redis") == "redis"
    assert map_service_target("backend") == "backend"
    assert map_service_target("grpc") == "backend"
    assert map_service_target("frontend") == "frontend"
    assert map_service_target("agent") == "agent"
    assert map_service_target("simulator") == "simulator"
    assert map_service_target("windows_agent") == "windows"
    assert map_service_target("windows_vnc") == "windows"
    assert map_service_target("windows_ads") == "windows"
    assert map_service_target("windows_opc") == "windows"
    print("   ✓ All service targets mapped accurately.")

    # 2. Key navigation
    print(">> 2. Testing navigation keys (Down, Up, Tab, Numeric)...")
    initial_idx = STATE.selected_idx
    handle_key(ord('j'))  # Down
    assert STATE.selected_idx == (initial_idx + 1) % len(STATE.services)
    handle_key(ord('k'))  # Up
    assert STATE.selected_idx == initial_idx
    handle_key(ord('1'))  # Select item 1 (index 0)
    assert STATE.selected_idx == 0
    assert STATE.running, "TUI must remain running during navigation"
    print("   ✓ Navigation keys passed.")

    # 3. 'r' key on various services
    print(">> 3. Testing 'r' (restart highlighted service)...")
    for idx in [0, 1, 2, 3, 4, 5]: # postgres, redis, backend, grpc, frontend, windows
        STATE.selected_idx = idx
        svc = STATE.services[idx]
        print(f"   • Pressing 'r' on service '{svc['name']}' (id: {svc['id']})...")
        handle_key(ord('r'))
        assert STATE.running, f"TUI detached or terminated on 'r' for service {svc['id']}!"
        time.sleep(0.1)
    print("   ✓ 'r' key restart executed without crash or detach.")

    # 4. 'R' key (restart all services)
    print(">> 4. Testing 'R' (restart ALL development services)...")
    handle_key(ord('R'))
    assert STATE.running, "TUI detached or terminated on 'R' (restart all)!"
    assert "Restarting all development services" in STATE.banner_msg
    print("   ✓ 'R' key executed without crash or detach.")

    # 5. Service toggle 's'
    print(">> 5. Testing 's' (toggle start/stop)...")
    STATE.selected_idx = 0
    handle_key(ord('s'))
    assert STATE.running, "TUI detached on 's' toggle!"
    print("   ✓ 's' toggle executed without crash or detach.")

    # 6. Windows toggle 'w'
    print(">> 6. Testing 'w' (toggle windows container)...")
    handle_key(ord('w'))
    assert STATE.running, "TUI detached on 'w'!"
    print("   ✓ 'w' key executed without crash or detach.")

    # 7. Background test run 't'
    print(">> 7. Testing 't' (trigger test suite in background)...")
    handle_key(ord('t'))
    assert STATE.running, "TUI detached on 't'!"
    print("   ✓ 't' key executed without crash or detach.")

    # 8. TUI utilities: 'p' (pause), 'l' (fullscreen logs), 'c' (clear buffer)
    print(">> 8. Testing display modes: 'p' (pause), 'l' (fullscreen), 'c' (clear)...")
    paused_state = STATE.is_paused
    handle_key(ord('p'))
    assert STATE.is_paused == (not paused_state)
    handle_key(ord('p'))

    fs_state = STATE.fullscreen_logs
    handle_key(ord('l'))
    assert STATE.fullscreen_logs == (not fs_state)
    handle_key(ord('l'))

    handle_key(ord('c'))
    assert STATE.running

    # Terminal resize event
    import curses
    handle_key(curses.KEY_RESIZE)
    assert STATE.running
    print("   ✓ Display modes and resize handled safely.")

    # 9. Modals: Help '?' and Quit dialog 'q'
    print(">> 9. Testing Help modal '?' and Quit dialog 'q'...")
    handle_key(ord('?'))
    assert STATE.show_help, "Help modal should open on '?'"
    handle_key(ord('?'))
    assert not STATE.show_help, "Help modal should close on '?'"

    handle_key(ord('q'))
    assert STATE.show_quit_dialog, "Quit dialog should open on 'q'"
    handle_key(ord('c'))
    assert not STATE.show_quit_dialog, "Quit dialog should cancel on 'c'"
    assert STATE.running, "TUI should still be running after cancelling quit dialog"
    print("   ✓ Modals opened and closed cleanly without detaching.")

    print("\n✅ All TUI Keyboard & Lifecycle tests PASSED (0 crashes, 0 detaches)!")
    return True

if __name__ == "__main__":
    test_tui_keys()
