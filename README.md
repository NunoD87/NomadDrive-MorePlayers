<a id="readme-top"></a>

[![Contributors][contributors-shield]][contributors-url]
[![Forks][forks-shield]][forks-url]
[![Stargazers][stars-shield]][stars-url]
[![Issues][issues-shield]][issues-url]
[![MIT License][license-shield]][license-url]

<br />
<div align="center">
  <h3 align="center">Nomad Drive More Players</h3>

  <p align="center">
    A BepInEx plugin that raises the co-op player limit in Nomad Drive. Pick the player count in game, above the usual 4.
    <br />
    <a href="https://github.com/NunoD87/NomadDrive-MorePlayers#getting-started"><strong>Install guide »</strong></a>
    <br />
    <br />
    <a href="https://github.com/NunoD87/NomadDrive-MorePlayers/releases">Download</a>
    &middot;
    <a href="https://github.com/NunoD87/NomadDrive-MorePlayers/issues/new?labels=bug">Report Bug</a>
    &middot;
    <a href="https://github.com/NunoD87/NomadDrive-MorePlayers/issues/new?labels=enhancement">Request Feature</a>
  </p>
</div>

<details>
  <summary>Table of Contents</summary>
  <ol>
    <li>
      <a href="#about-the-project">About The Project</a>
      <ul>
        <li><a href="#built-with">Built With</a></li>
      </ul>
    </li>
    <li>
      <a href="#getting-started">Getting Started</a>
      <ul>
        <li><a href="#prerequisites">Prerequisites</a></li>
        <li><a href="#installation">Installation</a></li>
      </ul>
    </li>
    <li><a href="#usage">Usage</a></li>
    <li><a href="#known-limits">Known Limits</a></li>
    <li><a href="#roadmap">Roadmap</a></li>
    <li><a href="#contributing">Contributing</a></li>
    <li><a href="#license">License</a></li>
    <li><a href="#contact">Contact</a></li>
    <li><a href="#acknowledgments">Acknowledgments</a></li>
  </ol>
</details>

## About The Project

Nomad Drive is a co-op survival game for 1 to 4 players. This mod lifts that cap:

* The **Create Game** player slider goes above 4 (starts at 6, top 8 by default, up to 12 in the config file).
* The lobby size follows what the host picks, so friends can really join the 5th, 6th, ... seat.
* The host chooses the number in game. The config file only holds the slider limits.

How it was found: the playtest assemblies show the limit in four places, and the mod patches all of them.

1. The slider range and its second clamp in `EvilCore.UI` (`CreateGamePanel`).
2. `LobbyCreateOptions.MaxPlayers` in `EvilCore.Networking`.
3. The Epic Online Services call that creates the lobby (`MaxLobbyMembers`).
4. Mirror's `maxConnections`, which makes the host refuse joins when full.

The game's types are reached through reflection, so this repository contains no game files.

> **Status: v0.x. Tested on the Nomad Drive Early Access Playtest (Unity 6000.0.73) with up to 4 players. Larger lobbies, seats and spawns are still being tested.**

<p align="right">(<a href="#readme-top">back to top</a>)</p>

### Built With

