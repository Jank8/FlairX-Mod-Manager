#!/usr/bin/env python3
"""
Translation Key Analysis Script
Analyzes C# code to find:
1. Translation keys used in code but missing from language files
2. Translation keys in language files but not used in code
3. Summary statistics
"""

import os
import re
import json
from pathlib import Path
from collections import defaultdict, Counter

class TranslationAnalyzer:
    def __init__(self, project_root="."):
        self.project_root = Path(project_root)
        self.language_dir = self.project_root / "Language"
        self.used_keys = set()
        self.language_keys = defaultdict(set)
        self.key_usage_count = Counter()
        
    def find_translation_keys_in_code(self):
        """Find all translation keys used in C# code"""
        print("🔍 Scanning C# files for translation keys...")
        
        # Pattern to match SharedUtilities.GetTranslation calls
        pattern = r'SharedUtilities\.GetTranslation\([^,]+,\s*"([^"]+)"\)'
        
        cs_files = list(self.project_root.rglob("*.cs"))
        print(f"Found {len(cs_files)} C# files to scan")
        
        for cs_file in cs_files:
            try:
                with open(cs_file, 'r', encoding='utf-8') as f:
                    content = f.read()
                    
                matches = re.findall(pattern, content)
                for key in matches:
                    self.used_keys.add(key)
                    self.key_usage_count[key] += 1
                    
                if matches:
                    print(f"  📄 {cs_file.relative_to(self.project_root)}: {len(matches)} keys")
                    
            except Exception as e:
                print(f"  ❌ Error reading {cs_file}: {e}")
        
        print(f"✅ Found {len(self.used_keys)} unique translation keys in code")
        return self.used_keys
    
    def find_keys_in_language_files(self):
        """Find all keys in language JSON files"""
        print("\n🔍 Scanning language files...")
        
        if not self.language_dir.exists():
            print(f"❌ Language directory not found: {self.language_dir}")
            return
        
        # Scan main language files
        for lang_file in self.language_dir.glob("*.json"):
            self._scan_language_file(lang_file, "main")
        
        # Scan subdirectory language files
        for subdir in self.language_dir.iterdir():
            if subdir.is_dir():
                for lang_file in subdir.glob("*.json"):
                    self._scan_language_file(lang_file, subdir.name)
        
        total_keys = sum(len(keys) for keys in self.language_keys.values())
        print(f"✅ Found {total_keys} total keys across {len(self.language_keys)} language files")
    
    def _scan_language_file(self, lang_file, category):
        """Scan a single language file"""
        try:
            with open(lang_file, 'r', encoding='utf-8') as f:
                data = json.load(f)
                keys = set(data.keys())
                self.language_keys[f"{category}/{lang_file.name}"] = keys
                print(f"  📄 {lang_file.relative_to(self.project_root)}: {len(keys)} keys")
        except Exception as e:
            print(f"  ❌ Error reading {lang_file}: {e}")
    
    def analyze_missing_keys(self):
        """Find keys used in code but missing from language files"""
        print("\n🔍 Analyzing missing keys...")
        
        # Get all keys from ALL language files (main and subdirectories)
        all_lang_keys = set()
        for file_path, keys in self.language_keys.items():
            all_lang_keys.update(keys)
        
        missing_keys = self.used_keys - all_lang_keys
        
        if missing_keys:
            print(f"❌ Found {len(missing_keys)} keys used in code but missing from language files:")
            for key in sorted(missing_keys):
                usage_count = self.key_usage_count[key]
                print(f"  • {key} (used {usage_count} time{'s' if usage_count != 1 else ''})")
        else:
            print("✅ All keys used in code are present in language files!")
        
        return missing_keys
    
    def analyze_unused_keys(self):
        """Find keys in language files but not used in code"""
        print("\n🔍 Analyzing unused keys...")
        
        # Get all keys from ALL language files
        all_lang_keys = set()
        for file_path, keys in self.language_keys.items():
            all_lang_keys.update(keys)
        
        unused_keys = all_lang_keys - self.used_keys
        
        # Filter out special keys that might not be used directly in code
        special_keys = {"Language_DisplayName"}
        unused_keys = unused_keys - special_keys
        
        if unused_keys:
            print(f"⚠️  Found {len(unused_keys)} keys in language files but not used in code:")
            for key in sorted(unused_keys):
                print(f"  • {key}")
        else:
            print("✅ All language file keys are used in code!")
        
        return unused_keys
    
    def analyze_key_usage_frequency(self):
        """Analyze how frequently keys are used"""
        print("\n📊 Key usage frequency analysis:")
        
        if not self.key_usage_count:
            print("No keys found in code")
            return
        
        # Most used keys
        most_used = self.key_usage_count.most_common(10)
        print(f"\n🔥 Top 10 most used keys:")
        for key, count in most_used:
            print(f"  • {key}: {count} times")
        
        # Usage distribution
        usage_counts = list(self.key_usage_count.values())
        single_use = sum(1 for count in usage_counts if count == 1)
        multiple_use = len(usage_counts) - single_use
        
        print(f"\n📈 Usage distribution:")
        print(f"  • Keys used once: {single_use}")
        print(f"  • Keys used multiple times: {multiple_use}")
        print(f"  • Average usage per key: {sum(usage_counts) / len(usage_counts):.1f}")
    
    def generate_report(self):
        """Generate a comprehensive report"""
        print("=" * 60)
        print("🔍 TRANSLATION KEY ANALYSIS REPORT")
        print("=" * 60)
        
        # Find keys in code and language files
        self.find_translation_keys_in_code()
        self.find_keys_in_language_files()
        
        # Analyze missing and unused keys
        missing_keys = self.analyze_missing_keys()
        unused_keys = self.analyze_unused_keys()
        
        # Usage frequency analysis
        self.analyze_key_usage_frequency()
        
        # Summary
        print("\n" + "=" * 60)
        print("📋 SUMMARY")
        print("=" * 60)
        print(f"Keys used in code: {len(self.used_keys)}")
        
        all_lang_keys = set()
        for file_path, keys in self.language_keys.items():
            all_lang_keys.update(keys)
        
        print(f"Keys in all language files: {len(all_lang_keys)}")
        print(f"Missing keys (in code, not in lang files): {len(missing_keys)}")
        print(f"Unused keys (in lang files, not in code): {len(unused_keys)}")
        
        if missing_keys or unused_keys:
            print("\n⚠️  Action required:")
            if missing_keys:
                print(f"  • Add {len(missing_keys)} missing keys to language files")
            if unused_keys:
                print(f"  • Consider removing {len(unused_keys)} unused keys from language files")
        else:
            print("\n✅ Perfect! All keys are properly synchronized.")
        
        return {
            'used_keys': self.used_keys,
            'missing_keys': missing_keys,
            'unused_keys': unused_keys,
            'key_usage_count': dict(self.key_usage_count)
        }

if __name__ == "__main__":
    analyzer = TranslationAnalyzer()
    results = analyzer.generate_report()