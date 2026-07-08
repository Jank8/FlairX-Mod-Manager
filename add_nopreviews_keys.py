import json
import os

LANG_DIR = r"c:\Users\Daria\Desktop\FlairX-Mod-Manager\FlairX-Mod-Manager\Language"

translations = {
    "en.json":    {"ShowNoPreviews": "Show Without Previews", "Category_No_Previews": "No Previews"},
    "pl.json":    {"ShowNoPreviews": "Pokaż bez grafik", "Category_No_Previews": "Bez grafik"},
    "cs.json":    {"ShowNoPreviews": "Zobrazit bez náhledů", "Category_No_Previews": "Bez náhledů"},
    "da.json":    {"ShowNoPreviews": "Vis uden forhåndsvisninger", "Category_No_Previews": "Ingen forhåndsvisninger"},
    "de.json":    {"ShowNoPreviews": "Ohne Vorschaubilder anzeigen", "Category_No_Previews": "Keine Vorschaubilder"},
    "el.json":    {"ShowNoPreviews": "Εμφάνιση χωρίς προεπισκοπήσεις", "Category_No_Previews": "Χωρίς προεπισκοπήσεις"},
    "es.json":    {"ShowNoPreviews": "Mostrar sin vistas previas", "Category_No_Previews": "Sin vistas previas"},
    "fi.json":    {"ShowNoPreviews": "Näytä ilman esikatseluja", "Category_No_Previews": "Ei esikatseluja"},
    "fr.json":    {"ShowNoPreviews": "Afficher sans aperçus", "Category_No_Previews": "Sans aperçus"},
    "hi.json":    {"ShowNoPreviews": "बिना प्रीव्यू के दिखाएं", "Category_No_Previews": "कोई प्रीव्यू नहीं"},
    "hu.json":    {"ShowNoPreviews": "Előnézet nélküliek megjelenítése", "Category_No_Previews": "Nincs előnézet"},
    "id.json":    {"ShowNoPreviews": "Tampilkan tanpa pratinjau", "Category_No_Previews": "Tanpa pratinjau"},
    "it.json":    {"ShowNoPreviews": "Mostra senza anteprime", "Category_No_Previews": "Senza anteprime"},
    "ja.json":    {"ShowNoPreviews": "プレビューなしを表示", "Category_No_Previews": "プレビューなし"},
    "ko.json":    {"ShowNoPreviews": "미리보기 없는 항목 표시", "Category_No_Previews": "미리보기 없음"},
    "nl.json":    {"ShowNoPreviews": "Toon zonder voorbeeldafbeeldingen", "Category_No_Previews": "Geen voorbeeldafbeeldingen"},
    "no.json":    {"ShowNoPreviews": "Vis uten forhåndsvisninger", "Category_No_Previews": "Ingen forhåndsvisninger"},
    "pt-BR.json": {"ShowNoPreviews": "Mostrar sem prévias", "Category_No_Previews": "Sem prévias"},
    "pt.json":    {"ShowNoPreviews": "Mostrar sem pré-visualizações", "Category_No_Previews": "Sem pré-visualizações"},
    "ro.json":    {"ShowNoPreviews": "Afișează fără previzualizări", "Category_No_Previews": "Fără previzualizări"},
    "ru.json":    {"ShowNoPreviews": "Показать без превью", "Category_No_Previews": "Без превью"},
    "sv.json":    {"ShowNoPreviews": "Visa utan förhandsvisningar", "Category_No_Previews": "Inga förhandsvisningar"},
    "th.json":    {"ShowNoPreviews": "แสดงที่ไม่มีภาพตัวอย่าง", "Category_No_Previews": "ไม่มีภาพตัวอย่าง"},
    "tl.json":    {"ShowNoPreviews": "Ipakita ang walang preview", "Category_No_Previews": "Walang preview"},
    "tr.json":    {"ShowNoPreviews": "Önizlemesiz göster", "Category_No_Previews": "Önizleme yok"},
    "uk.json":    {"ShowNoPreviews": "Показати без превью", "Category_No_Previews": "Без превью"},
    "vi.json":    {"ShowNoPreviews": "Hiển thị không có xem trước", "Category_No_Previews": "Không có xem trước"},
    "zh-CN.json": {"ShowNoPreviews": "显示无预览图的", "Category_No_Previews": "无预览图"},
    "zh-TW.json": {"ShowNoPreviews": "顯示無預覽圖的", "Category_No_Previews": "無預覽圖"},
}

updated = []
skipped = []

for filename, new_keys in translations.items():
    filepath = os.path.join(LANG_DIR, filename)
    if not os.path.exists(filepath):
        skipped.append(f"NOT FOUND: {filename}")
        continue
    with open(filepath, "r", encoding="utf-8") as f:
        data = json.load(f)
    changed = False
    for key, value in new_keys.items():
        if key not in data:
            data[key] = value
            changed = True
    if changed:
        with open(filepath, "w", encoding="utf-8") as f:
            json.dump(data, f, ensure_ascii=False, indent=2)
            f.write("\n")
        updated.append(filename)
    else:
        skipped.append(f"already has keys: {filename}")

print(f"Updated {len(updated)} files:")
for f in updated:
    print(f"  + {f}")
if skipped:
    print(f"\nSkipped {len(skipped)}:")
    for f in skipped:
        print(f"  - {f}")
