#!/usr/bin/env python3
"""
Heimdall Mock CMI (Common Information Model / WMI / CIM) Command Runner
Parses and executes standard Windows management queries (wmic, Get-CimInstance)
against simulated PCs defined in fixtures/enterprise_plant_dataset.json.
"""

import sys
import os
import re
import json
import argparse
from typing import Dict, List, Any, Optional

try:
    from dataset_loader import load_enterprise_dataset, get_client_pc_by_hostname, get_all_client_pcs
except ImportError:
    from .dataset_loader import load_enterprise_dataset, get_client_pc_by_hostname, get_all_client_pcs

class MockCmiEngine:
    def __init__(self, hostname: Optional[str] = None):
        self.hostname = (hostname or os.environ.get('SIMULATED_HOSTNAME') or 'CPC-L06-ROB-01').strip()
        self.pc = get_client_pc_by_hostname(self.hostname)
        if not self.pc:
            pcs = get_all_client_pcs()
            self.pc = pcs[0] if pcs else {}

        self.cmi = self.pc.get('cmiHardware', {})

    def execute(self, cmd_string: str) -> str:
        """Executes a wmic command or Get-CimInstance query string and returns formatted text."""
        cmd = cmd_string.strip()
        
        # Handle PowerShell syntax: Get-CimInstance Win32_* or Get-WmiObject Win32_*
        if re.search(r'(Get-CimInstance|Get-WmiObject)\s+Win32_', cmd, re.IGNORECASE):
            return self._execute_powershell_cim(cmd)

        # Handle WMIC syntax: wmic <alias> [where "..."] get <properties> [/format:...] [/value]
        if cmd.lower().startswith('wmic') or any(kw in cmd.lower() for kw in ['os get', 'cpu get', 'bios get', 'logicaldisk get', 'memorychip get', 'computersystem get', 'nicconfig']):
            return self._execute_wmic(cmd)

        return f"Unknown CMI command: {cmd}"

    def _execute_wmic(self, cmd: str) -> str:
        tokens = cmd.split()
        # Normalize
        if tokens and tokens[0].lower() == 'wmic':
            tokens = tokens[1:]

        if not tokens:
            return ""

        alias = tokens[0].lower()
        
        # Check for /value format
        is_value_format = any('/value' in t.lower() for t in tokens)
        
        # Find properties after 'get'
        get_idx = -1
        for i, t in enumerate(tokens):
            if t.lower() == 'get':
                get_idx = i
                break

        requested_props: List[str] = []
        if get_idx != -1 and get_idx + 1 < len(tokens):
            prop_str = tokens[get_idx + 1]
            requested_props = [p.strip() for p in prop_str.split(',') if not p.startswith('/')]

        # Dispatch based on alias
        if alias == 'os':
            data = self._get_os_data()
        elif alias in ('cpu', 'processor'):
            data = self._get_cpu_data()
        elif alias in ('memorychip', 'physicalmemory'):
            data = self._get_memory_data()
        elif alias in ('logicaldisk', 'disk'):
            data = self._get_logicaldisk_data()
        elif alias == 'bios':
            data = self._get_bios_data()
        elif alias in ('computersystem', 'cs'):
            data = self._get_computersystem_data()
        elif alias in ('nicconfig', 'networkadapterconfiguration'):
            data = self._get_nicconfig_data()
        else:
            return f"Alias not found: {alias}"

        # If specific properties requested, filter them
        if requested_props:
            filtered: Dict[str, Any] = {}
            for p in requested_props:
                # Case-insensitive match
                for k, v in data.items():
                    if k.lower() == p.lower():
                        filtered[k] = v
                        break
            data = filtered

        # Format output
        if is_value_format or True: # WMIC default in script automation is key=value
            lines = []
            for k, v in data.items():
                if isinstance(v, list):
                    v_str = "{" + ", ".join(f'"{item}"' for item in v) + "}"
                else:
                    v_str = str(v)
                lines.append(f"{k}={v_str}")
            return "\r\n".join(lines) + "\r\n"

    def _execute_powershell_cim(self, cmd: str) -> str:
        m = re.search(r'(Get-CimInstance|Get-WmiObject)\s+(Win32_\w+)', cmd, re.IGNORECASE)
        class_name = m.group(2).lower() if m else "win32_operatingsystem"

        if class_name == 'win32_operatingsystem':
            data = self._get_os_data()
        elif class_name == 'win32_processor':
            data = self._get_cpu_data()
        elif class_name == 'win32_physicalmemory':
            data = self._get_memory_data()
        elif class_name == 'win32_logicaldisk':
            data = self._get_logicaldisk_data()
        elif class_name == 'win32_bios':
            data = self._get_bios_data()
        elif class_name == 'win32_computersystem':
            data = self._get_computersystem_data()
        elif class_name == 'win32_networkadapterconfiguration':
            data = self._get_nicconfig_data()
        else:
            data = {"Status": "OK", "Class": class_name}

        lines = [f"{k.ljust(25)} : {v}" for k, v in data.items()]
        return "\n".join(lines) + "\n"

    def _get_os_data(self) -> Dict[str, Any]:
        os_info = self.cmi.get('os', {})
        return {
            "Caption": os_info.get('Caption', self.pc.get('osVersion', 'Windows 10 IoT Enterprise')),
            "Version": os_info.get('Version', '10.0.19045'),
            "BuildNumber": os_info.get('BuildNumber', '19045'),
            "OSArchitecture": os_info.get('OSArchitecture', '64-bit'),
            "InstallDate": os_info.get('InstallDate', '20240101000000.000000+000'),
            "Status": "OK"
        }

    def _get_cpu_data(self) -> Dict[str, Any]:
        cpu_info = self.cmi.get('cpu', {})
        return {
            "Name": cpu_info.get('Name', 'Intel(R) Core(TM) i7-11700E CPU @ 2.80GHz'),
            "NumberOfCores": cpu_info.get('NumberOfCores', 8),
            "NumberOfLogicalProcessors": cpu_info.get('NumberOfLogicalProcessors', 16),
            "MaxClockSpeed": cpu_info.get('MaxClockSpeed', 2800),
            "Status": "OK"
        }

    def _get_memory_data(self) -> Dict[str, Any]:
        mem_info = self.cmi.get('memory', {})
        return {
            "Capacity": mem_info.get('Capacity', 34359738368),
            "Speed": mem_info.get('Speed', 2666),
            "Manufacturer": mem_info.get('Manufacturer', 'Micron Technology'),
            "PartNumber": mem_info.get('PartNumber', 'MTA18ASF4G72AZ-2G6E1')
        }

    def _get_logicaldisk_data(self) -> Dict[str, Any]:
        disks = self.cmi.get('disks', [])
        d0 = disks[0] if disks else {"Caption": "C:", "Size": 512110190592, "FreeSpace": 342110190592, "FileSystem": "NTFS"}
        return {
            "Caption": d0.get('Caption', 'C:'),
            "Size": d0.get('Size', 512110190592),
            "FreeSpace": d0.get('FreeSpace', 342110190592),
            "FileSystem": d0.get('FileSystem', 'NTFS'),
            "Description": "Local Fixed Disk"
        }

    def _get_bios_data(self) -> Dict[str, Any]:
        bios = self.cmi.get('bios', {})
        return {
            "Manufacturer": bios.get('Manufacturer', 'American Megatrends Inc.'),
            "SMBIOSBIOSVersion": bios.get('SMBIOSBIOSVersion', 'V2.14'),
            "SerialNumber": bios.get('SerialNumber', f"BIOS-{self.hostname}")
        }

    def _get_computersystem_data(self) -> Dict[str, Any]:
        cs = self.cmi.get('computerSystem', {})
        return {
            "Name": cs.get('Name', self.hostname),
            "Manufacturer": cs.get('Manufacturer', 'Beckhoff Automation GmbH'),
            "Model": cs.get('Model', 'Industrial Edge IPC'),
            "TotalPhysicalMemory": cs.get('TotalPhysicalMemory', 34359738368),
            "Domain": cs.get('Domain', 'factory.corp')
        }

    def _get_nicconfig_data(self) -> Dict[str, Any]:
        net = self.cmi.get('network', {})
        return {
            "Description": net.get('Description', 'Intel(R) I210 Gigabit Network Connection'),
            "IPAddress": [net.get('IPAddress', self.pc.get('ipAddress', '10.10.10.11'))],
            "MACAddress": net.get('MACAddress', self.pc.get('macAddress', '00:1A:2B:3C:4D:11')),
            "DefaultIPGateway": [net.get('DefaultIPGateway', '10.10.10.1')],
            "DNSServerSearchOrder": net.get('DNSServerSearchOrder', ['10.10.1.10', '10.10.1.11']),
            "IPEnabled": True
        }

