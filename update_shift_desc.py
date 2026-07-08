import json
import os

LANG_DIR = r"c:\Users\Daria\Desktop\FlairX-Mod-Manager\FlairX-Mod-Manager\Language"

KEY = "SettingsPage_AutoDeactivateConflictingMods_Description"

updates = {
    "en.json":    "Auto-deactivate conflicting mods. Hold Shift to ignore this setting.",
    "pl.json":    "Automatycznie deaktywuj kolidujące mody. Przytrzymaj Shift aby ignorować to ustawienie.",
    "cs.json":    "Automaticky deaktivovat kolidující mody. Podržte Shift pro ignorování tohoto nastavení.",
    "da.json":    "Deaktiver automatisk konfliktende mods. Hold Shift for at ignorere denne indstilling.",
    "de.json":    "Konfliktende Mods automatisch deaktivieren. Shift gedrückt halten, um diese Einstellung zu ignorieren.",
    "el.json":    "Αυτόματη απενεργοποίηση mods σε σύγκρουση. Κρατήστε Shift για να αγνοήσετε αυτή τη ρύθμιση.",
    "es.json":    "Desactivar automáticamente mods en conflicto. Mantén Shift para ignorar esta opción.",
    "fi.json":    "Deaktivoi ristiriitaiset modit automaattisesti. Pidä Shift pohjassa ohittaaksesi tämän asetuksen.",
    "fr.json":    "Désactiver automatiquement les mods en conflit. Maintenez Shift pour ignorer ce paramètre.",
    "hi.json":    "टकराने वाले मॉड को स्वचालित रूप से निष्क्रिय करें। इस सेटिंग को अनदेखा करने के लिए Shift दबाए रखें।",
    "hu.json":    "Ütköző modok automatikus deaktiválása. Tartsd lenyomva a Shift-et a beállítás figyelmen kívül hagyásához.",
    "id.json":    "Nonaktifkan mod yang konflik secara otomatis. Tahan Shift untuk mengabaikan pengaturan ini.",
    "it.json":    "Disattiva automaticamente i mod in conflitto. Tieni premuto Shift per ignorare questa impostazione.",
    "ja.json":    "競合するModを自動的に無効化します。Shiftを押しながら操作するとこの設定を無視します。",
    "ko.json":    "충돌하는 모드를 자동으로 비활성화합니다. Shift를 누른 채로 이 설정을 무시합니다.",
    "nl.json":    "Conflicterende mods automatisch deactiveren. Houd Shift ingedrukt om deze instelling te negeren.",
    "no.json":    "Deaktiver konflikterende mods automatisk. Hold Shift for å ignorere denne innstillingen.",
    "pt-BR.json": "Desativar mods conflitantes automaticamente. Segure Shift para ignorar esta configuração.",
    "pt.json":    "Desativar mods em conflito automaticamente. Mantenha Shift premido para ignorar esta definição.",
    "ro.json":    "Dezactivează automat modurile conflictuale. Ține apăsat Shift pentru a ignora această setare.",
    "ru.json":    "Автоматически деактивировать конфликтующие моды. Удерживайте Shift для игнорирования этой настройки.",
    "sv.json":    "Avaktivera konfliktande mods automatiskt. Håll Shift nedtryckt för att ignorera denna inställning.",
    "th.json":    "ปิดใช้งานมอดที่ขัดแย้งโดยอัตโนมัติ กด Shift ค้างไว้เพื่อเพิกเฉยต่อการตั้งค่านี้",
    "tl.json":    "Awtomatikong i-deactivate ang mga mod na nagko-conflict. Hawakan ang Shift para balewalain ang setting na ito.",
    "tr.json":    "Çakışan modları otomatik devre dışı bırak. Bu ayarı yoksaymak için Shift'i basılı tut.",
    "uk.json":    "Автоматично деактивувати конфліктуючі моди. Утримуйте Shift для ігнорування цього налаштування.",
    "vi.json":    "Tự động vô hiệu hóa các mod xung đột. Giữ Shift để bỏ qua cài đặt này.",
    "zh-CN.json": "自动停用冲突的Mod。按住Shift以忽略此设置。",
    "zh-TW.json": "自動停用衝突的Mod。按住Shift以忽略此設定。",
}

updated = []
skipped = []

for filename, new_desc in updates.items():
    filepath = os.path.join(LANG_DIR, filename)
    if not os.path.exists(filepath):
        skipped.append(f"NOT FOUND: {filename}")
        continue
    with open(filepath, "r", encoding="utf-8") as f:
        data = json.load(f)
    if data.get(KEY) == new_desc:
        skipped.append(f"unchanged: {filename}")
        continue
    data[KEY] = new_desc
    with open(filepath, "w", encoding="utf-8") as f:
        json.dump(data, f, ensure_ascii=False, indent=2)
        f.write("\n")
    updated.append(filename)

print(f"Updated {len(updated)} files:")
for f in updated:
    print(f"  + {f}")
if skipped:
    print(f"\nSkipped {len(skipped)}:")
    for f in skipped:
        print(f"  - {f}")
