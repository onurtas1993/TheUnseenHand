<table>
  <tr>
    <td>

<img src="./TheUnseenHand/Assets/icon.ico" width="128"/>
    </td>
    <td>

# The Unseen Hand

Build AI-powered game macros that see game state, make intelligent decisions, and keep your character playing autonomously.

[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![Platform](https://img.shields.io/badge/platform-Windows-0078D4?logo=windows)](https://www.microsoft.com/windows)
[![License](https://img.shields.io/github/license/onurtas1993/TheUnseenHand)](LICENSE)
    </td>
  </tr>
</table>

<table>
  <tr> 
    <td>
<img src="https://raw.githubusercontent.com/onurtas1993/images/refs/heads/main/unseen_hand_demo.gif"/>
    </td>
  </tr>
    <tr> 
    <td>
<img src="https://raw.githubusercontent.com/onurtas1993/images/refs/heads/main/unseen_hand_demo2.gif"/>
    </td>
  </tr>
</table>

## Screenshot of the latest version of the application

<table> 
    <tr> 
    <td>
<img src="https://raw.githubusercontent.com/onurtas1993/images/refs/heads/main/unseen_hand_ss.jpg"/>
    </td>
    <td>
<img src="https://raw.githubusercontent.com/onurtas1993/images/refs/heads/main/unseen_hand_ss2.jpg"/>
    </td>
  </tr> 
</table>

The Unseen Hand is a Windows game macro creator for building self-playing character routines. It goes beyond a fixed key repeater: the macro can read information such as HP, MP, stats, or the current target from the game screen, then choose what the character should do through nested `THEN` and `ELSE` action sequences.

Create rotations, recovery rules, target-dependent attacks, movement fallbacks, and other gameplay loops from the GUI. The application only advances the macro while the configured game is in the foreground, helping keep keyboard input scoped to the intended window.

## Highlights

- Gamer-friendly visual editor for creating, editing, reordering, and removing macro actions
- `Press`, `Wait`, and nested `If / Then / Else` actions
- Configurable key-hold durations and condition-check intervals
- GUI-assisted screen-region coordinate calculation
- Screen-region capture relative to the game client area
- Typed vision outputs: text, integer, decimal, and boolean
- Optional validation constraints for recognized numeric values
- Live display of values read by `Vision.GameCapture`
- JSON import/export for reusable macro profiles
- Local inference through an OpenAI-compatible LM Studio endpoint
- Focus-aware execution that pauses when the target application loses focus

## How it works

```text
WPF macro editor
      |
      +-- Press / Wait --> Input.Abstractions --> selected input provider --> target window
      |                                      |--> Input.Native (SendInput)
      |                                      +--> Input.Interception (driver)
      |
      +-- If condition --> Vision.GameCapture --> Vision.Inference --> LM Studio
                              |
                              +-- typed value --> THEN / ELSE branch
```

Each macro runs as a continuous gameplay loop. Before an action is executed, the application verifies that the configured game is foreground. Vision-based conditions capture only their selected screen regions, ask the local model for structured values, validate the response, and execute the matching branch. This allows the character to respond to changing game state instead of blindly replaying a fixed sequence.

## Requirements

- Windows 10 version 2004 or newer
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Unseen Hand must run as administrator when the target app is also running as administrator.
- The `Interception` input provider additionally requires its keyboard filter driver to be installed.
- For vision conditions:
  - [LM Studio](https://lmstudio.ai/) with its local server enabled
  - `ggml-org/GLM-OCR-GGUF` vision model needs to be installed
  - the model is 1.4GB and fits to most gaming laptops

## Getting started

Clone the repository and build the solution by opening it in Visual Studio 2022.

On startup, the app loads its default macro profile. Build and maintain profiles with the application's **Add Action**, **Edit Action**, **Load**, and **Save** controls.

## Create a game macro

The GUI is the normal way to configure The Unseen Hand. Add key presses and waits, create vision-based conditions, arrange nested branches, and use the coordinate tools to select the game areas that should be read. Save the finished setup as a reusable profile, then press **Start** to let the character run the gameplay loop.

The project stores its configuration in four JSON files behind the scenes:

| File | Purpose |
| --- | --- |
| [`TheUnseenHand/macro-settings.json`](TheUnseenHand/macro-settings.json) | Target process and ordered macro actions |
| [`Input.Abstractions/input.json`](Input.Abstractions/input.json) | Keyboard input provider selection |
| [`Vision.GameCapture/gamevision.json`](Vision.GameCapture/gamevision.json) | Capture regions, prompts, output types, and validation rules |
| [`Vision.Inference/localai.json`](Vision.Inference/localai.json) | Local model endpoint, model name, timeout, and inference settings |

### Keyboard input provider

Keyboard-provider selection is infrastructure configuration and is intentionally separate from saved macro profiles. Configure it only in [`Input.Abstractions/input.json`](Input.Abstractions/input.json):

```json
{
  "schemaVersion": 1,
  "keyboardProvider": "Interception"
}
```

Supported values are:

- `Windows` uses `Input.Native` and the Windows `SendInput` API. It requires no additional installation. Try this first, if the target application does not receive inputs, then some anti-tampering software might be blocking he input simulations sent from Windows and you have to proceed with the below way.
- `Interception` uses `Input.Interception` and its driver-backed scan-code injection. Install the driver from an Administrator Command Prompt and reboot before using it:

```bat
cd /d D:\repo\TheUnseenHand\Input.Interception\tools
install-interception.exe /install
```

To remove the driver, run `install-interception.exe /uninstall` from the same elevated prompt and reboot. The input-provider choice is global for the application; loading or saving `macro-settings.json` does not read, write, or change it.

> [!IMPORTANT]
> The JSON shown below is a presentation of what the GUI saves and a reference for developers and contributors. Players should use the GUI to create actions, calculate capture coordinates, configure vision readers, and save profiles instead of editing these files by hand.

### Saved macro format

The macro editor saves only the target game and its nested action tree in `macro-settings.json`. Keyboard-provider selection is not part of a saved macro. A saved profile looks like this:

```json
{
  "schemaVersion": 5,
  "target": {
    "processName": "your-game.exe"
  },
  "macro": {
    "actions": [
      {
        "type": "If",
        "condition": {
          "source": "PlayerHP",
          "operator": "LessThan",
          "value": "2000"
        },
        "actions": [
          { "type": "Press", "value": "1", "durationMilliseconds": 75 }
        ],
        "elseActions": []
      },
      { "type": "Wait", "value": "500" }
    ]
  }
}
```

Supported comparison operators are `Equals`, `NotEquals`, `LessThan`, `LessThanOrEqual`, `GreaterThan`, and `GreaterThanOrEqual`. Text and boolean outputs support equality comparisons; numeric outputs support all operators.

### Example saved Vision.GameCapture format

The `Vision.RegionEditor` application calculates capture regions for the parts of the game HUD the vision model needs to read. Those selections are stored in [`Vision.GameCapture/gamevision.json`](Vision.GameCapture/gamevision.json). The current example targets a popular MMORPG. Coordinates are initially supplied by the user, then adjusted in the editor until the intended area is captured precisely.

<img src="https://raw.githubusercontent.com/onurtas1993/images/refs/heads/main/gamevision_ss.png" width="567"/>

```json
{
  "ExecutableName": "<GAME_EXECUTABLE>.exe",
  "Readers": {
    "PlayerVitals": {
      "Region": { "Anchor": "TopLeft", "X": 80, "Y": 37, "Width": 90, "Height": 27 },
      "Prompt": [
        "Read the four numeric player-vitals values visible in this image.",
        "The top row is current HP / maximum HP and the bottom row is current MP / maximum MP.",
        "Read the printed digits, not the bar length."
      ],
      "Outputs": {
        "PlayerHP": { "Type": "Integer" },
        "PlayerMaxHP": { "Type": "Integer" },
        "PlayerMP": { "Type": "Integer" },
        "PlayerMaxMP": { "Type": "Integer" }
      }
    },
    "CurrentMob": {
      "Region": { "Anchor": "TopCenter", "X": -100, "Y": 11, "Width": 200, "Height": 18 },
      "Prompt": [
        "Read the exact mob name visible in this image.",
        "Preserve spaces."
      ],
      "Outputs": {
        "CurrentMob": { "Type": "Text" }
      }
    },
    "PlayerInfo": {
      "Region": { "Anchor": "TopLeft", "X": 38, "Y": 336, "Width": 120, "Height": 40 },
      "Prompt": [
        "The top row is Attack; read the number directly beside the visible Attack label.",
        "The bottom row is Defence; read the number directly beside the visible Defence label.",
        "If a label or its adjacent number is not visible, mark that output unreadable."
      ],
      "Outputs": {
        "Attack": { "Type": "Integer", "Minimum": 100, "Maximum": 9999 },
        "Defence": { "Type": "Integer", "Minimum": 100, "Maximum": 9999 }
      }
    }
  }
}
```

Behind the GUI, `Anchor` determines the reference point for `X` and `Y`; the current configuration uses `TopLeft` and `TopCenter`. Coordinates are relative to the captured game client area and are not automatically scaled. Output names must be unique across all readers.

Supported output types are `Text`, `Integer`, `Decimal`, and `Boolean`. Numeric outputs can specify `Minimum` and `Maximum`; integer outputs can also use `MinimumDigits` and `MaximumDigits`. Recognition results that are unreadable, incorrectly typed, or outside these constraints are discarded rather than passed to a macro condition.

Use the coordinate-calculation GUI to create or adjust regions rather than calculating these values manually. The included `Vision.RegionEditor` can then preview a configured reader and show its recognition result independently from macro execution. Recalculate the regions if the game's resolution, window layout, or UI scale changes.

### Saved local-model format

The default adapter expects an OpenAI-compatible server at `http://localhost:1234/v1`:

```json
{
  "BaseUrl": "http://localhost:1234/v1",
  "Model": "glm-ocr",
  "ApiKey": null,
  "MaxTokens": 1024,
  "Temperature": 0.0,
  "TimeoutSeconds": 60
}
```

Select the model exposed by your local server during setup. No model files, GPU runtime, or inference engine are bundled with this repository.

## Solution structure

| Project | Responsibility |
| --- | --- |
| `TheUnseenHand` | Gamer-facing WPF macro creator, profiles, and gameplay-loop execution |
| `Input.Abstractions` | Shared `IKeyboardInput` contract and the independent `input.json` provider configuration |
| `Input.Native` | Windows target-window discovery and `SendInput`-based keyboard input |
| `Input.Interception` | Interception driver-backed scan-code input and driver installation assets |
| `Vision.GameCapture` | Foreground-window capture, region handling, and typed recognition |
| `Vision.Inference` | Image and prompt requests to an OpenAI-compatible local endpoint |
| `Vision.RegionEditor` | Capture-region editing, preview, and recognition diagnostics |


## Safety and responsible use

Automation can violate the rules of games and other applications. Use this project only where automation is permitted, keep a way to stop execution readily available, and review profiles before running them. You are responsible for complying with the target application's terms and applicable policies.

## Contributing

Issues and pull requests are welcome. When proposing a change, describe the use case, keep platform-specific behavior explicit, and verify the solution with:

```powershell
dotnet build TheUnseenHand.sln -c Release -p:Platform=x64
```

## License

Distributed under the [GPL-3.0 license](LICENSE).
