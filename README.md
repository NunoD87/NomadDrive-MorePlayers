# Nomad Drive More Players

BepInEx plugin that raises the co-op player limit in **Nomad Drive** (ships with 4).

- Default **6** players, up to **8 recommended**, configurable up to **12** (untested).
- Built from reading the playtest assemblies. Game code is reached by reflection, so no game files are in this repo.
- Status: **v0.x = not yet tested in game.** Reports welcome in Issues.

## Install

**Easy way (recommended):** download `NomadDriveMorePlayers-AllInOne-<version>.zip` from Releases and extract it
into the game folder, next to the game `.exe`. Launch the game from Steam. Done.

It contains BepInEx 5.4.23.5, a set of unstripped Unity core libraries (see below) and this plugin.
No game file is modified.

Check `BepInEx/LogOutput.log` for lines like `Lobby MaxPlayers 4 -> 6`.

**Why the extra files?** The game is Unity 6000.0.73 and its `mscorlib.dll` is stripped (it lacks
`Module.GetPEKind`), so BepInEx crashes on start with no log. Both BepInEx 5 and 6 need that method, so a different
BepInEx version does not help. The zip ships the matching unstripped libraries in `BepInEx/unstripped_corlibs` and
`doorstop_config.ini` points Mono at them.

**Manual way:** if you already have a working BepInEx 5, use the plain `NomadDriveMorePlayers-<version>.zip`.
It only contains the plugin.

## Config

`BepInEx/config/com.nunod87.nomaddrive.moreplayers.cfg` (created on first run)

| Key | Default | Meaning |
|---|---|---|
| `Enabled` | true | Switch the mod off without uninstalling |
| `MaxPlayers` | 6 | Lobby size, host included. 2 to 12 |
| `VerboseLog` | false | Log every patched call |

## Who needs it

The lobby size and the connection cap are both decided by the **host**, so in theory only the host needs the mod.
This is **not yet confirmed**. If a client without the mod has problems (UI, seats), install it on everyone.

## Known limits

- The game's own loot/economy scaling is clamped to 8 players. Above 8 it stays at the 8-player level.
- The RV has a fixed number of seat slots. Extra players may have no seat. Under investigation.
- Player spawn points and UI lists may assume 4 players.
- Playtest and release builds have different Steam app IDs and may differ in code. A game update can break the patches. The plugin logs an error if it cannot find the expected types.

## Build

```
dotnet build src/NomadDriveMorePlayers/NomadDriveMorePlayers.csproj -c Release
```

CI builds on every push. Pushing a tag like `v0.1.0` publishes a GitHub Release with the zip.

Not affiliated with Indie Devil. Use at your own risk; check the playtest terms before modding.
