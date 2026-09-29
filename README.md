# Portal Sound Control

Mute Valheim's portal sounds or set their volume. Client-side only.

## Requirements

- Valheim with [BepInExPack_Valheim](https://thunderstore.io/c/valheim/p/denikson/BepInExPack_Valheim/)

## Install

Install through your mod manager, or copy `PortalSoundControl.dll` into `BepInEx/plugins/PortalSoundControl/`.

## Config

`BepInEx/config/local.portalsoundcontrol.cfg` (created on first launch). Changes made with a configuration manager apply while the game is running.

| Setting | Default | What it does |
|---|---|---|
| Mute | false | Mute portal sounds completely |
| Volume | 1.0 | Portal volume from 0 (silent) to 1 (normal) |
| AffectAmbientHum | true | Apply to sounds portals make while standing (hum, loops) |
| AffectTeleportEffects | true | Apply to teleport and activation sound effects |

## Building from source

1. Install the .NET SDK and BepInEx.
2. Double-click `build.bat`. It finds Valheim and BepInEx (game folder, r2modman or Thunderstore profile), builds, and copies the DLL into `BepInEx/plugins/PortalSoundControl/`.

## Troubleshooting

Search `BepInEx/LogOutput.log` for `Portal Sound Control`. It lists the portal audio sources and teleport effect prefabs it found. If a portal sound is still audible, send those lines.
