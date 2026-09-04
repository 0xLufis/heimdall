#!/usr/bin/env python3
"""
Integration test for Simulated PC Daemon and Mock Active Directory
"""

import unittest
import os
import sys

sys.path.insert(0, os.path.dirname(__file__))
sys.path.insert(0, os.path.join(os.path.dirname(__file__), '../active_directory'))

from dataset_loader import load_enterprise_dataset, get_client_pc_by_hostname
from mock_ad_server import MockActiveDirectoryService
from simulated_pc_daemon import SimulatedPcState, build_system_info_request, state

class TestSimulatedPcIntegration(unittest.TestCase):
    def setUp(self):
        self.ad_service = MockActiveDirectoryService()

    def test_mock_ad_directory_and_devices(self):
        devices = self.ad_service.get_devices()
        self.assertGreaterEqual(len(devices), 10)
        hostnames = [d['deviceHostName'] for d in devices]
        self.assertIn('CPC-L06-ROB-01', hostnames)
        self.assertIn('CPC-L09-VIS-01', hostnames)
        self.assertIn('CPC-L06-SCR-01', hostnames)

    def test_simulated_pc_state_and_cmi(self):
        s = SimulatedPcState('CPC-L06-SCR-01')
        self.assertEqual(s.pc.get('machineType'), 'Screwing Station')
        bios_out = s.engine.execute('wmic bios get SerialNumber /value')
        self.assertIn('BIOS-L06-SCR-01', bios_out)

    def test_simulated_pc_system_info_request_generation(self):
        req = build_system_info_request()
        self.assertTrue(req.hostname)
        self.assertTrue(req.machine_identifier)
        self.assertGreater(len(req.components), 0)
        
        # Verify CMI component data
        cmi_comp = next((c for c in req.components if c.technology == "Windows Management Instrumentation"), None)
        self.assertIsNotNone(cmi_comp)
        self.assertIn("WmicOs", cmi_comp.data_json)

if __name__ == '__main__':
    unittest.main()
