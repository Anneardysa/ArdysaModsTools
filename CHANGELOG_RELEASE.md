# Release Notes — v2.1.20-beta

> Covers builds **2120 → 2121**

---

## 🐛 Fixed

- Fixed Courier and Ward parsing structure breaking deeply nested properties like `styles` and `alternate_icons`. Re-implemented `ParseTopLevelKeyValues` and `ExtractVisualsKeyValues` with block depth-tracking algorithms to ensure nested properties no longer leak into the top-level merged output.
- Fixed Courier and Ward `particle_create` entries extracting all style particles regardless of the selected style. Now strictly filters and strips the `style` field, ensuring only the target style's ambient particles are injected.
- Fixed Couriers with `alternate_icons` falling back to the default unstyled `/onibi_lvl_00` thumbnail and item name. `BuildMergedCourierBlock` now explicitly overrides `item_name` and `image_inventory` properties by parsing the selected style's metadata before applying the merge.
- Fixed Ethereal effects failing to apply on couriers that already have native particle effects (e.g., Aghanim's Interdimensional Baby Roshan). Ethereal effects now properly replace native particles instead of being blocked by slot limits.

---
