#!/usr/bin/env python3
import json, os, glob

LANGUAGE_DIR = "FlairX-Mod-Manager/Language"
BASE_LANG = "en.json"

def load_json(path):
    with open(path, 'r', encoding='utf-8') as f:
        return json.load(f)

def save_json(path, data):
    with open(path, 'w', encoding='utf-8') as f:
        json.dump(data, f, ensure_ascii=False, indent=2)

base = load_json(os.path.join(LANGUAGE_DIR, BASE_LANG))
lang_files = [f for f in glob.glob(os.path.join(LANGUAGE_DIR, "*.json")) if not f.endswith(BASE_LANG)]

total = 0
for lang_file in sorted(lang_files):
    data = load_json(lang_file)
    missing = set(base.keys()) - set(data.keys())
    if missing:
        for key in sorted(missing):
            data[key] = base[key]
        save_json(lang_file, data)
        print(f"Updated {os.path.basename(lang_file)}: +{len(missing)} keys")
        total += len(missing)
    else:
        print(f"OK {os.path.basename(lang_file)}")

print(f"\nTotal: {total} translations added across {len(lang_files)} files")
