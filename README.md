# EricLauncher

A very bare-bones launcher for Epic Games Store games **that have already been installed via the official launcher**. Designed for Windows and written in C# using .NET 7.0.

This application was written for personal usage and isn't 100% user oriented. If you're looking for something more stable, tested, reliable, featureful and/or cross-platform, check out [Legendary (CLI)](https://github.com/derrod/legendary) or [Heroic Games Launcher (GUI)](https://github.com/Heroic-Games-Launcher/HeroicGamesLauncher).

**This is provided without any warranty, I am not responsible for your account getting banned, your hard drive exploding, getting sniped at 2nd in a Battle Royale, or thermonuclear war.**

## Features

- Logging in to Epic Games accounts.
- Multi-account support. (specify an `--accountId` at the command line.)
- Checking for Fortnite updates. (if an update is available, the game will not launch.)
- Support for at least some games. (tested with Fortnite, Borderlands 3, Death Stranding and FUSER.)
    - Not every game will work right now.

## Usage

This is designed to be run from the command line, but you can drag and drop a game's executable onto it and it should work. (Alternatively you could make a batch file or shortcut.)

```
Usage: EricLauncher.exe [executable path] (options)

Options:
    --accountId [accountId] - use a specific Epic Games account to sign in.
                              omitting this option will use the default account
    --noManifest - don't check the local Epic Games Launcher install folder for the manifest.
                   this WILL break certain games from launching, e.g. Fortnite
    --stayOpen - keeps EricLauncher open in the background until the game is closed
                 useful for launching through other launchers, e.g. Steam
```

The account ID parameter is only required if you are using multiple accounts. Omitting this value will use (or save) a default account.

For best results, make sure the game has been launched at least once by the official Epic Games Launcher, and the provided executable path is the same one that gets launched by the official launcher.

Session files are stored in `%localappdata%\EricLauncher`.

## Known Issues

- Using the "accountId" parameter with the same account ID as the default account will break the default account until re-login.

## TODO

- Only refresh tokens when access token expires.
- Fetching manifest online when one can't be found in the local cache.
- Fetch ownership tokens (`-epicovt`) for the games that require them.
- Offline game launching.
- Support for macOS and Linux.
- Allowing specifying an auth type at the command line.
