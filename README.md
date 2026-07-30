# Crescent Atlas

> [!CAUTION]
> **Development prototype — do not use unless you are participating in testing.**
> This plugin has not completed live North Horn validation and may crash FFXIV
> or Dalamud, display incorrect information, or lose collected observations.

Crescent Atlas is an independent Dalamud plugin for Final Fantasy XIV's
Occult Crescent. It provides a passive field display, event notifications,
and a local observation collector. No BOCCHI source code or data is included.

The first release is a North Horn data-collection build. It never automates
movement, interaction, or combat and never uploads observations.

## Current features

- Enumerates regular treasure candidate positions from the active client layout.
- Detects currently loaded treasure objects.
- Draws a bright direct line and distance from the player to loaded treasures
  within 120 yalms, both on the dedicated atlas and over the game view.
- Updates guide-line player tracking every rendered frame for smooth movement.
- Draws an FFXIV-style player arrow that rotates with the character's live
  facing direction.
- Always highlights the nearest known treasure candidate with an orange guide
  line when no treasure is currently present at that unchecked spot.
- Uses green guide lines for loaded treasures and orange guide lines for
  unchecked candidate locations where no treasure is known to be present.
- Tracks the current field-entry route: treasure candidates become checked
  after the player comes within 35 yalms, with unchecked points shown in cyan
  and checked points in green.
- Marks fixed silver-coffer points with a separate silver diamond and records
  discovered silver treasures with `cofferType: silver`.
- Emphasizes treasure candidate points with bright filled markers and rings.
- Reads active FATEs through Dalamud's public `IFateTable` API.
- Draws active FATE and Critical Encounter markers with their native game map
  icons when the client exposes a valid icon ID.
- Reads active Critical Encounters through a read-only, fail-closed client view.
- Records Event Objects as discovery candidates so carrot IDs can be confirmed
  from live North Horn sessions.
- Learns Magic Pot timing per field session: one observation produces a
  provisional estimate; two observations use the measured interval and
  alternating location.
- Seeds the live Magic Pot predictor from two independently collected North
  Horn observations, then shows the alternating predicted point and countdown
  directly on the atlas.
- Shows all confirmed markers in a dedicated click-through atlas without
  opening or controlling the standard game map.
- Uses the current in-game map texture and Dalamud's standard map-coordinate
  conversion so north, south, scale, and offsets match the game map.
- Zooms the atlas from 100% to 400% with the mouse wheel in layout mode.
- Starts upgraded installations in layout mode so dragging any window edge or
  corner resizes the atlas directly.
- Provides a persisted map-opacity slider while the atlas is in layout mode.
- Keeps the atlas field free of marker-name and distance text; marker meanings
  remain available in the compact legend.

## Collection data

Data is written below Dalamud's Crescent Atlas configuration directory:

- `collection/sessions/<session>.jsonl` — append-only observations for one run.
- `collection/snapshot.json` — deduplicated aggregate across runs.

Collection files are shared manually only after you review them. They do not
intentionally contain character names, account IDs, world names, or chat.

## Commands

- `/catlas` or `/catlas map` toggles the atlas.
- `/catlas collect on` enables collection.
- `/catlas collect off` disables collection.
- `/catlas click` switches between click-through and layout mode.
- `/catlas flush` flushes observations to disk.
- `/catlas folder` prints the collection output directory.
- `/catlas status` prints current collection status.

## Validation boundary

The project builds against Dalamud API 15 and its pure Magic Pot prediction
logic has offline smoke tests. North Horn territory identity, map calibration,
carrot IDs, Magic Pot FATE IDs, and live-game behavior still require validation
inside FFXIV before any collected dataset is treated as authoritative.

## License

MIT
