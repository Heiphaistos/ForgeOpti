"""Valide un ou plusieurs fichiers de catalogue candidats contre tout le dossier Catalog.

Usage : python tools/validate-catalog.py src/ForgeOpti.Core/Catalog/import-atlas.json [...]
Vérifie : JSON valide, champs requis, ids uniques (contre tout le catalogue ET entre fichiers candidats),
types de valeurs de registre, CmdOp avec Revert, aucun caractère de contrôle, oneClick interdit pour Avance.
Signale (sans échouer) les emplacements de registre/tâches déjà utilisés par d'autres tweaks (le moteur gère le partage).
"""
import glob, json, os, sys

CAT = os.path.join(os.path.dirname(__file__), '..', 'src', 'ForgeOpti.Core', 'Catalog')
REQ = ['id', 'category', 'name', 'description', 'risk', 'reboot', 'gaming', 'oneClick', 'ops']
KINDS = {'DWord', 'QWord', 'String', 'ExpandString'}


def loc(o):
    if o['type'] == 'reg':
        return f"reg|{o['hive']}|{o['key']}|{o['name']}".lower()
    if o['type'] == 'task':
        return f"task|{o['path']}".lower()
    return None


def main(candidates):
    cand = {os.path.abspath(c) for c in candidates}
    existing_ids, existing_locs = {}, {}
    for f in glob.glob(os.path.join(CAT, '*.json')):
        if os.path.abspath(f) in cand:
            continue
        for t in json.load(open(f, encoding='utf-8')):
            existing_ids[t['id']] = os.path.basename(f)
            for o in t['ops']:
                l = loc(o)
                if l:
                    existing_locs.setdefault(l, t['id'])
    errors, warns, seen = [], [], {}
    total = 0
    for c in candidates:
        try:
            data = json.load(open(c, encoding='utf-8'))
        except Exception as e:
            errors.append(f'{c}: JSON invalide : {e}')
            continue
        for t in data:
            total += 1
            i = t.get('id', '?')
            for k in REQ:
                if k not in t:
                    errors.append(f'{i}: champ manquant {k}')
            if i in existing_ids:
                errors.append(f'{i}: id deja present dans {existing_ids[i]}')
            if i in seen:
                errors.append(f'{i}: id duplique entre candidats')
            seen[i] = c
            if t.get('risk') not in ('Sur', 'Modere', 'Avance'):
                errors.append(f"{i}: risk invalide {t.get('risk')}")
            if t.get('risk') == 'Avance' and t.get('oneClick'):
                errors.append(f'{i}: oneClick interdit pour Avance')
            if not t.get('ops'):
                errors.append(f'{i}: ops vide')
            for o in t.get('ops', []):
                if o['type'] == 'reg':
                    if o['hive'] not in ('HKLM', 'HKCU'):
                        errors.append(f"{i}: hive {o['hive']}")
                    if o['kind'] not in KINDS:
                        errors.append(f"{i}: kind {o['kind']}")
                    if o['kind'] in ('DWord', 'QWord') and not isinstance(o['value'], int):
                        errors.append(f"{i}: valeur {o['kind']} non entiere")
                    if o['kind'] in ('String', 'ExpandString') and not isinstance(o['value'], str):
                        errors.append(f"{i}: valeur String non texte")
                elif o['type'] == 'cmd':
                    if not o.get('revert', '').strip():
                        errors.append(f'{i}: CmdOp sans revert')
                    for s in (o.get('apply', ''), o.get('revert', '')):
                        if any(ord(ch) < 32 and ch not in '\n\r\t' for ch in s):
                            errors.append(f'{i}: caractere de controle dans un script (echappement \\a, \\f... ?)')
                elif o['type'] != 'task':
                    errors.append(f"{i}: type d'op inconnu {o['type']}")
                l = loc(o)
                if l and l in existing_locs:
                    warns.append(f'{i}: emplacement deja utilise par {existing_locs[l]} ({l})')
    print(f'{total} tweaks verifies, {len(errors)} erreurs, {len(warns)} chevauchements (OK, geres par le moteur)')
    for e in errors:
        print('ERREUR', e)
    for w in warns[:40]:
        print('  chevauchement', w)
    return 1 if errors else 0


if __name__ == '__main__':
    sys.exit(main(sys.argv[1:]))
