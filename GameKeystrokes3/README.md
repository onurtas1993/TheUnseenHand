# GameKeystrokes3

`GameKeystrokes3` sends keyboard scan codes through the Interception v1.0.1
keyboard filter driver. It is intended for software that ignores user-mode
`SendInput` input.

## Driver installation

1. Open an Administrator Command Prompt.
2. Change to the `GameKeystrokes3/tools` directory.
3. Run `install-interception.exe /install`.
4. Reboot Windows.

Driver installation changes Windows system state and is deliberately not part
of the application build or startup. Do not install or use input-injection
drivers with games whose rules or anti-cheat systems prohibit them.

The bundled files came from the official Interception v1.0.1 GitHub release.
The user-mode API is covered by the included LGPL 3.0 license for
non-commercial usage. Commercial usage requires the applicable license from
the Interception author.
