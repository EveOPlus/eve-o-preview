# Damage symbols

Editable, monochrome SVG adaptations of the damage silhouettes illustrated in
[EVE's Damage Types and Resistances reference](https://support.eveonline.com/hc/en-us/articles/203280501-Damage-Types-and-Resistances):
electromagnetic lightning, three thermal heat trails, the symmetric kinetic fan
converging at a right-facing impact point, and an explosive burst.
These are vector redraws, not official FC SVG exports or
pixel-identical reproductions of the shaded client textures. EVE Online and the
referenced icon designs belong to FC.

Each SVG uses a 16 × 16 viewBox and filled polygons in `currentColor`, without a
background. `OverlaySymbols` loads these embedded polygons once. Both renderers
use this exact geometry with the user's ARGB colour and a contrast outline; no
SVG engine, disk access or asset parsing runs in frame callbacks. Keep edits to
this simple polygon subset, or extend both consumers deliberately.

The original outline symbols remain separate Advanced choices. New presets use
these four EVE-style symbols; Unknown and Mixed stay distinct.

The September 2026 review compared all four vectors with the official reference
image linked above. EM, thermal and explosive retain their recognisable silhouettes;
kinetic has tapered trails converging on the right-facing impact point. Keep it
distinct from the older generic `Kinetic` choice.

Weapon platforms use [separate SVGs based on T1 ammunition](../Weapons/README.md),
with one fixed icon per platform. Repair stroke symbols in `OverlaySymbols` use
module references from the [FC Image Server](https://developers.eveonline.com/docs/services/image-server/):
shield repair 3588, armour repair 26912 and hull repair 4299. They simplify the
module silhouette for small monochrome graphics.
