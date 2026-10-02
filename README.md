# DeathTaunt

A **Valheim** mod (BepInEx + HarmonyX) that announces on screen every time a player dies and plays a taunt sound depending on what killed them.

> Player **Hansito** was killed by **Troll**

The message appears in red near the top of the screen for 5 seconds, together with the sound you assigned to that mob.

## Features

- Death message with the name of the player and of the creature that killed them.
- Per-mob sounds: just drop a `.wav` file named after the mob, no code changes or recompiling needed.
- A fallback sound (`default.wav`) for any death that has no sound of its own.
- Environmental deaths with their own message and sound: fire, poison, freezing and drowning.
- Messages in Spanish or English, following the language set in the game.
- A debug tool that lists the internal names of every creature in the game (`mobs.txt`).

## Requirements

- Valheim.
- [BepInEx for Valheim](https://thunderstore.io/c/valheim/p/denikson/BepInExPack_Valheim/) installed.

## Installation

1. Install [BepInEx for Valheim](https://thunderstore.io/c/valheim/p/denikson/BepInExPack_Valheim/) if you have not already, and run the game once so it creates the `BepInEx/plugins` folder.
2. Download the mod and extract it. The package contains a single `DeathTaunt` folder with the plugin and an empty `sounds` folder.
3. Copy the whole `DeathTaunt` folder into `BepInEx/plugins/`, so it looks like this:

```
Valheim/
└── BepInEx/
    └── plugins/
        └── DeathTaunt/
            ├── DeathTaunt.dll
            └── sounds/        <- put your .wav files here
```

4. **The mod ships without any sounds.** Put your own `.wav` files in `DeathTaunt/sounds/` (see [Adding sounds](#adding-sounds)). Until you do, death messages still appear but nothing plays.
5. Start the game. `BepInEx/LogOutput.log` should contain a line with `Loading [DeathTaunt 1.0.0]`.

Once you have added a few sounds, the folder might look like this:

```
DeathTaunt/
├── DeathTaunt.dll
└── sounds/
    ├── default.wav
    ├── Troll.wav
    ├── fire.wav
    └── ...
```

If you prefer to compile it yourself, see [Building from source](#building-from-source).

## Adding sounds

On startup the mod reads every `.wav` file in the `sounds/` folder and uses the **file name** as the key. The format must be **WAV**. If you have an mp3 or another format, convert it first:

```bash
ffmpeg -i taunt.mp3 -c:a pcm_s16le -ar 44100 Troll.wav
```

### Naming rules

- The file name is the mob's **internal (prefab) name**: `Troll.wav`, `Boar.wav`, `Lox.wav`.
- Names are case-insensitive.
- Matching is **by prefix**: `Draugr.wav` also covers `Draugr_Elite` and `Draugr_Ranged`.
- If several files match, the most specific one wins: with both `Skeleton.wav` and `Skeleton_Poison.wav`, the poison skeleton uses the second.
- If nothing matches, `default.wav` plays. If that file does not exist either, the message is shown without sound.

### Special files

These names are not mobs. They are causes of death with no attacker:

| File           | Plays when                           |
| -------------- | ------------------------------------ |
| `default.wav`  | Any death without a sound of its own |
| `fire.wav`     | Death by fire                        |
| `poison.wav`   | Death by poison                      |
| `frost.wav`    | Death by freezing                    |
| `drowning.wav` | Death by drowning                    |

If a mob is the one dealing the damage (for example a poison skeleton), the mob's sound plays instead of the cause's sound.

### Finding a mob's internal name

There are two ways:

1. **`mobs.txt`**: when you enter a world, the mod writes `mobs.txt` into the mod folder, listing every creature in the game, one per line, as `Prefab | token | faction`. The name of the `.wav` is the first column.
2. **The log**: every death is recorded in `BepInEx/LogOutput.log` with the prefab in brackets:

```
DeathTaunt: Player Hansito was killed by Troll [Troll]
```

## Configuration

A config file is created on first launch at `BepInEx/config/hansito.DeathTaunt.cfg`:

| Section | Option        | Default | Description                                                                            |
| ------- | ------------- | ------- | -------------------------------------------------------------------------------------- |
| `Debug` | `DumpMobList` | `true`  | Writes `mobs.txt` when entering a world. Set it to `false` once you no longer need it. |

## Multiplayer

- When a player dies, their client sends the other players the name of the player and of the mob. **Sounds never travel over the network**: each player plays the files from their own `sounds/` folder, so everyone can have different sounds.
- To see and hear everyone's deaths, **every player must have the mod installed**. Players without it can still join and play, but they will not see or hear the announcements, and their own deaths will not be announced.
- The server does not need the mod.

## Building from source

Requirements: the .NET SDK and the Valheim and BepInEx DLLs.

1. Open `DeathTaunt.csproj` and set these two paths to match your installation:

```xml
<ValheimDir>/path/to/Valheim</ValheimDir>
<BepInExDir>/path/to/the/folder/that/contains/BepInEx</BepInExDir>
```

`ValheimDir` must contain `valheim_Data/Managed/`, and `BepInExDir` must contain `BepInEx/core/`. If both live in the same directory, use the same path for both.

2. Build:

```bash
dotnet build -c Release
```

3. The `.dll` ends up in `bin/Release/net462/DeathTaunt.dll`. The project includes a post-build step that copies it to `BepInEx/plugins/DeathTaunt/`.

The project targets `net462`, which is what Valheim (Mono) uses.

## Troubleshooting

| Symptom                                                     | What to check                                                                                                 |
| ----------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------- |
| No `Loading [DeathTaunt ...]` line in the log               | The `.dll` is not in `BepInEx/plugins/`, or BepInEx is not loading. Check the log for errors.                 |
| The message shows but nothing plays                         | `default.wav` or the mob's file is missing. Names must match exactly and files must be `.wav`.                |
| An environmental death shows the generic "has died" message | That hit type is not recognized. Look for the `DeathTaunt hitType: ...` line in the log to see its real name. |
| `mobs.txt` does not appear                                  | You have to enter a world (the main menu is not enough), and `DumpMobList` must be `true`.                    |

## Known limitations

- Only the WAV format is supported.
- The hit type names used to detect environmental deaths may vary between game versions.
- The message is drawn with `OnGUI`, not with the game's own HUD.

## License

This project is licensed under the [MIT License](LICENSE).

The mod ships without any audio. Sounds you add yourself are not covered by this license and remain subject to their own terms.
