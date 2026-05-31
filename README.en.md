<div align="center">

<img src="https://raw.githubusercontent.com/sany86russ/AuroraRMG/main/docs/logo-wide.png" width="520" alt="AuroraRMG"/>

### Random map template generator for **Heroes of Might and Magic: Olden Era**

[![Latest release](https://img.shields.io/github/v/release/sany86russ/AuroraRMG?style=for-the-badge&color=8B7CFF&label=Version)](https://github.com/sany86russ/AuroraRMG/releases/latest)
[![Download](https://img.shields.io/github/downloads/sany86russ/AuroraRMG/total?style=for-the-badge&color=22D3EE&label=Downloads)](https://github.com/sany86russ/AuroraRMG/releases)
[![Platform](https://img.shields.io/badge/Windows-10%20%2F%2011-0E1117?style=for-the-badge&logo=windows)](https://github.com/sany86russ/AuroraRMG/releases/latest)

**Configure your map in a friendly UI instead of hand-editing JSON — then hit "Create template".**

[📥 Download](#-installation) · [🚀 Quick start](#-quick-start) · [🧭 Interface](#-interface-overview) · [⚙️ All settings](#️-all-settings) · [🎲 Presets](#-built-in-presets-43) · [❓ FAQ](#-faq)

[Русский](README.md) · 🌍 **English**

</div>

---

> [!NOTE]
> **Based on [Olden-Era---Template-Generator](https://github.com/KhanDevelopsGames/Olden-Era---Template-Generator/) by [KhanDevelopsGames](https://github.com/KhanDevelopsGames).**
> The idea and the basic approach to building `.rmg.json` templates come from that project — thanks to the original author! AuroraRMG is a reworked version with a new look (the **Aurora** theme), full bilingual localization (**RU/EN**), a visual zone editor, an expanded set of options and its own auto-updater. See [Credits](#-credits--origin).

---

## 🗺 What is this

**AuroraRMG** is a Windows desktop app that creates random-map template files (`.rmg.json`) for **Heroes of Might and Magic: Olden Era**.

A map template is a set of rules the game uses to build a random map when you start a new game: how many players, how zones are connected, how many resources and towns, the terrain, how strong neutral monsters are, and so on. Such files used to be written by hand in JSON. AuroraRMG gives you a visual editor: drag sliders and toggles → press **"Create template"** → get a ready `.rmg.json` that shows up in the game's template list.

**Key features:**

| | |
|---|---|
| 🎲 **43 built-in presets** | 1v1 Classic, 1v1 Single-hero, FFA for 3–8 players, **PvE 1×2…1×7**, special modes — one-click load |
| 🗺 **Visual zone editor** | interactive canvas: zones, connections, inspector, PNG export — edit the map graph by hand |
| 🌍 **Two languages (RU/EN)** | instant language switch in the header, no restart; auto-detected from the OS locale |
| 🚫 **Item, spell & hero bans** | full hero roster with real names (optionally from the game's assets) |
| 🧩 **Full control** | zone topology, economy, monster strength, terrain, water, victory conditions |
| 🖼 **Map preview** | a visual schematic of zone placement and connections before you save |
| 🌑 **Aurora theme** | dark neon UI (violet → cyan) with branded menus |
| 🔄 **Auto-update** | the app finds and installs new versions from GitHub by itself |
| 📦 **Single file** | self-contained `.exe`, no installation |
| 🧭 **Auto-locate game** | finds the Olden Era templates folder via the Steam registry |

---

## 📥 Installation

1. Open the **[latest release](https://github.com/sany86russ/AuroraRMG/releases/latest)** page.
2. Download **`AuroraRMG.exe`** from the *Assets* section.
3. Run it. Done — no installation, it's a self-contained single `.exe`.

> [!IMPORTANT]
> **Requirements:** Windows 10 or 11 (64-bit). You do **not** need to install a separate .NET runtime — it's bundled inside the `.exe` (self-contained build).

> [!TIP]
> On first launch Windows SmartScreen may warn about an "unknown publisher" (the file isn't signed with a paid certificate). Click **"More info" → "Run anyway"**. The file is about 65 MB. How to verify the file is genuine — below.

---

## 🔐 Verifying the build's authenticity

`AuroraRMG.exe` is **not signed** with a paid publisher certificate (hence the SmartScreen "unknown publisher" warning). But that **doesn't mean it can't be verified** — quite the opposite.

Every release is built **not on someone's home PC, but directly on GitHub's servers** from this open source code — the [`.github/workflows/release.yml`](.github/workflows/release.yml) workflow. During the build GitHub issues a cryptographically signed **build provenance attestation** that firmly ties the specific `.exe` to a specific commit of this repository and a specific build run.

This directly addresses the main fear: *"what if someone took the project, slipped malware into the `.exe` and re-uploaded it"*. A tampered file **won't pass verification** — it won't have a valid attestation from this repository.

**How to verify (needs the free [GitHub CLI](https://cli.github.com/)):**

```bash
gh attestation verify AuroraRMG.exe --repo sany86russ/AuroraRMG
```

If the file is genuine you'll see something like `✓ Verification succeeded!` — meaning **this** binary was built by GitHub from **this** repository's source and hasn't changed since. If verification fails, the file wasn't downloaded from here or was modified — **don't run it**.

> [!TIP]
> More on the technology: GitHub docs — [Artifact Attestations](https://docs.github.com/en/actions/concepts/security/artifact-attestations).

---

## 🔄 Auto-update

AuroraRMG can update itself, no manual downloading.

- **On every launch** the app quietly checks the latest release in this repository.
- If a newer version is available, a **banner** appears at the top of the window:

  > ✨ A new version of AuroraRMG vX.Y is available · **[Update]** · [What's new] · [Later]

- **Update** — the app downloads the new `.exe`, shows a progress window, replaces itself and restarts.
- **What's new** — opens the release page with the changelog.
- **Later** — hides the banner until the next launch.

The current version is always shown **in the window header**, in the badge next to `AuroraRMG`.

> [!NOTE]
> The update check is **skipped** if the app is started with the `--minimized` flag (see [Running without disturbing the game](#-running-without-disturbing-the-game)). If there's no internet or GitHub is unreachable, the app just keeps working.

---

## 🚀 Quick start

**Way 1 — via a preset (fastest):**

1. Launch AuroraRMG.
2. On the **"Rules"** tab, at the very top, in the **TEMPLATE** block, click the **"Preset ▾"** button.
3. Pick a ready-made config from the grouped menu (e.g. *"Standard"* under *1v1 — Classic*).
4. Click **"⚔ Create template"** in the right panel.
5. Click save and store the `.rmg.json` in the game's templates folder.
6. Launch Olden Era → start a new game → pick your template from the list.

**Way 2 — manually:**

1. Set the **name**, **player count** and **map size**.
2. Go through the tabs and tune topology, zones, economy, environment.
3. Click **"⚔ Create template"** — a **preview** of the zone layout appears on the right.
4. Check the validation warnings (if any) and save the file.

---

## 🧭 Interface overview

The window has three areas:

```
┌──────────────────────────────────────────────────────────────┐
│  🧭 AuroraRMG  [v1.0]  —  file_name    💾 💾… 🗺 RU EN  _ ☐ ✕ │   ← Header
├───────────────┬──────────────────────────────┬───────────────┤
│  Rules        │                              │  ⚔ Create      │
│  Map & Zones  │     Selected tab content     │   template     │
│  Bonuses/Bans │                              │  ┌──────────┐  │
│  Extra Content│                              │  │ map      │  │
│  …            │                              │  │ preview  │  │
│ ← navigation →│                              │  └──────────┘  │
│               │                              │  💾 Save       │
└───────────────┴──────────────────────────────┴───────────────┘
   left rail          work area                action panel
```

### Window header

| Element | Action | Shortcut |
|---|---|---|
| 🧭 **AuroraRMG** + version badge | Product name and current version | — |
| *file name* | Current settings file (`*` = unsaved changes) | — |
| 🔄 | Reset all settings (new template) | `Ctrl + N` |
| 📂 | Open saved settings | `Ctrl + O` |
| 💾 | Save settings | `Ctrl + S` |
| 💾… | Save settings as… | `Ctrl + Shift + S` |
| 🗺 **Editor** | Open the visual zone editor | — |
| **RU** / **EN** | Switch UI language (instant) | — |
| `_` `☐` `✕` | Minimize / maximize / close | — |

> "Settings" (💾) is an `.oetgs` file with **all** editor parameters so you can continue later. It is **not** a game template. The game `.rmg.json` is created separately via "Create template" → "Save".

### Left navigation rail — 4 main tabs

| Tab | About |
|---|---|
| **Rules** | Basic map parameters, heroes, victory conditions, environment |
| **Map & Zones** | Topology (map shape) and detailed zone setup |
| **Bonuses & Bans** | Starting bonuses for players, banning items, spells and heroes |
| **Extra Content [EXP.]** | Guaranteed objects in zones (mines, treasures, etc.) |

### How it looks

<details open>
<summary><b>"Rules" tab</b> — basic parameters, heroes, victory conditions, environment</summary>

<img src="https://raw.githubusercontent.com/sany86russ/AuroraRMG/main/docs/ui-rules-en.png" alt="Rules tab" width="100%"/>

</details>

<details>
<summary><b>"Map & Zones" tab</b> — map topology and zone setup</summary>

<img src="https://raw.githubusercontent.com/sany86russ/AuroraRMG/main/docs/ui-map-zones-en.png" alt="Map & Zones tab" width="100%"/>

</details>

<details>
<summary><b>"Bonuses & Bans" tab</b> — starting bonuses and item/spell/hero bans</summary>

<img src="https://raw.githubusercontent.com/sany86russ/AuroraRMG/main/docs/ui-bonuses-bans-en.png" alt="Bonuses & Bans tab" width="100%"/>

</details>

<details>
<summary><b>"Extra Content [EXP.]" tab</b> — guaranteed objects in zones</summary>

<img src="https://raw.githubusercontent.com/sany86russ/AuroraRMG/main/docs/ui-extra-content-en.png" alt="Extra Content tab" width="100%"/>

</details>

> 💡 Screenshots are from the current version in the **Aurora** theme. The UI is available in Russian and English — switch in the header (RU/EN).

---

## ⚙️ All settings

> Values in parentheses are the **range** and **default**. The `[EXP.]` tag marks an experimental feature that may produce unstable results.

### Tab · "Rules"

#### "Template" block

| Setting | Description |
|---|---|
| **Preset** | A grouped menu of 43 ready configs. Picking one fills every field instantly. See [Presets](#-built-in-presets-43). |
| **Template name** | The name the template appears under in the game. |
| **Map size** | Playfield size. Standard values roughly from 80×80 to 240×240. |
| **Experimental large maps** `[EXP.]` | Adds sizes from 256×256 to 512×512. Official maps stop at 240×240, so larger ones may fail to load, freeze or behave unpredictably. |
| **Players** | Number of players *(2 – 8, default 2)*. |

#### "Heroes" block

| Setting | Description |
|---|---|
| **Starting hero limit** | How many heroes you can have at the start *(1 – 12, default 4)*. |
| **Max hero limit** | The hero cap *(1 – 12, default 8)*. |
| **Limit gain per town** | How much the cap grows per captured town *(0 – 20, default 1)*. |
| **Single-hero mode** | Special mode: a player has only one hero. |

> Each hero value can be set with the slider, the **−/+** buttons, or by typing a number (Enter). The maximum is 12, matching the official game templates.

#### "Game rules" block

| Setting | Description |
|---|---|
| **Faction-laws experience** | Faction-laws XP multiplier *(25% – 200%, step 25, default 100%)*. |
| **Astrology experience** | Astrology XP multiplier *(25% – 200%, step 25, default 100%)*. |
| **Main victory condition** | The main win condition (see [table](#-victory-conditions--special-modes)). |
| **Defeat on losing the starting town** | A player is eliminated after losing their starting town. Extra slider **"Town-loss day"** *(1 – 30)*. |
| **Defeat on losing the starting hero** | A player is eliminated after losing their starting hero. |
| **Victory by holding a neutral town** | *City Hold* mode: capture and hold a designated town. Slider **"Days to hold"** *(1 – 30, default 6)*. |
| **Tournament rules** | 1v1 tournament mode (2 players only). Parameters: **points to win** *(1 – 10)*, **first battle on day** *(3 – 30)*, **days between battles** *(3 – 30)*. |

#### "Environment & encounters" block

| Setting | Description |
|---|---|
| **Terrain type** | Map biome: **By faction** (a zone's terrain follows its town's faction — engine default), **Random mix**, or a fixed biome: **Grass, Snow, Lava, Sand, Dirt, Deathland, Autumn**. |
| **Monster aggression** | How guards react: **Passive**, **Normal** (default), **Aggressive**. |
| **Water amount** | Water borders between zones: **None / Some / Medium / Lots**. Water style matches the chosen terrain. |
| **Neutral join chance** | A diplomacy modifier for all zones *(-1.00 … +1.00, default -0.50)*. Higher = neutrals join the player more eagerly. |
| **Terrain roughness** | Obstacle density (rocks, forest) per zone *(0% – 200%, default 100%)*. |
| **Lake amount** | Lake coverage per zone *(0% – 200%, default 100%)*. |
| **Allow guard bypass (holes)** | Enables "holes" in guards — some can be passed without a fight. |

---

### Tab · "Map & Zones"

#### "Topology" block — map shape

| Topology | How zones are placed and connected |
|---|---|
| **Balanced** | Concentric quality rings: players outside, neutrals inside; connections to neighbours on adjacent rings. *(default)* |
| **Random** | Zones at random positions, each connected to all bordering zones (Delaunay-based). |
| **Ring** | All zones in a circle, each linked to two neighbours. |
| **Hub** | All zones connect to a shared central hub; players never border directly. |
| **Chain** | Zones in a line from end to end, not closed. |

Extra (topology-dependent): **Hub zone size** *(0.25× – 3×)*, **Towns in hub** *(0 – 4)*, **Connect only via neutral zones**.

#### "Zone setup" block

Base sliders (always available): **Towns in player zones** *(1 – 4)*, **Towns in neutral zones** *(1 – 4)*, **Resource frequency** *(20% – 400%)*, **Structure frequency** *(20% – 200%)*, **Neutral army strength** *(25% – 300%)*, **Border/portal strength** *(25% – 300%)*, **Generate roads**, **Create remote footholds**, **Create extra portals** (+ **Max portal count** *1 – 32*).

Advanced settings (the **"Advanced settings"** checkbox): per-tier neutral zones (6 sliders — **weak / medium / strong**, each **with/without town**, up to 32 total), **Min. neutrals between players** *(0 – 8)*, **Player/Neutral zone size** `[EXP.]`, **Guard strength spread** *(0% – 50%)*. In simple mode a single **"Extra neutral zones"** *(0 – 30)* slider is available instead.

---

### Tab · "Bonuses & Bans"

| Section | Description |
|---|---|
| **Starting bonuses** | Bonuses granted **equally to all players** at the start. **`+`** to add, **`×`** to remove. |
| **Banned items** | Artifacts that **won't appear** on the map. **`+`** with filter/categories, **`×`** to unban. |
| **Banned spells** | Spells excluded from this map. |
| **Banned heroes** | Heroes that won't appear (`globalBans.heroes`), with per-faction colour tags. |

> [!TIP]
> **Full hero roster with names.** By default a built-in verified list is available. The **"Connect the installed game's assets"** checkbox (with a clear disclaimer) loads the **full 108-hero roster with real names** straight from the installed game's local files (`Core.zip`) — nothing is uploaded to the network. The editor is fully usable without it.

---

### Tab · "Extra Content [EXP.]"

> [!WARNING]
> Experimental section. It defines **mandatory content** — objects **guaranteed** to appear in every zone of the chosen type. Overdoing it can make a map unbalanced or unplayable.

Content is configured separately for five zone types (sub-tabs: **Player zones, Weak / Medium / Strong neutral zones, Hub zones**). Within each, objects are grouped: ◆ **Mines**, ◆ **Treasures**, ◆ **Creature recruitment**, ◆ **Resource banks**, ◆ **Utility structures**, ◆ **Hero development structures**. Each added object has: **Amount**, **Guarded**, **By the town**, **Road distance** *(Any / Adjacent / Near / Medium / Far / Very far)*.

---

## 🖼 Preview panel & generation

| Element | Description |
|---|---|
| **⚔ Create template** | Generates the map from current settings and builds its **preview**. |
| **Validation list** | Problems (e.g. tournament with ≠2 players, or no City-Hold town) show up here as warnings/errors. Errors block saving. |
| **Map preview** | Zone schematic: placement, connections, towns; the City-Hold town is marked with a **gold house icon**. |
| **Outdated warning** | Changing settings after generation marks the preview as outdated — regenerate it. |
| **Save preview next to template** | Checkbox: save the preview image next to the template. |
| **💾 Save** | Saves the finished `.rmg.json` (opens the game's templates folder by default). |

---

## 🗺 Visual zone editor

The **"🗺 Editor"** header button opens an interactive **zone-graph canvas editor** — see and edit the template's structure by hand, in the spirit of community visual editors.

<img src="https://raw.githubusercontent.com/sany86russ/AuroraRMG/main/docs/ui-editor-en.png" alt="Visual zone editor" width="100%"/>

- **Canvas:** zones are nodes (🟢 player · 🔵 hub · 🟤/⚪/🟡 neutral by quality), connections are edges (gold = direct, dashed cyan = portal, dashed brown = road). There's a grid, a **legend** and a controls hint.
- **Navigation:** wheel to zoom (or **−/+** buttons), drag the background to pan, **"Reset view"** and **"Auto-layout"**.
- **Editing:** drag zones; the **inspector** on the right edits the selected zone (name, size, layout, diplomacy, guards) or connection (from/to, type, guards, road).
- **Functions:** **➕ Zone** (or double-click the canvas), **🔗 Connect** (link two zones), **🗑 Delete** (or the `Del` key), **✓ Validate** (dangling links, duplicate names, self-loops, isolated zones), **💾 Save .rmg.json** and **📂 Load**.
- **🖼 Export PNG** — save an image of the zone graph to share.
- Keys: `Del` — delete selection, `Esc` — cancel connecting / clear selection.

> The editor reuses the same layout as the preview, so the graph matches the generated map. Node positions are for clarity only (the `.rmg.json` stores no coordinates — the game computes them).

---

## 🌍 UI language (RU/EN)

AuroraRMG is **fully bilingual** — Russian and English.

- The **RU / EN** switch in the header changes the language **instantly, no restart**.
- On first launch the language is chosen from the Windows locale (Russian system → RU, otherwise EN) and remembered afterwards.
- **Everything** is translated: tabs, buttons, labels, tooltips, descriptions, dropdown values, game-content names, preset names/descriptions, dialogs, messages and the zone editor. Logic values (SIDs, modes, tokens) are never translated — generation stays stable.

---

## 🎲 Built-in presets (43)

Presets focus on fair 1v1 play, but there are FFA, special modes and **PvE** (one player vs several computers). Each is tested: an automated test verifies the generated map really matches the description. Opened via the **"Preset ▾"** button — presets are grouped by mode type in a dropdown menu.

- **🗡 1v1 — Classic (20):** Duel (fast), Standard, Rich lands, City hold, Tournament, Islands (water), Hub, Chain, Isolation, Snowy, Lava, Desert, Hardcore, Peaceful (economy), Two towns, Hub treasury, Portals, Mega-rich (sandbox), Asceticism (survival), Deep water.
- **🛡 1v1 — Single hero (9):** Blitz, Duel, Epic, Hub, Snow blitz, Tournament, City hold, Islands, Chain.
- **👥 FFA / multiplayer (8):** 3/4/6/8 players, Hub variants, King of the Hill (4p), Massacre — fast FFA (4).
- **🤖 PvE — 1 vs AI (6):** 1 vs 2 … 1 vs 7 (one player against 2–7 computers; map size and neutrals scale with sides).

---

## 🏆 Victory conditions & special modes

**Main victory conditions:** Standard (destroy everyone), accumulate resources, accumulate gold, capture a specified town, **City Hold**, **Tournament**. You can additionally enable **defeat on losing the starting town/hero**, independent of the main condition.

**🏰 City Hold** — capture a designated town and hold it for a set number of days. The town is chosen automatically (the hub for "Hub" topology, otherwise the highest-quality neutral zone equidistant from players) and marked with a gold house icon. If no suitable town exists, generation is blocked (see validation).

**⚔ Tournament** — a competitive 1v1 mode with an isolated prep phase. Available **only with exactly 2 players**. Each player starts in a fully isolated, mirrored cluster; neutral zones are balanced between sides. Supports Chain/Ring (two mirrored chains), Random (two mirrored clusters) and Hub (each gets a private hub).

---

## 💾 Saving & loading settings

| File | What it is | How to create |
|---|---|---|
| **`.oetgs`** (settings) | Full editor state — to continue tuning later | 💾 / 💾… in the header (`Ctrl+S` / `Ctrl+Shift+S`) |
| **`.rmg.json`** (template) | A finished game map template | **"Create template" → "Save"** |

Open saved settings — 📂 (`Ctrl+O`). Reset everything to defaults — 🔄 (`Ctrl+N`).

---

## 📂 Where to save templates

Game templates `.rmg.json` live here:

```
<Olden Era install folder>\HeroesOldenEra_Data\StreamingAssets\map_templates
```

> AuroraRMG tries to **find** this folder via the Steam registry and open the save dialog right there. If the game is installed in a non-standard place, just point to the path manually in the dialog.

After saving, launch Olden Era and pick the template when creating a new game.

---

## 🎮 Running without disturbing the game

To keep the generator handy while playing fullscreen, start it with the **`--minimized`** flag (or `-m`, or `/min`):

```
AuroraRMG.exe --minimized
```

In this mode the window starts **minimized to the taskbar without stealing focus** — it won't pop over a fullscreen game. The update check is **skipped** for this launch.

---

## 💡 Tips & generation notes

- **First "Create template", then "Save".** The button builds the map + preview; "Save" writes the `.rmg.json`. Changing any setting marks the preview outdated and blocks saving — just regenerate.
- **Neutral zones** come from the per-tier sliders (Advanced) or the simple "Extra neutral zones" slider. Without at least one neutral zone the map is players-only.
- **Olden Era has exactly 7 biomes** (Grass, Snow, Lava, Sand, Dirt, Deathland, Autumn). No underground/multi-level maps; symmetry emerges from the chosen topology.
- **Bans & bonuses are global:** banned items/spells/heroes are excluded map-wide; starting bonuses go to all players equally.
- **`[EXP.]` features** (large maps, zone fine-tuning, mandatory content) aren't fully tested and may produce broken maps.

> [!CAUTION]
> The generator only guarantees a valid `.rmg.json`. **In-game playability is not guaranteed** — the project is in active development. Test new templates in a quick game before playing seriously.

---

## ❓ FAQ

**The game doesn't see my template.** Make sure the `.rmg.json` sits exactly in `…\HeroesOldenEra_Data\StreamingAssets\map_templates` and restart the game.

**SmartScreen warns about the `.exe`.** The file isn't certificate-signed — normal for a small tool. "More info" → "Run anyway". You can also verify it (see above).

**"Create template" saves nothing.** First click **"Create template"** (builds the preview), then **"Save"**. Check the validation list — red items block saving.

**The map looks odd / empty / unbalanced.** Likely experimental features are in play (large maps, zone fine-tuning, mandatory content). Try a preset or reset to defaults.

**Do I need a separate .NET?** No. The runtime is bundled in the `.exe`.

---

## 💜 Support the creator

AuroraRMG is developed in spare time and distributed **for free**. If it's useful, you can support development with crypto. Any amount motivates new features and presets. Thanks! 🙌

| ₿ Bitcoin (BTC) — network **Bitcoin** | ₮ Tether (USDT) — network **Tron (TRC20)** |
|---|---|
| `1GFcHvGPchDf6fgAqqtyZrEkFbVcrnWFgQ` | `TEByALUzbYKWCYvyKPiAKrErs8ba6gc4Bo` |

> [!WARNING]
> Send **strictly on the stated network**: BTC only on **Bitcoin**, USDT only on **Tron (TRC20)**. A wrong network or a contract address will lose the funds. Double-check the address before sending.

---

## 🙏 Credits & origin

AuroraRMG is based on **[Olden-Era---Template-Generator](https://github.com/KhanDevelopsGames/Olden-Era---Template-Generator/)** by **[KhanDevelopsGames](https://github.com/KhanDevelopsGames)** — the original idea and approach to programmatically building `.rmg.json` templates. Huge thanks to the original author! 🙌

What was added and reworked in AuroraRMG (as of **1.0.0**):

- 🌑 a new look — the dark neon **Aurora** theme, a new icon/logo and branded menus;
- 🌍 **full bilingual localization (RU/EN)** with an instant header switch;
- 🗺 a **visual zone-graph editor** (canvas, inspector, validation, PNG export);
- 🚫 **hero bans** (full roster with names from the game assets) on top of item and spell bans;
- 🎲 **43 built-in presets** (incl. **PvE 1×2…1×7**) with matching auto-tests, grouped in a menu;
- ⚙️ expanded environment options (terrain, aggression, water, diplomacy, lakes, holes) and zone fine-tuning;
- 🦸 reworked hero-limit input (slider + −/+ buttons + field, max 12);
- 🔄 a built-in **auto-updater** + CI build with **provenance attestation**.

---

## ⚠️ Disclaimer & license

> A tool for quickly generating random map templates with custom settings. Some edge cases may be handled imperfectly — bugs are possible. Features marked `[EXP.]` aren't fully tested and may produce broken or unplayable maps.
>
> This project is **not affiliated** with or endorsed by the developers of Heroes of Might and Magic: Olden Era. Use generated templates at your own risk.

License: [MIT](LICENSE).

<div align="center">

---

**[⬆ Top](#auroramrg)** · [📥 Download the latest version](https://github.com/sany86russ/AuroraRMG/releases/latest)

</div>
