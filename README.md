# Crescent Atlas

Crescent Atlas is a display-only Dalamud plugin for Final Fantasy XIV's Occult Crescent. It combines a dedicated field atlas, treasure and event tracking, Magic Pot predictions and alerts, Magical Elixir search assistance, visit history, and optional local observation collection.

> [!CAUTION]
> **Crescent Atlas is a beta plugin from v0.1.81 onward.** It remains under active field validation. Every reasonable precaution is taken to avoid crashes, incorrect markers or timers, and local data loss, but these outcomes cannot be guaranteed against. Treat predictions and inferred locations as guidance rather than authoritative game data, and use the plugin at your own risk.

Crescent Atlas does not automate movement, interaction, combat, or item use. It does not upload observations automatically. No BOCCHI source code or data is included.

## Install

Add this URL to Dalamud's custom plugin repositories:

```text
https://raw.githubusercontent.com/miqote69/CrescentAtlas/main/repo.json
```

Open the plugin with:

```text
/catlas
```

See the [latest release](https://github.com/miqote69/CrescentAtlas/releases/latest) for the current version.

## Current Features

### Dedicated Occult Crescent atlas

- Uses the current game map texture and standard map-coordinate conversion.
- Shows an FFXIV-style player arrow with live position and facing direction.
- Supports drag-to-pan, mouse-wheel zoom, edge resizing, window pinning, click-through mode, and collapsible controls.
- Displays an **Outside Occult Crescent** notice without a map background when the player is elsewhere.
- Switches between surface and subterranean marker layers so surface-only markers do not remain on the underground map.
- Keeps fixed aetheryte markers visible on the appropriate map.
- Provides separate pages for the map, icon guide, sound settings, and visit history.
- Supports Japanese and English UI text and shows the plugin version in the menu bar.

### Treasure and carrot tracking

- Discovers bronze and silver treasure candidate positions from the active client layout.
- Detects loaded treasure objects and classifies known silver coffers separately.
- Marks candidate points as checked after the player enters the current 70-yalm detection range and the object is not present.
- Persists checked points for the current island visit and provides a **Reset treasure checks** button.
- Draws optional map and field guide lines to the nearest enabled treasure target; disabling a treasure category also disables its guide target.
- Shows confirmed carrot locations and detects active nearby carrots with a carrot marker and guide line.
- Filters invalid underground staging objects and separates surface and subterranean treasure candidates by elevation.

### FATE and Critical Encounter tracking

- Reads active FATEs through Dalamud's public FATE table.
- Reads Critical Encounters through a read-only, fail-closed dynamic-event view.
- Uses native game map icons when the client exposes a valid icon.
- Shows event names, remaining time or registration countdown, and progress.
- Provides compact and detailed event-display modes.
- Can notify in chat when configured FATEs, Critical Encounters, carrots, or treasures are detected.

### Magic Pot prediction and alerts

- Learns Magic Pot timing independently for each recorded island instance.
- Requires a live observation on a new island before showing that island's prediction.
- Uses a provisional interval after the first observation and measured plausible intervals after later observations.
- Alternates between confirmed Magic Pot event locations when the observations support it.
- Hides the next prediction while a Magic Pot FATE is currently active.
- Shows the predicted location, predicted local time, countdown, and confidence context on the atlas.
- Supports 3-minute, 1-minute, and appearance alerts as one selectable sound set.
- Offers FFXIV chat sound effects (`<se.1>` through `<se.16>`) or bundled Japanese and English voice sets.

### Magical Elixir assistance

- Detects when Magical Elixir guidance is active.
- Reads confirmed active destination markers from the game's map-agent data when available.
- Parses Japanese and English direction and distance hints from the game message.
- Narrows known target candidates from repeated hints.
- Estimates an unknown destination from multiple observations and displays uncertainty instead of presenting an early estimate as exact.
- Keeps the inferred destination visible long enough to follow and records confirmed bronze, silver, or gold Pot reward destinations locally.

### Visit history and local collection

- Records Occult Crescent entry and exit times and displays them newest first.
- Uses the projected content end time as a best-effort island identifier so separate island instances are not intentionally mixed.
- Stores append-only session observations, a deduplicated snapshot, and visit history under the plugin configuration directory.
- Keeps collection data local until the user chooses to inspect or share it.

## Map Controls and Filters

The expanded map controls provide independent visibility toggles for:

- Bronze chests
- Silver chests
- Carrots
- FATEs
- Critical Encounters
- FATE/CE details
- Forked Tower
- Magic Pot prediction
- Treasure guide lines

Use the title-bar menu button to collapse the controls. The gear button contains window pinning and click-through settings.

## Commands

| Command | Action |
| --- | --- |
| `/catlas` or `/catlas map` | Toggle the atlas. |
| `/catlas collect on` | Enable local observation collection. |
| `/catlas collect off` | Disable local observation collection. |
| `/catlas click` | Toggle click-through mode. |
| `/catlas flush` | Flush pending observations to disk. |
| `/catlas folder` | Print the collection directory. |
| `/catlas status` | Print field, visit, collection, and map status. |
| `/catlas log` | Print the independent diagnostic log path. |

## Local Data and Privacy

Data is stored below Dalamud's Crescent Atlas plugin configuration directory:

- `collection/sessions/<session>.jsonl` — append-only observations for one plugin run.
- `collection/snapshot.json` — deduplicated aggregate observations.
- `collection/island-visits.json` — local entry, exit, duration, and best-effort island identity records.
- `diagnostics/bootstrap.log` — initialization, runtime, and unload diagnostics independent of the main Dalamud log.

The plugin does not intentionally collect character names, account IDs, world names, or chat history as part of its observation dataset. Direction messages used for Magical Elixir inference are processed for the active search and are not uploaded automatically.

## Current Limitations

- Candidate and fixed-location datasets are still being expanded through live field validation.
- Magic Pot times and locations can be provisional after limited observations or after entering a different island instance.
- Magical Elixir estimates are best-effort inferences until the game exposes or loads the destination marker.
- Critical Encounter countdowns depend on read-only client state and can be unavailable or temporarily inaccurate after game updates.
- Objects outside the client's current loading range cannot be confirmed as present.
- FFXIV, Dalamud, or FFXIVClientStructs updates can temporarily break map, event, or native client views.

## Documentation

- [Project Wiki](https://github.com/miqote69/CrescentAtlas/wiki)
- [Latest release](https://github.com/miqote69/CrescentAtlas/releases/latest)
- [Issues](https://github.com/miqote69/CrescentAtlas/issues)

## License

Crescent Atlas is licensed under the [MIT License](LICENSE).
