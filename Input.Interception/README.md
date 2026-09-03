# Input.Interception

`Input.Interception` sends keyboard scan codes through the Interception v1.0.1
keyboard filter driver. It is intended for software that ignores user-mode
`SendInput` events.

## Public API

`InterceptionKeyboardInput` implements the shared `IKeyboardInput` contract:

```csharp
using Input.Abstractions;
using Input.Interception;

IKeyboardInput keyboard = new InterceptionKeyboardInput();
await keyboard.HoldAsync("W", 500);
```

Provider selection belongs to `Input.Abstractions/input.json`; it is not stored
in application macro profiles.

## Driver installation

1. Open an Administrator Command Prompt.
2. Change to the `Input.Interception/tools` directory.
3. Run `install-interception.exe /install`.
4. Reboot Windows.

To uninstall the driver, run `install-interception.exe /uninstall` from the
same elevated prompt and reboot again.

Driver installation changes Windows system state and is deliberately not part
of the application build or startup. Do not install or use input-injection
drivers with games whose rules or anti-cheat systems prohibit them.

The bundled files came from the official Interception v1.0.1 GitHub release.
The user-mode API is covered by the included LGPL 3.0 license for
non-commercial usage. Commercial usage requires the applicable license from
the Interception author.
