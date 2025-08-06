#!/usr/bin/env python3
"""
Remove unused translation keys from language files
"""

import json
from pathlib import Path

def remove_unused_keys():
    """Remove the identified unused keys from language files"""
    
    # Keys to remove (identified as unused)
    unused_keys = {
        "EnableConfirmFirst",
        "FatalError", 
        "Info",
        "ModLibraryNotFound",
        "Warning"
    }
    
    language_dir = Path("Language")
    files_modified = []
    
    # Process all JSON files in Language directory and subdirectories
    for json_file in language_dir.rglob("*.json"):
        try:
            print(f"🔍 Checking {json_file}...")
            
            # Read the file
            with open(json_file, 'r', encoding='utf-8') as f:
                data = json.load(f)
            
            # Check if any unused keys exist in this file
            keys_to_remove = []
            for key in unused_keys:
                if key in data:
                    keys_to_remove.append(key)
            
            if keys_to_remove:
                print(f"  📝 Removing {len(keys_to_remove)} unused keys: {', '.join(keys_to_remove)}")
                
                # Remove the keys
                for key in keys_to_remove:
                    del data[key]
                
                # Write back to file with proper formatting
                with open(json_file, 'w', encoding='utf-8') as f:
                    json.dump(data, f, indent=2, ensure_ascii=False)
                
                files_modified.append(str(json_file))
            else:
                print(f"  ✅ No unused keys found")
                
        except Exception as e:
            print(f"  ❌ Error processing {json_file}: {e}")
    
    # Summary
    print(f"\n{'='*50}")
    print(f"🎉 CLEANUP COMPLETE")
    print(f"{'='*50}")
    print(f"Files modified: {len(files_modified)}")
    for file in files_modified:
        print(f"  • {file}")
    
    if files_modified:
        print(f"\n✅ Successfully removed unused translation keys!")
        print(f"Removed keys: {', '.join(sorted(unused_keys))}")
    else:
        print(f"\n✅ No unused keys found to remove.")

if __name__ == "__main__":
    remove_unused_keys()