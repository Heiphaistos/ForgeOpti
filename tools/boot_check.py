"""Cherche les tweaks qui modifient le démarrage d'un service/pilote de DEMARRAGE ou SYSTEME (Start 0/1) :
modifier ceux-là peut empêcher Windows de démarrer (INACCESSIBLE_BOOT_DEVICE 0x7B).

Usage : python tools/boot_check.py <dossier avec baseline-probes.json>   (rapport du banc d'essai VM)
        python tools/boot_check.py                                       (liste statique des services ciblés par le catalogue)
"""
import glob
import json
import os
import sys

CAT = os.path.join(os.path.dirname(__file__), '..', 'src', 'ForgeOpti.Core', 'Catalog', '*.json')
SEP = chr(92)

tweaks = {}
for f in glob.glob(CAT):
    for t in json.load(open(f, encoding='utf-8')):
        tweaks[t['id']] = t

base = {}
if len(sys.argv) > 1:
    base = json.load(open(os.path.join(sys.argv[1], 'baseline-probes.json'), encoding='utf-8-sig'))

print('Services ciblés par le catalogue (Start) :')
for tid, t in sorted(tweaks.items()):
    for i, o in enumerate(t['ops']):
        if o['type'] == 'reg' and (SEP + 'services' + SEP) in o['key'].lower() and o['name'].lower() == 'start':
            svc = o['key'].split(SEP)[-1]
            b = base.get(f'{tid}#{i}', '?')
            flag = ''
            if b.split(':')[-1] in ('0', '1', '2'):
                flag = '   <-- demarrage/systeme/auto a l origine'
            print(f"  {tid:48s} {svc:28s} base={b:14s} cible={o['value']}{flag}")
