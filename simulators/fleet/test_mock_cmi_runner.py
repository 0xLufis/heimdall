#!/usr/bin/env python3
"""
Unit tests for Mock CMI Runner & Dataset Loader
"""

import unittest
import os
import sys

sys.path.insert(0, os.path.dirname(__file__))
from dataset_loader import load_enterprise_dataset, get_client_pc_by_hostname, get_all_client_pcs
from mock_cmi_runner import MockCmiEngine

class TestMockCmiRunner(unittest.TestCase):
    def test_dataset_loader_reads_canonical_dataset(self):
        dataset = load_enterprise_dataset()
        self.assertEqual(dataset.get('metadata', {}).get('plantName'), 'Smart Factory Giga-01 (AI Synthetic Facility)')
        pcs = get_all_client_pcs()
        self.assertGreaterEqual(len(pcs), 10)

    def test_mock_cmi_wmic_os_query(self):
        engine = MockCmiEngine(hostname='IPC-L01-OP030-DEDICATED')
        output = engine.execute('wmic os get Caption,Version /value')
        self.assertIn('Caption=Microsoft Windows 10 IoT Enterprise 2021 LTSC', output)
        self.assertIn('Version=10.0.19045', output)

    def test_mock_cmi_wmic_cpu_query(self):
        engine = MockCmiEngine(hostname='IPC-L01-OP030-DEDICATED')
        output = engine.execute('wmic cpu get Name,NumberOfCores /value')
        self.assertIn('Name=Intel(R) Core(TM) i7-1185GRE @ 2.80GHz', output)
        self.assertIn('NumberOfCores=8', output)

    def test_mock_cmi_wmic_bios_query(self):
        engine = MockCmiEngine(hostname='IPC-L01-ROB-ALPHA')
        output = engine.execute('wmic bios get SerialNumber /value')
        self.assertIn('SerialNumber=BIOS-IPC-L01-ROB-ALPHA', output)

    def test_mock_cmi_powershell_cim_query(self):
        engine = MockCmiEngine(hostname='IPC-L01-OP030-DEDICATED')
        output = engine.execute('Get-CimInstance Win32_BIOS')
        self.assertIn('SerialNumber', output)
        self.assertIn('BIOS-IPC-L01-OP030-DEDICATED', output)

    def test_mock_cmi_disk_and_memory(self):
        engine = MockCmiEngine(hostname='IPC-L01-OP030-DEDICATED')
        mem = engine.execute('wmic memorychip get Capacity /value')
        self.assertIn('Capacity=34359738368', mem)
        disk = engine.execute('wmic logicaldisk get Caption,FreeSpace /value')
        self.assertIn('Caption=C:', disk)

if __name__ == '__main__':
    unittest.main()