def main():
    parser = argparse.ArgumentParser(description="Heimdall Mock CMI Command Runner")
    parser.add_argument("command", nargs="*", help="WMIC or CIM command to execute")
    parser.add_argument("--hostname", default=os.environ.get("SIMULATED_HOSTNAME", "CPC-L06-ROB-01"), help="Target simulated host")
    parser.add_argument("--json", action="store_true", help="Output as JSON")
    args = parser.parse_args()

    prog = os.path.basename(sys.argv[0]).lower()
    raw_cmd = " ".join(args.command)
    if prog in ('wmic', 'wmic.exe') and not raw_cmd.lower().startswith('wmic'):
        cmd_str = f"wmic {raw_cmd}"
    elif prog in ('get-ciminstance', 'get-wmiobject') and not ('get-ciminstance' in raw_cmd.lower() or 'get-wmiobject' in raw_cmd.lower()):
        cmd_str = f"Get-CimInstance {raw_cmd}"
    else:
        cmd_str = raw_cmd if raw_cmd else "wmic os get Caption,Version /value"

    engine = MockCmiEngine(hostname=args.hostname)
    output = engine.execute(cmd_str)
    if args.json:
        # Simple parser to json
        lines = output.strip().splitlines()
        res = {}
        for line in lines:
            if '=' in line:
                k, v = line.split('=', 1)
                res[k.strip()] = v.strip()
            elif ':' in line:
                k, v = line.split(':', 1)
                res[k.strip()] = v.strip()
        print(json.dumps(res, indent=2))
    else:
        sys.stdout.write(output)

if __name__ == '__main__':
    main()
