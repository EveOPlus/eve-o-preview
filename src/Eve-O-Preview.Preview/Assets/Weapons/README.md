# Weapon platform symbols

One fixed, monochrome SVG per weapon platform. T1 ammunition is a visual reference
only: the icon never changes with the loaded or reported ammunition. Each uses
the shared 16 x 16 polygon format, `currentColor` and a transparent background.
The symbols are embedded and loaded once by `OverlaySymbols`; settings samples,
native thumbnails and portable previews use the same geometry without downloads.

The reference images were viewed directly from the
[FC Image Server](https://developers.eveonline.com/docs/services/image-server/),
using `https://images.evetech.net/types/{typeID}/icon?size=64`.

| Platform | T1 reference | Type ID | Simplified shape |
| --- | --- | --- | --- |
| Rocket | Scourge Rocket | 266 | Short upright rocket with fins and exhaust |
| Missile | Scourge Light Missile | 210 | Long diagonal finned missile |
| Torpedo | Scourge Torpedo | 267 | Heavy capsule with a separate tail |
| Blaster | Antimatter Charge S | 222 | Charge box and single cartridge |
| Railgun | Iron Charge L | 231 | Three cylindrical charges |
| Laser | Multifrequency S | 246 | Round lens with a reflection |
| Autocannon | Fusion S | 183 | Pointed round and ammunition belt |
| Artillery | EMP L | 201 | Two large pointed shells |
| Disintegrator | Tetryon Exotic Plasma S | 47885 | Three segments of a plasma cell |
| Drone | Hobgoblin I | 2454 | Winged drone silhouette; drones have no ammunition |
| Smartbomb | Large EMP Smartbomb I | 3993 | Radial pulse; smartbombs have no ammunition |
| Light missile | Scourge Light Missile | 210 | Slim body with a small tail |
| Heavy missile | Scourge Heavy Missile | 209 | Broad horizontal missile and paired fins |
| Heavy assault missile | Scourge Heavy Assault Missile | 20307 | Short heavy body with separated side fins |
| Cruise missile | Scourge Cruise Missile | 203 | Long cross-wing silhouette |
| XL cruise missile | Scourge XL Cruise Missile | 32436 | Long body and swept wings |
| XL torpedo | Scourge XL Torpedo | 17859 | Wide capsule, side stabilizers and separate exhaust |
| Pulse laser | Multifrequency S | 246 | Separated lens pulses |
| Beam laser | Radio S | 239 | Continuous beam leaving a curved lens |
| Fighter | Firbolg I | 23059 | Forward fuselage with swept wings and twin tips |
| Vorton | GalvaSurge Condenser Pack S | 54769 | Condenser stack and electrical arc |
| Bomb | Scorch Bomb | 27916 | Banded canister |
| Guided bomb | Standup Heavy Guided Bomb | 37849 | Canister flanked by guide fins |
| Structure missile | Standup Heavy Missile | 37847 | Paired missile rack |
| Point defense | Standup Flak Round I | 63195 | Central round with outward flak bursts |
| Doomsday | Judgment / Standup Arcing Vorton Projector I | 24550 / 35928 | Central destructive burst |
| Lance | Holy Destiny Electromagnetic Lance | 40631 | Narrow piercing beam |
| Reaper | Divine Harvest Electromagnetic Reaper | 40632 | Swept crescent beam |
| Bosonic field | Bosonic Field Generator | 40633 | Expanding cone |
| Defender missile | Defender Missile I | 32782 | Interceptor within a protective bracket |
| Hybrid fallback | Antimatter Charge S | 222 | Charge case and cartridge |
| Projectile fallback | Fusion S | 183 | Single round and cartridge rim |

The added references were inspected on 2026-09-14 against FC build 3503375.
Superweapons share the same skull reference icon; their individual silhouettes
are conceptual drawings of the attack shape. Generic Missile and Laser assets
remain distinct fallbacks. Utility bombs share Bomb, rapid launchers share their
ammunition family, and all fighter subtypes share Fighter. There are 32 fixed
platform assets; no damage/faction/meta/size-specific alternatives are selected.

Shapes, proportions and orientation deliberately differ from the shaded item
images where that helps distinguish platforms at 12-24 pixels. Hybrid and
projectile platforms share ammunition families, so the chosen variants are design
references, not claims about which ammunition a weapon accepts. EVE Online and
the referenced designs belong to FC; these are original simplified redraws, not
official SVG exports. Downloaded reference PNGs are not shipped with the app.

Keep persisted symbol values stable. Existing symbol IDs now use these assets;
Torpedo and Smartbomb were appended so old enum values and saved custom choices
remain valid. All further platform/symbol enum values were appended. Projectile
retains its existing numeric symbol and now uses the shared filled polygon path.
Presets provide a distinct icon for every identified platform.
User-selected custom symbols remain custom.
