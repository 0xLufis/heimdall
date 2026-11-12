#!/usr/bin/env python3
"""
Regenerates fixtures/enterprise_plant_dataset.json (directory / identity fixture read by the web
BFF, the mock Active Directory and the simulators).

The fixture is rendered from the same in-memory model as the inventory CSV, the topology and the
SQL seed, so it cannot drift from them: see `build_fixture` in seed_pipeline.py.
`python3 seed_data/seed_pipeline.py --generate-all` regenerates all four files at once; this
script only rewrites the fixture.
"""

import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import seed_pipeline  # noqa: E402


def generate_fixtures():
    model = seed_pipeline.build_model()
    seed_pipeline._write(seed_pipeline.FIXTURE_FILE, seed_pipeline.render_fixture(model))
    fixture = seed_pipeline.build_fixture(model)
    print(f"✓ Wrote {seed_pipeline.FIXTURE_FILE} (Users: {len(fixture['users'])}, Orgs: {len(fixture['organizations'])}, "
          f"IPCs: {len(fixture['clientPcs'])}, OUs: {len(fixture['activeDirectoryOUs'])})")


if __name__ == '__main__':
    generate_fixtures()
