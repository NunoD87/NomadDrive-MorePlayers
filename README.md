# Nomad Drive More Players

BepInEx plugin that raises the co-op player limit in **Nomad Drive** (ships with 4).

- Pick the player count **in game**: the Create Game player slider now goes above 4 (default start 6, top 8 recommended, up to 12 via config, untested).
- Built from reading the playtest assemblies. Game code is reached by reflection, so no game files are in this repo.
- Status: **v0.x = not yet tested in game.** Reports welcome in Issues.

## Install

1. Install **BepInExPack_NomadDrive** from Nexus Mods: https://www.nexusmods.com/nomaddrive/mods/7
   It is a BepInEx pack made for Nomad Drive, so it takes care of the game-specific setup (the game's stripped
   `mscorlib.dll` makes plain BepInEx crash at start). Follow the install steps on its page, then run the game once
   from Steam so BepInEx creates its folders.
2. Download `NomadDriveMorePlayers-<version>.zip` from [Releases](../../releases) and unzip it into the game folder
   (next to the game `.exe`). It contains `BepInEx/plugins/NomadDriveMorePlayers/NomadDriveMorePlayers.dll`.
3. Start the game from Steam, open **Create Game** (multiplayer): the player slider now goes above 4.

Check `BepInEx/LogOutput.log` for `Create Game slider range 1..8` and `Mirror maxConnections 4 -> 8`.

<details>
<summary>Doing it by hand instead of the pack</summary>

You need **BepInEx 5.4.23.5 x64** (https://github.com/BepInEx/BepInEx/releases/tag/v5.4.23.5) extracted next to the
game `.exe`, plus the unstripped Unity core libraries for the game's Unity version (6000.0.73 at the time of writing):
extract https://unity.bepinex.dev/corlibs/6000.0.73.zip into `<Game>_Data/Managed/`, overwriting (back up the originals).
Without them BepInEx dies with `MissingMethodException: Module.GetPEKind` in a `preloader_*.log`.
If the game updates its Unity version, use the matching `https://unity.bepinex.dev/corlibs/<version>.zip`.
</details>

## Config

Players are chosen in game with the slider on the **Create Game** screen. The config file only holds the slider limits.

`BepInEx/config/com.nunod87.nomaddrive.moreplayers.cfg` (created on first run)

| Key | Default | Meaning |
|---|---|---|
| `Enabled` | true | Switch the mod off without uninstalling |
| `SliderMaxPlayers` | 8 | Top of the slider, host included. 4 to 12. Above 8 is untested |
| `DefaultPlayers` | 6 | Where the slider starts, and the lobby size when a lobby is made without the slider (e.g. turning a single-player world into multiplayer) |
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
