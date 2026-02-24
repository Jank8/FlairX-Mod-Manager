import json
import os

# New translation keys to add
new_keys = {
    "SettingsPage_CategoryManagement_Header": "Category Management",
    "SettingsPage_PinnedCategories_Label": "Pinned Categories",
    "SettingsPage_PinnedCategories_Description": "Pin categories to footer menu for quick access",
    "SettingsPage_PinnedCategories_Placeholder": "Select category to pin...",
    "SettingsPage_HiddenCategories_Label": "Hidden Categories",
    "SettingsPage_HiddenCategories_Description": "Hide categories from the navigation menu",
    "SettingsPage_HiddenCategories_Placeholder": "Select category to hide..."
}

# Directory containing language files
lang_dir = "FlairX-Mod-Manager/Language"

# Process each JSON file in the directory
for filename in os.listdir(lang_dir):
    if filename.endswith(".json"):
        filepath = os.path.join(lang_dir, filename)
        
        # Skip en.json as it already has the translations
        if filename == "en.json":
            print(f"Skipping {filename} (already updated)")
            continue
        
        try:
            # Read the JSON file
            with open(filepath, 'r', encoding='utf-8') as f:
                data = json.load(f)
            
            # Add new keys if they don't exist
            added_count = 0
            for key, value in new_keys.items():
                if key not in data:
                    data[key] = value
                    added_count += 1
            
            if added_count > 0:
                # Write back to file with proper formatting
                with open(filepath, 'w', encoding='utf-8') as f:
                    json.dump(data, f, ensure_ascii=False, indent=4)
                print(f"Updated {filename}: added {added_count} key(s)")
            else:
                print(f"Skipped {filename}: all keys already exist")
                
        except Exception as e:
            print(f"Error processing {filename}: {e}")

print("\nTranslation update complete!")
