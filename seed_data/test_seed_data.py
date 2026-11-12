#!/usr/bin/env python3
"""
Seed datasets: referential integrity is enforced (a broken reference is reported, not loaded) and
generation is deterministic. Run: python3 -m unittest discover -s seed_data -p "test_*.py"
"""

import copy
import os
import sys
import unittest
from datetime import datetime, timezone

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
sys.path.insert(0, os.path.join(HERE, "..", "scripts"))

import seed_pipeline  # noqa: E402
import seed_demo_data  # noqa: E402


class EnterpriseDatasetTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.model = seed_pipeline.build_model()

    def test_model_is_consistent_and_files_are_current(self):
        errors, _, facts = seed_pipeline.check_model(self.model)
        self.assertEqual(errors, [])
        self.assertEqual(seed_pipeline.check_generated_files(self.model), [])
        self.assertGreaterEqual(facts.get("dxf_handles", 100), 100)

    def test_generation_is_deterministic(self):
        self.assertEqual(seed_pipeline.render_sql(self.model), seed_pipeline.render_sql(seed_pipeline.build_model()))

    def test_hierarchy_and_admin_contract(self):
        types = {g["type"] for g in self.model["groups"]}
        self.assertTrue({"BusinessUnit", "Project", "Line", "SubLine", "Technological"} <= types)
        sql = seed_pipeline.render_sql(self.model)
        self.assertIn("'usr-admin-primary', 'System Administrator', 'admin@heimdall.dev', true, 'system_admin', 'admin'", sql)
        self.assertIn("'mem-admin-platform', 'org-platform', 'usr-admin-primary'", sql)
        self.assertNotIn("INSERT INTO backend.maintenance_tickets", sql)

    def test_broken_references_are_reported(self):
        m = copy.deepcopy(self.model)
        m["links"] = [l for l in m["links"] if l["station"] != "L01-OP010"]
        m["stations"][0]["controllers"] = []
        m["stations"][1]["handle"] = "L99-OP999"
        m["users"]["usr-synth-01"]["role"] = "wizard"
        m["recipes"][0]["crew"] = "pg-nobody"
        errors, _, _ = seed_pipeline.check_model(m)
        text = "\n".join(errors)
        for needle in ("no controller", "L99-OP999", "wizard", "unknown crew"):
            self.assertIn(needle, text)


class DemoDatasetTests(unittest.TestCase):
    NOW = datetime(2026, 11, 9, 20, 0, tzinfo=timezone.utc)

    def build(self, mutate=None):
        ds = seed_demo_data.Dataset(self.NOW, with_logins=False, demo_password=None)
        if mutate:
            original = ds._load
            ds._load = lambda name: mutate(name, original(name))
        return ds.build()

    def test_valid_and_references_resolve(self):
        ds = self.build()
        self.assertEqual(ds.errors, [])
        self.assertEqual(ds.reference_facts["simulated_stations"], 19)

    def test_broken_references_are_reported(self):
        def mutate(name, doc):
            if name == "plant.json":
                doc["station_controllers"] = [l for l in doc["station_controllers"] if l["_key"] != "sc:L3-MAIN:OP090"]
                for c in doc["controllers"]:
                    if c["_key"] == "ipc:L1-MAIN":
                        c["pinned_object_handle"] = "NOT-IN-DRAWING"
                for mc in doc["machine_components"]:
                    if mc["_key"] == "mc:L1-OP010:CONVEYOR-01":
                        mc["current_part_id"] = "@part:L2-OP060:optic"
            return doc
        text = "\n".join(self.build(mutate).errors)
        self.assertIn("NV-L3-OP090: needs exactly one Primary", text)
        self.assertIn("NOT-IN-DRAWING", text)
        self.assertIn("is not installed in this station", text)


if __name__ == "__main__":
    unittest.main()