* [![BepInEx][BepInEx-badge]][BepInEx-url]
* [![Harmony][Harmony-badge]][Harmony-url]
* [![C#][CSharp-badge]][CSharp-url]
* [![GitHub Actions][Actions-badge]][Actions-url]

<p align="right">(<a href="#readme-top">back to top</a>)</p>

## Getting Started

### Prerequisites

* Nomad Drive (the playtest build the mod was written against).
* **BepInExPack_NomadDrive** from Nexus Mods: <https://www.nexusmods.com/nomaddrive/mods/7>

  It is BepInEx 5.4.23.5 set up for this game: it ships `winhttp.dll`, `doorstop_config.ini`, `BepInEx/core` and an
  `unstripped_libs` folder, and points the game at those libraries. This is needed because the game's own
  `mscorlib.dll` is stripped (no `Module.GetPEKind`), which makes plain BepInEx crash at start with only a
  `preloader_*.log`. No game file is modified.

### Installation

1. Extract **BepInExPack_NomadDrive** into the game folder, next to the game `.exe`
   (Steam: right-click the game, Manage, Browse local files). `winhttp.dll` must sit beside the `.exe`.
2. Start the game once from Steam, wait for the main menu, then quit. BepInEx creates its `plugins` and `config` folders.
3. Download `NomadDriveMorePlayers-<version>.zip` from [Releases][releases-url] and extract it into the same game folder.
   You should end up with `BepInEx/plugins/NomadDriveMorePlayers/NomadDriveMorePlayers.dll`.
4. Start the game from Steam.

<details>
<summary>Without the pack (manual)</summary>

Install **BepInEx 5.4.23.5 x64** from <https://github.com/BepInEx/BepInEx/releases/tag/v5.4.23.5> next to the game `.exe`,
then either copy the matching Unity core libraries from `https://unity.bepinex.dev/corlibs/<unity version>.zip`
(6000.0.73 at the time of writing) over the ones in `<Game>_Data/Managed/` (back them up first), or place them in a
folder and set `dll_search_path_override` in `doorstop_config.ini` to that folder, which is what the pack does.

</details>

<p align="right">(<a href="#readme-top">back to top</a>)</p>

## Usage

1. Main menu, **Multiplayer**, **Create Game**.
2. Drag the **Max players** slider. It now goes up to your configured maximum.
3. Start the game and share the join code or let friends use the lobby list.

Check the BepInEx log (`BepInEx/LogOutput.log`, or `output_log.txt` next to the `.exe` when the pack redirects it) for:

```
Create Game slider range 1..8, starts at 6.
Party size from slider: 4 -> 6
EOS lobby MaxLobbyMembers requested: 6
Mirror maxConnections 4 -> 8
```

**Config** (`BepInEx/config/com.nunod87.nomaddrive.moreplayers.cfg`, created on first run):

| Key | Default | Meaning |
|---|---|---|
| `Enabled` | `true` | Switch the mod off without uninstalling |
| `SliderMaxPlayers` | `8` | Top of the slider, host included. 4 to 12. Above 8 is untested |
| `DefaultPlayers` | `6` | Where the slider starts, and the lobby size when a lobby is made without the slider (for example turning a single-player world into multiplayer) |
| `VerboseLog` | `false` | Log every patched call |

**Who needs the mod?** The slider, the lobby size and the connection cap are all decided by the host, so in theory only the host.
This is **not confirmed yet**. If a friend without the mod has problems (UI, seats), install it on their PC too.

<p align="right">(<a href="#readme-top">back to top</a>)</p>

## Known Limits

- The game's loot and economy scaling is clamped to 8 players. Above 8 it stays at the 8-player level.
- The RV has a fixed number of seat slots. Extra players may have no seat. Under investigation.
- Player spawn points and the player list UI may assume 4 players.
- Playtest and release builds can differ. A game update can break the patches. The plugin logs an error if it cannot find the expected types.

<p align="right">(<a href="#readme-top">back to top</a>)</p>

## Roadmap

- [x] Raise the lobby size (EOS) and the connection cap (Mirror)
- [x] Raise the Create Game player slider
- [ ] Confirm that a client without the mod can join a 5+ player lobby
- [ ] Seats for players 5 and up
- [ ] Spawn points and player list for more than 4 players
- [ ] Re-check against the 27 Oct 2026 release build
- [ ] v1.0.0 stable release

See the [open issues](https://github.com/NunoD87/NomadDrive-MorePlayers/issues) for a full list of proposed features (and known issues).

<p align="right">(<a href="#readme-top">back to top</a>)</p>

## Contributing

Contributions are what make the open source community such an amazing place to learn, inspire, and create. Any contributions you make are **greatly appreciated**.

If you have a suggestion that would make this better, please fork the repo and create a pull request. You can also simply open an issue with the tag "enhancement".

1. Fork the Project
2. Create your Feature Branch (`git checkout -b feature/AmazingFeature`)
3. Commit your Changes (`git commit -m 'feat: add some AmazingFeature'`)
4. Push to the Branch (`git push origin feature/AmazingFeature`)
5. Open a Pull Request

Build locally with `dotnet build src/NomadDriveMorePlayers/NomadDriveMorePlayers.csproj -c Release`.
CI builds every push, and pushing a tag like `v0.2.1` publishes a GitHub Release with the zip.

<p align="right">(<a href="#readme-top">back to top</a>)</p>

## License

Distributed under the MIT License. See `LICENSE` for more information.

Not affiliated with Indie Devil. Use at your own risk, and check the playtest terms before modding.

<p align="right">(<a href="#readme-top">back to top</a>)</p>

## Contact

Nuno Daniel - [@NunoD87](https://github.com/NunoD87)

Project Link: [https://github.com/NunoD87/NomadDrive-MorePlayers](https://github.com/NunoD87/NomadDrive-MorePlayers)

<p align="right">(<a href="#readme-top">back to top</a>)</p>

## Acknowledgments

* [BepInEx](https://github.com/BepInEx/BepInEx) and [HarmonyX](https://github.com/BepInEx/HarmonyX)
* [BepInExPack_NomadDrive](https://www.nexusmods.com/nomaddrive/mods/7) for the game-ready BepInEx setup
* [UnityDataMiner corlibs](https://unity.bepinex.dev/corlibs/) for the unstripped Unity libraries
* [Best-README-Template](https://github.com/othneildrew/Best-README-Template)
* Indie Devil, for making Nomad Drive

<p align="right">(<a href="#readme-top">back to top</a>)</p>

<!-- MARKDOWN LINKS & IMAGES -->
[contributors-shield]: https://img.shields.io/github/contributors/NunoD87/NomadDrive-MorePlayers.svg?style=for-the-badge
[contributors-url]: https://github.com/NunoD87/NomadDrive-MorePlayers/graphs/contributors
[forks-shield]: https://img.shields.io/github/forks/NunoD87/NomadDrive-MorePlayers.svg?style=for-the-badge
[forks-url]: https://github.com/NunoD87/NomadDrive-MorePlayers/network/members
[stars-shield]: https://img.shields.io/github/stars/NunoD87/NomadDrive-MorePlayers.svg?style=for-the-badge
[stars-url]: https://github.com/NunoD87/NomadDrive-MorePlayers/stargazers
[issues-shield]: https://img.shields.io/github/issues/NunoD87/NomadDrive-MorePlayers.svg?style=for-the-badge
[issues-url]: https://github.com/NunoD87/NomadDrive-MorePlayers/issues
[license-shield]: https://img.shields.io/github/license/NunoD87/NomadDrive-MorePlayers.svg?style=for-the-badge
[license-url]: https://github.com/NunoD87/NomadDrive-MorePlayers/blob/main/LICENSE
[releases-url]: https://github.com/NunoD87/NomadDrive-MorePlayers/releases
[BepInEx-badge]: https://img.shields.io/badge/BepInEx-5.4.23.5-1f6feb?style=for-the-badge
[BepInEx-url]: https://github.com/BepInEx/BepInEx
[Harmony-badge]: https://img.shields.io/badge/HarmonyX-patches-7b2cbf?style=for-the-badge
[Harmony-url]: https://github.com/BepInEx/HarmonyX
[CSharp-badge]: https://img.shields.io/badge/C%23-net472-239120?style=for-the-badge&logo=csharp&logoColor=white
[CSharp-url]: https://learn.microsoft.com/dotnet/csharp/
[Actions-badge]: https://img.shields.io/badge/GitHub_Actions-CI-2088FF?style=for-the-badge&logo=githubactions&logoColor=white
[Actions-url]: https://github.com/NunoD87/NomadDrive-MorePlayers/actions
