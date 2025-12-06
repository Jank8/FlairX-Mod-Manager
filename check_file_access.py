#!/usr/bin/env python3
"""
Skrypt do sprawdzania poprawności użycia FileAccessQueue w projekcie.
Szuka miejsc gdzie mod.json jest zapisywany bez użycia kolejki lub z race condition.
"""

import os
import re
from pathlib import Path
from dataclasses import dataclass
from typing import List, Tuple

@dataclass
class Issue:
    file: str
    line: int
    issue_type: str
    code: str
    context: str

def find_cs_files(root_dir: str) -> List[Path]:
    """Znajdź wszystkie pliki .cs w projekcie"""
    cs_files = []
    for root, dirs, files in os.walk(root_dir):
        # Pomijaj foldery bin, obj, .vs
        dirs[:] = [d for d in dirs if d not in ['bin', 'obj', '.vs', '.git']]
        for file in files:
            if file.endswith('.cs'):
                cs_files.append(Path(root) / file)
    return cs_files

def analyze_file(filepath: Path) -> List[Issue]:
    """Analizuj plik pod kątem problemów z dostępem do plików"""
    issues = []
    
    try:
        content = filepath.read_text(encoding='utf-8')
    except:
        return issues
    
    lines = content.split('\n')
    
    # Wzorce do szukania
    patterns = {
        # Bezpośredni zapis bez kolejki
        'direct_write': r'File\.WriteAllText(?:Async)?\s*\(\s*(?!.*FileAccessQueue)',
        # Bezpośredni odczyt bez kolejki (potencjalny problem jeśli potem jest zapis)
        'direct_read': r'File\.ReadAllText(?:Async)?\s*\(\s*(?!.*FileAccessQueue)',
        # Użycie FileAccessQueue.WriteAllTextAsync (może być OK lub nie)
        'queue_write': r'FileAccessQueue\.WriteAllText(?:Async)?',
        # Użycie FileAccessQueue.ReadAllTextAsync
        'queue_read': r'FileAccessQueue\.ReadAllText(?:Async)?',
        # Użycie FileAccessQueue.ExecuteAsync (atomowe - OK)
        'queue_execute': r'FileAccessQueue\.ExecuteAsync',
    }
    
    # Szukaj wzorców związanych z mod.json (nie active_mods.json)
    mod_json_pattern = r'(?<!active_)mod\.json|modJsonPath'
    
    in_execute_block = False
    execute_depth = 0
    
    for i, line in enumerate(lines, 1):
        # Sprawdź czy jesteśmy w bloku ExecuteAsync
        if 'FileAccessQueue.ExecuteAsync' in line:
            in_execute_block = True
            execute_depth = line.count('{') - line.count('}')
        elif in_execute_block:
            execute_depth += line.count('{') - line.count('}')
            if execute_depth <= 0:
                in_execute_block = False
        
        # Szukaj bezpośrednich zapisów do mod.json
        if re.search(mod_json_pattern, line, re.IGNORECASE):
            # Sprawdź kontekst - kilka linii przed i po
            start = max(0, i - 5)
            end = min(len(lines), i + 5)
            context_lines = lines[start:end]
            context = '\n'.join(context_lines)
            
            # Sprawdź czy jest bezpośredni File.WriteAllText
            if re.search(r'File\.WriteAllText', line) and not in_execute_block:
                issues.append(Issue(
                    file=str(filepath),
                    line=i,
                    issue_type='CRITICAL: Direct File.WriteAllText outside ExecuteAsync',
                    code=line.strip(),
                    context=context
                ))
            
            # Sprawdź czy jest FileAccessQueue.WriteAllTextAsync (potencjalny race condition)
            if re.search(r'FileAccessQueue\.WriteAllText', line):
                # Sprawdź czy wcześniej był odczyt bez ExecuteAsync
                prev_context = '\n'.join(lines[max(0, i-20):i])
                if re.search(r'FileAccessQueue\.ReadAllText|File\.ReadAllText', prev_context):
                    if not re.search(r'FileAccessQueue\.ExecuteAsync', prev_context):
                        issues.append(Issue(
                            file=str(filepath),
                            line=i,
                            issue_type='WARNING: Read then Write without ExecuteAsync (race condition)',
                            code=line.strip(),
                            context=context
                        ))
    
    # Dodatkowa analiza - szukaj wzorca read-modify-write bez ExecuteAsync
    # Szukaj bloków gdzie jest ReadAllText, potem modyfikacja, potem WriteAllText
    content_no_comments = re.sub(r'//.*$', '', content, flags=re.MULTILINE)
    content_no_comments = re.sub(r'/\*.*?\*/', '', content_no_comments, flags=re.DOTALL)
    
    # Szukaj metod które operują na mod.json
    method_pattern = r'(async\s+)?(?:Task|void)\s+\w+.*?mod\.?json.*?\{([^{}]*(?:\{[^{}]*\}[^{}]*)*)\}'
    
    return issues

