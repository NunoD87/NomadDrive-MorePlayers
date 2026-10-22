# Nomad Drive More Players

BepInEx plugin that raises the co-op player limit in **Nomad Drive** (ships with 4).

- Default **6** players, up to **8 recommended**, configurable up to **12** (untested).
- Built from reading the playtest assemblies. Game code is reached by reflection, so no game files are in this repo.
- Status: **v0.x = not yet tested in game.** Reports welcome in Issues.

## Install

1. Install **BepInEx 5.4.x, x64** (Mono) into the game folder: https://github.com/BepInEx/BepInEx/releases
2. Run the game once so BepInEx creates its folders, then close it.
3. Unzip this mod's release into the game folder (it contains `BepInEx/plugins/NomadDriveMorePlayers/`).
4. Start the game, host a lobby. Check `BepInEx/LogOutput.log` for lines like `Lobby MaxPlayers 4 -> 6`.

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
