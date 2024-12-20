# EricLauncher

A very bare-bones launcher for Epic Games Store games **that have already been
installed via the official launcher**. Designed for Windows and written in C#
using .NET 8.0.

This application was written for personal usage and isn't 100% user oriented.
If you're looking for something more stable, tested, reliable, featureful
and/or cross-platform, check out [Legendary (CLI)](https://github.com/derrod/legendary)
or [Heroic Games Launcher (GUI)](https://github.com/Heroic-Games-Launcher/HeroicGamesLauncher).

**This does not handle cloud save games, you may lose save data!**

**This is provided without any warranty, I am not responsible for your account
getting banned, your save data being lost, your hard drive exploding, getting
sniped at 2nd in a Battle Royale, or thermonuclear war.**

## Features

- Logging in to Epic Games accounts.
- Multi-account support. (specify an `--accountId` or `--account` at the command
  line.)
- Checking for Fortnite updates. (if an update is available, the game will not
  launch.)
- Windows and macOS support, as well as providing launch args on Linux.
- Support for at least some games. (tested with Fortnite, Borderlands 3, Death
  Stranding and FUSER.)
    - Including games that require ownership tokens. (in theory)
- Offline game launching. (only works on some games)

## Usage

This is designed to be run from the command line, but you can drag and drop a
game's primary executable onto it and it should work.

**For Fortnite, the game's primary executable is always FortniteLauncher.exe**

Alternatively you could make a batch file or shortcut, or create a shortcut in
a launcher such as Steam - pointing to EricLauncher, with the game you want to
launch as the launch arguments

### Steam Usage

Add EricLauncher.exe as a non-Steam shortcut, then set the launch arguments on
the shortcut to be the path to your game's executable.

![Steam Properties window for a game named 'Fortnite', which shows the 'Target' as being 'EricLauncher.exe' and 'Launch Options' as being 'FortniteLauncher.exe'](readme_assets/steam_example.png)

### Command Line Usage

```
Usage: EricLauncher.exe [game executable path or verb] (options) (game arguments)

Options:
  --accountId [id]     - use a specific Epic Games account ID to sign in.
  --account [username] - use a specific Epic Games account username to sign in.
  --setDefault         - when used with either account/accountId, sets the default account.
  --heroic             - loads the game's manifest from Heroic Games Launcher instead of Epic.
  --manifest [path]    - loads an Epic Games Launcher manifest file from the specified path.
  --noManifest         - don't check the local Epic Games Launcher install folder for the manifest.
  --stayOpen           - keeps EricLauncher open in the background until the game is closed.
  --dryRun             - goes through the Epic Games login flow, but does not launch the game.
  --offline            - skips the Epic Games login flow, to launch the game in offline mode.

Verbs:
  logout    - Logs out of Epic Games.
```

The account ID parameter is only required if you are using multiple accounts.
Omitting this value will use (or save) a default account.

For best results, make sure the game has been launched at least once by the
official Epic Games Launcher, and the provided executable path is the same one
that gets launched by the official launcher.

(Heroic support is preliminary but should work on Windows, use `--heroic`)

Epic Games session files are stored in `%localappdata%\EricLauncher`. You can
log out of EricLauncher by running `EricLauncher.exe logout`.

## TODO

- Fetching manifest online when one can't be found in the local cache.
  - and also from Heroic/Legendary launchers cache, on macOS and Linux.
- Support for launching games under Proton on Linux.
  - ...and Crossover/GPTK on macOS.
