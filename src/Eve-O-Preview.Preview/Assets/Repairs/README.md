# Repair symbols

Shield, armour and hull follow the supplied reference silhouettes, in that order:
a pointed shield rim, a left-facing helmet with a visor cutout, and a cube showing
its filled top and two side faces. Narrow transparent seams run from the
upper-left corner, upper-right corner and bottom point to the centre of the hull.
Fine shading and small surface details are omitted for readability at 12-24 pixels.
The separate repair arrows are removed; each row already has an IN/OUT prefix.

These are transparent 16 x 16 SVGs with `currentColor` filled polygons, including
negative space in the shield, helmet visor and between cube faces. `OverlaySymbols` loads them once. Native
thumbnails, portable previews and configuration pickers share the same geometry
and the configured repair colour; no image loading happens during frame callbacks.
