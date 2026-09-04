#!/usr/bin/env python3
"""
Python Dataset Loader for Enterprise Plant Dataset
Provides shared access to fixtures/enterprise_plant_dataset.json
across simulators, mock Active Directory, and mock CMI runners.
"""

import os
import json
from typing import Dict, List, Any, Optional

_CACHED_DATASET: Optional[Dict[str, Any]] = None

def get_dataset_path() -> str:
    candidates = [
        os.path.join(os.path.dirname(__file__), '../../fixtures/enterprise_plant_dataset.json'),
        os.path.join(os.path.dirname(__file__), '../fixtures/enterprise_plant_dataset.json'),
        os.path.join(os.path.dirname(__file__), 'fixtures/enterprise_plant_dataset.json'),
        os.path.join(os.getcwd(), 'fixtures/enterprise_plant_dataset.json'),
        '/app/fixtures/enterprise_plant_dataset.json'
    ]
    for c in candidates:
        norm = os.path.abspath(c)
        if os.path.exists(norm):
            return norm
    # Default to first candidate
    return os.path.abspath(candidates[0])

def load_enterprise_dataset() -> Dict[str, Any]:
    global _CACHED_DATASET
    if _CACHED_DATASET is not None:
        return _CACHED_DATASET

    path = get_dataset_path()
    if os.path.exists(path):
        with open(path, 'r', encoding='utf-8') as f:
            _CACHED_DATASET = json.load(f)
            return _CACHED_DATASET

    raise FileNotFoundError(f"Cannot find enterprise_plant_dataset.json in candidate locations.")

def get_all_client_pcs() -> List[Dict[str, Any]]:
    return load_enterprise_dataset().get('clientPcs', [])

def get_client_pc_by_hostname(hostname: str) -> Optional[Dict[str, Any]]:
    pcs = get_all_client_pcs()
    target = hostname.upper().strip()
    for pc in pcs:
        if pc.get('hostname', '').upper() == target:
            return pc
    return None

def get_all_users() -> List[Dict[str, Any]]:
    return load_enterprise_dataset().get('users', [])

def get_user_by_id(user_id: str) -> Optional[Dict[str, Any]]:
    for u in get_all_users():
        if u.get('id') == user_id:
            return u
    return None

def get_all_security_groups() -> List[Dict[str, Any]]:
    return load_enterprise_dataset().get('securityGroups', [])

def get_security_group_by_id(group_id: str) -> Optional[Dict[str, Any]]:
    target = group_id.lower().strip()
    for g in get_all_security_groups():
        if g.get('id', '').lower() == target or g.get('groupIdentifier', '').lower() == target:
            return g
    return None

def get_all_ous() -> List[Dict[str, Any]]:
    return load_enterprise_dataset().get('activeDirectoryOUs', [])

def get_all_organizations() -> List[Dict[str, Any]]:
    return load_enterprise_dataset().get('organizations', [])

if __name__ == '__main__':
    ds = load_enterprise_dataset()
    print(f"Loaded Enterprise Plant Dataset: {ds.get('metadata', {}).get('plantName')}")
    print(f"  Total Client PCs: {len(ds.get('clientPcs', []))}")
    print(f"  Total Users: {len(ds.get('users', []))}")
    print(f"  Total Security Groups: {len(ds.get('securityGroups', []))}")
    print(f"  Total OUs: {len(ds.get('activeDirectoryOUs', []))}")
