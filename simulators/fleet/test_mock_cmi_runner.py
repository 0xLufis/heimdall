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
        self.assertEqual(dataset.get('metadata', {}).get('plantName'), 'Smart Factory Giga-01')
        pcs = get_all_client_pcs()
        self.assertGreaterEqual(len(pcs), 10)

    def test_mock_cmi_wmic_os_query(self):
        engine = MockCmiEngine(hostname='CPC-L06-ROB-01')
        output = engine.execute('wmic os get Caption,Version /value')
        self.assertIn('Caption=Microsoft Windows 10 IoT Enterprise LTSC', output)
        self.assertIn('Version=10.0.19045', output)

    def test_mock_cmi_wmic_cpu_query(self):
        engine = MockCmiEngine(hostname='CPC-L09-VIS-01')
        output = engine.execute('wmic cpu get Name,NumberOfCores /value')
        self.assertIn('Name=Intel(R) Core(TM) i9-13900E CPU @ 3.00GHz', output)
        self.assertIn('NumberOfCores=24', output)

    def test_mock_cmi_wmic_bios_query(self):
        engine = MockCmiEngine(hostname='CPC-L06-SCR-01')
        output = engine.execute('wmic bios get SerialNumber /value')
        self.assertIn('SerialNumber=BIOS-L06-SCR-01', output)

    def test_mock_cmi_powershell_cim_query(self):
        engine = MockCmiEngine(hostname='CPC-L06-ROB-01')
        output = engine.execute('Get-CimInstance Win32_BIOS')
        self.assertIn('SerialNumber', output)
        self.assertIn('BIOS-L06-ROB-01', output)

    def test_mock_cmi_disk_and_memory(self):
        engine = MockCmiEngine(hostname='CPC-L03-MIL-01')
        mem = engine.execute('wmic memorychip get Capacity /value')
        self.assertIn('Capacity=17179869184', mem)
        disk = engine.execute('wmic logicaldisk get Caption,FreeSpace /value')
        self.assertIn('Caption=C:', disk)

if __name__ == '__main__':
    unittest.main()
