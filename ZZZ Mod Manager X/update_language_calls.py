#!/usr/bin/env python3
"""
Script to update all LanguageManager.Instance.T() calls to T() in SettingsPage.xaml.cs
"""

import re

def update_language_calls(file_path):
    with open(file_path, 'r', encoding='utf-8') as f:
        content = f.read()
    
    # Replace LanguageManager.Instance.T( with T(
    updated_content = re.sub(r'LanguageManager\.Instance\.T\(', 'T(', content)
    
    with open(file_path, 'w', encoding='utf-8') as f:
        f.write(updated_content)
    
    print(f"Updated {file_path}")

if __name__ == "__main__":
    update_language_calls("Pages/SettingsPage.xaml.cs")