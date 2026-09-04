#!/usr/bin/env python3
"""
Simulated Active Directory / Microsoft Graph Service
Serves enterprise directory records (OUs, Users, Security Groups, Computer Objects)
from the canonical enterprise_plant_dataset.json.
Can run as a standalone HTTP microservice or be imported programmatically in test suites.
"""

import sys
import os
import json
import argparse
from http.server import HTTPServer, BaseHTTPRequestHandler
from urllib.parse import urlparse, parse_qs
from typing import Dict, List, Any, Optional

# Add fleet directory to import dataset_loader
sys.path.insert(0, os.path.join(os.path.dirname(__file__), '../fleet'))
from dataset_loader import load_enterprise_dataset, get_all_client_pcs, get_all_users, get_all_security_groups, get_all_ous

class MockActiveDirectoryService:
    def __init__(self):
        self.dataset = load_enterprise_dataset()

    def get_user_groups(self, user_id_or_email: str) -> List[Dict[str, Any]]:
        target = user_id_or_email.lower().strip()
        user = None
        for u in self.dataset.get('users', []):
            if u.get('id', '').lower() == target or u.get('email', '').lower() == target or u.get('username', '').lower() == target:
                user = u
                break
        
        user_group_ids = user.get('securityGroupIds', []) if user else []
        matched_groups = []
        for g in self.dataset.get('securityGroups', []):
            gid = g.get('groupIdentifier', '').lower()
            sid = g.get('id', '').lower()
            if any(u_gid.lower() == gid or u_gid.lower() == sid for u_gid in user_group_ids):
                matched_groups.append({
                    "@odata.type": "#microsoft.graph.group",
                    "id": g.get('groupIdentifier'),
                    "displayName": g.get('displayName'),
                    "mappedRole": g.get('mappedRole'),
                    "organization": g.get('mappedOrganizationName'),
                    "securityEnabled": True
                })
        return matched_groups

    def get_users(self) -> List[Dict[str, Any]]:
        domain = self.dataset.get('metadata', {}).get('domain', 'factory.corp')
        users = []
        for u in self.dataset.get('users', []):
            users.append({
                "id": u.get('id'),
                "displayName": u.get('name'),
                "userPrincipalName": f"{u.get('username')}@{domain}",
                "mail": u.get('email'),
                "jobTitle": u.get('jobTitle'),
                "department": u.get('department'),
                "accountEnabled": True,
                "memberOf": u.get('securityGroupIds', [])
            })
        return users

    def get_groups(self) -> List[Dict[str, Any]]:
        return self.dataset.get('securityGroups', [])

    def get_devices(self) -> List[Dict[str, Any]]:
        devices = []
        for pc in self.dataset.get('clientPcs', []):
            devices.append({
                "id": pc.get('machineIdentifier'),
                "displayName": pc.get('name'),
                "deviceHostName": pc.get('hostname'),
                "operatingSystem": pc.get('osVersion'),
                "trustType": "ServerAdJoined",
                "isManaged": True,
                "network": {
                    "ipAddress": pc.get('ipAddress'),
                    "macAddress": pc.get('macAddress'),
                    "vlanId": pc.get('vlanId')
                },
                "adOuPath": pc.get('adOuPath')
            })
        return devices

    def search_ldap(self, base_dn: str = "DC=factory,DC=corp", query_filter: str = "(objectClass=*)") -> List[Dict[str, Any]]:
        filt = query_filter.lower()
        results = []

        if 'organizationalunit' in filt or 'ou' in filt or filt == '(objectclass=*)':
            for ou in self.dataset.get('activeDirectoryOUs', []):
                results.append({
                    "dn": ou.get('ouPath'),
                    "objectClass": ["top", "organizationalUnit"],
                    "ou": ou.get('name'),
                    "vlanId": ou.get('vlanId'),
                    "vlanName": ou.get('vlanName'),
                    "subnet": ou.get('subnet'),
                    "location": ou.get('location'),
                    "purpose": ou.get('purpose'),
                    "machineType": ou.get('machineType')
                })

        if 'computer' in filt or 'device' in filt or filt == '(objectclass=*)':
            domain = self.dataset.get('metadata', {}).get('domain', 'factory.corp')
            for pc in self.dataset.get('clientPcs', []):
                results.append({
                    "dn": f"CN={pc.get('hostname')},{pc.get('adOuPath')}",
                    "objectClass": ["top", "person", "organizationalPerson", "user", "computer"],
                    "cn": pc.get('hostname'),
                    "sAMAccountName": f"{pc.get('hostname')}$",
                    "dNSHostName": f"{pc.get('hostname', '').lower()}.{domain}",
                    "operatingSystem": pc.get('osVersion'),
                    "networkAddress": pc.get('ipAddress'),
                    "macAddress": pc.get('macAddress'),
                    "vlanId": pc.get('vlanId')
                })

        if 'user' in filt or 'person' in filt or filt == '(objectclass=*)':
            for u in self.dataset.get('users', []):
                results.append({
                    "dn": f"CN={u.get('name')},OU=Users,{base_dn}",
                    "objectClass": ["top", "person", "organizationalPerson", "user"],
                    "cn": u.get('name'),
                    "sAMAccountName": u.get('username'),
                    "mail": u.get('email'),
                    "title": u.get('jobTitle'),
                    "memberOf": u.get('securityGroupIds', [])
                })

        return results