def check_specific_patterns(filepath: Path) -> List[Issue]:
    """Sprawdź specyficzne wzorce problemów"""
    issues = []
    
    try:
        content = filepath.read_text(encoding='utf-8')
    except:
        return issues
    
    lines = content.split('\n')
    
    # Szukaj wzorca: ReadAllText -> Deserialize -> modyfikacja -> Serialize -> WriteAllText
    # bez użycia ExecuteAsync
    
    i = 0
    while i < len(lines):
        line = lines[i]
        
        # Szukaj odczytu mod.json (nie active_mods.json)
        if re.search(r'(File\.ReadAllText|FileAccessQueue\.ReadAllText).*modJson', line, re.IGNORECASE):
            # Sprawdź następne 30 linii czy jest zapis
            for j in range(i+1, min(i+30, len(lines))):
                next_line = lines[j]
                
                # Jeśli natrafimy na ExecuteAsync, to OK
                if 'FileAccessQueue.ExecuteAsync' in next_line:
                    break
                
                # Jeśli natrafimy na WriteAllText bez bycia w ExecuteAsync
                if re.search(r'(File\.WriteAllText|FileAccessQueue\.WriteAllText).*modJson', next_line, re.IGNORECASE):
                    # Sprawdź czy jesteśmy w bloku ExecuteAsync
                    block_start = max(0, i-10)
                    block_content = '\n'.join(lines[block_start:j+1])
                    
                    if 'FileAccessQueue.ExecuteAsync' not in block_content:
                        context = '\n'.join(lines[max(0,i-2):min(len(lines),j+3)])
                        issues.append(Issue(
                            file=str(filepath),
                            line=i,
                            issue_type='POTENTIAL RACE CONDITION: Read-Modify-Write without ExecuteAsync',
                            code=f"Read at line {i}, Write at line {j}",
                            context=context
                        ))
                    break
        i += 1
    
    return issues

def main():
    print("=" * 80)
    print("FileAccessQueue Usage Checker")
    print("Sprawdzanie poprawności użycia kolejki dostępu do plików")
    print("=" * 80)
    print()
    
    root_dir = "FlairX-Mod-Manager"
    
    if not os.path.exists(root_dir):
        print(f"ERROR: Katalog {root_dir} nie istnieje!")
        return
    
    cs_files = find_cs_files(root_dir)
    print(f"Znaleziono {len(cs_files)} plików .cs")
    print()
    
    all_issues = []
    
    for filepath in cs_files:
        issues = analyze_file(filepath)
        issues.extend(check_specific_patterns(filepath))
        all_issues.extend(issues)
    
    # Grupuj po pliku
    issues_by_file = {}
    for issue in all_issues:
        if issue.file not in issues_by_file:
            issues_by_file[issue.file] = []
        issues_by_file[issue.file].append(issue)
    
    if not all_issues:
        print("✅ Nie znaleziono problemów z dostępem do plików!")
    else:
        print(f"⚠️  Znaleziono {len(all_issues)} potencjalnych problemów:")
        print()
        
        for filepath, issues in issues_by_file.items():
            rel_path = os.path.relpath(filepath, root_dir)
            print(f"📁 {rel_path}")
            print("-" * 60)
            
            for issue in issues:
                print(f"  Line {issue.line}: {issue.issue_type}")
                print(f"  Code: {issue.code[:100]}...")
                print()
            
            print()
    
    # Podsumowanie
    print("=" * 80)
    print("PODSUMOWANIE")
    print("=" * 80)
    
    critical = sum(1 for i in all_issues if 'CRITICAL' in i.issue_type)
    warnings = sum(1 for i in all_issues if 'WARNING' in i.issue_type or 'POTENTIAL' in i.issue_type)
    
    print(f"  CRITICAL: {critical}")
    print(f"  WARNINGS: {warnings}")
    print()
    
    if critical > 0:
        print("❌ Znaleziono krytyczne problemy wymagające naprawy!")
    elif warnings > 0:
        print("⚠️  Znaleziono ostrzeżenia - sprawdź czy są to faktyczne problemy")
    else:
        print("✅ Wszystko wygląda OK!")

if __name__ == "__main__":
    main()
