#!/usr/bin/env python3
"""The committed simulator -> backend contract fixture must match the current simulator code."""

import os
import sys
import unittest

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import contract_fixture  # noqa: E402


class ContractFixtureTest(unittest.TestCase):
    def test_generation_is_deterministic(self):
        self.assertEqual(contract_fixture.render(), contract_fixture.render())

    def test_committed_fixture_is_up_to_date(self):
        self.assertTrue(
            os.path.exists(contract_fixture.FIXTURE_PATH),
            "fixture missing: run `venv/bin/python simulators/fleet/contract_fixture.py --write`",
        )
        with open(contract_fixture.FIXTURE_PATH, encoding="utf-8") as f:
            committed = f.read()
        self.assertEqual(
            committed,
            contract_fixture.render(),
            "station model / encoding changed: regenerate with "
            "`venv/bin/python simulators/fleet/contract_fixture.py --write` and re-run the backend contract test",
        )

    def test_covers_running_fault_and_maintenance_states(self):
        states = {case["state"] for case in contract_fixture.build_cases()}
        self.assertTrue({"FAULTED", "MAINTENANCE"} <= states, states)
        self.assertTrue(states & {"RUNNING", "WARNING", "IDLE"}, states)


if __name__ == "__main__":
    unittest.main()