class MockAdHttpHandler(BaseHTTPRequestHandler):
    service = MockActiveDirectoryService()

    def log_message(self, format, *args):
        pass

    def _send_json(self, status: int, data: Any):
        body = json.dumps(data, indent=2).encode('utf-8')
        self.send_response(status)
        self.send_header("Content-Type", "application/json")
        self.send_header("Access-Control-Allow-Origin", "*")
        self.send_header("Access-Control-Allow-Methods", "GET, POST, OPTIONS")
        self.send_header("Access-Control-Allow-Headers", "Content-Type, Authorization")
        self.send_header("Content-Length", str(len(body)))
        self.end_headers()
        self.wfile.write(body)

    def do_OPTIONS(self):
        self.send_response(200)
        self.send_header("Access-Control-Allow-Origin", "*")
        self.send_header("Access-Control-Allow-Methods", "GET, POST, OPTIONS")
        self.send_header("Access-Control-Allow-Headers", "Content-Type, Authorization")
        self.end_headers()

    def do_GET(self):
        parsed = urlparse(self.path)
        qs = parse_qs(parsed.query)

        if parsed.path.endswith("/v1.0/me/memberOf"):
            user_id = qs.get("userId", ["usr-sally-01"])[0]
            groups = self.service.get_user_groups(user_id)
            self._send_json(200, {
                "@odata.context": "https://graph.microsoft.com/v1.0/$metadata#directoryObjects",
                "value": groups
            })
        elif parsed.path.endswith("/v1.0/users"):
            self._send_json(200, {
                "@odata.context": "https://graph.microsoft.com/v1.0/$metadata#users",
                "value": self.service.get_users()
            })
        elif parsed.path.endswith("/v1.0/groups"):
            self._send_json(200, {
                "@odata.context": "https://graph.microsoft.com/v1.0/$metadata#groups",
                "value": self.service.get_groups()
            })
        elif parsed.path.endswith("/v1.0/devices"):
            self._send_json(200, {
                "@odata.context": "https://graph.microsoft.com/v1.0/$metadata#devices",
                "value": self.service.get_devices()
            })
        elif parsed.path.endswith("/ldap/search") or parsed.path == "/ldap":
            base_dn = qs.get("baseDN", ["DC=factory,DC=corp"])[0]
            filt = qs.get("filter", ["(objectClass=*)"])[0]
            results = self.service.search_ldap(base_dn=base_dn, query_filter=filt)
            self._send_json(200, {
                "baseDN": base_dn,
                "filter": filt,
                "count": len(results),
                "entries": results
            })
        elif parsed.path == "/" or parsed.path == "/health":
            self._send_json(200, {
                "service": "Mock Active Directory & Entra ID Graph Provider",
                "status": "healthy",
                "domain": "factory.corp"
            })
        else:
            self._send_json(404, {"error": "Endpoint not found"})

    def do_POST(self):
        parsed = urlparse(self.path)
        length = int(self.headers.get("Content-Length", 0))
        raw_body = self.rfile.read(length).decode("utf-8") if length > 0 else "{}"
        try:
            payload = json.loads(raw_body)
        except Exception:
            payload = {}

        if parsed.path.endswith("/ldap/search"):
            base_dn = payload.get("baseDN", "DC=factory,DC=corp")
            filt = payload.get("filter", "(objectClass=*)")
            results = self.service.search_ldap(base_dn=base_dn, query_filter=filt)
            self._send_json(200, {
                "baseDN": base_dn,
                "filter": filt,
                "count": len(results),
                "entries": results
            })
        else:
            self.do_GET()

def run_server(port: int = 5088):
    server = HTTPServer(("0.0.0.0", port), MockAdHttpHandler)
    print(f"==================================================================")
    print(f"  Mock Active Directory & Entra ID Server running on port {port}")
    print(f"  Graph Endpoints: /v1.0/me/memberOf, /v1.0/users, /v1.0/groups, /v1.0/devices")
    print(f"  LDAP Endpoint:   /ldap/search")
    print(f"==================================================================")
    try:
        server.serve_forever()
    except KeyboardInterrupt:
        print("\nStopping Mock Active Directory Server...")

if __name__ == '__main__':
    parser = argparse.ArgumentParser(description="Mock Active Directory / Entra ID Graph Server")
    parser.add_argument("--port", type=int, default=int(os.environ.get("MOCK_AD_PORT", "5088")), help="Port to listen on")
    args = parser.parse_args()
    run_server(port=args.port)
