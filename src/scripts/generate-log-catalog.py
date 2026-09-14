"""Generate the compact offline combat fallback from the managed full SDE index.

Usage: python scripts/generate-log-catalog.py <sde-jsonl.zip> <managed-sde.sqlite>
Build the SQLite index with tests/StaticData.Smoke first. Reusing the production
index keeps NPC gun/missile selection and alias ambiguity rules consistent.
"""
import itertools
import json
import pathlib
import sqlite3
import sys
import zipfile

archive, database = sys.argv[1:]
with sqlite3.connect(pathlib.Path(database).resolve().as_uri() + '?mode=ro', uri=True) as db:
    build = db.execute('SELECT build FROM manifest').fetchone()[0]
    # Keep this in step with StaticDataDatabase.CombatIndexVersion. Opening the
    # installed database via StaticData.Smoke upgrades old derived indexes locally.
    if not db.execute("SELECT 1 FROM sqlite_master WHERE name='combat_index'").fetchone() or db.execute('SELECT version FROM combat_index').fetchone()[0] != 2:
        raise ValueError('Open this database with the current StaticData.Smoke first to upgrade its combat index')
    rows = db.execute('SELECT n.name,c.id,c.npc,c.platform,c.damage FROM names n JOIN combat c ON c.id=n.id ORDER BY n.name')
    npcs, attacks, weapons = [], {}, {}
    for name, matches in itertools.groupby(rows, key=lambda row: row[0]):
        matches = list(matches)
        npc = all(row[2] for row in matches)
        platforms = {row[3] for row in matches}
        damages = {row[4] for row in matches}
        platform = next(iter(platforms)) if len(platforms) == 1 else 0
        types = next(iter(damages)) if len(damages) == 1 else 0
        kind = {0: 0, 1: 1, 2: 2, 4: 3, 8: 4}.get(types, 5)
        info = {'Platform': platform, 'DamageType': kind, 'Types': types, 'TypeId': matches[0][1] if len(matches) == 1 else 0}
        if npc:
            npcs.append(name)
            if types or platform: attacks[name] = info
        elif types or platform:
            weapons[name] = info
with zipfile.ZipFile(archive) as sde:
    meta = json.loads(sde.read('_sde.jsonl').decode().strip())
    if meta['buildNumber'] != build:
        raise ValueError('Archive and database builds differ')
    systems = {name: v['_key'] for line in sde.open('mapSolarSystems.jsonl')
               for v in [json.loads(line)] for name in v.get('name', {}).values()}
out = pathlib.Path(__file__).resolve().parents[1] / 'Eve-O-Preview/Resources/LogCatalog.json'
out.write_text(json.dumps({'Build': build, 'NpcNames': npcs, 'NpcAttacks': attacks, 'Systems': systems, 'Weapons': weapons},
                          ensure_ascii=False, separators=(',', ':')), encoding='utf-8')
print(f'Build {build}: {len(npcs)} NPC aliases, {len(attacks)} NPC attack profiles, {len(weapons)} weapon/item aliases, {len(systems)} system aliases.')
