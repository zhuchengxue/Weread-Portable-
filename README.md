# WeRead Side Reader

Portable Windows WebView2 side reader for WeRead.

## Run

Double-click:

```text
Run-WereadReader-Hidden.vbs
```

This launcher starts the app without showing a console window.

## Features

- Opens the real WeRead website in a slim WebView2 window.
- Portable folder layout: app files, profile, settings, icon, and dependencies stay in this directory.
- Saves window size, position, zoom, Auto state, and last WeRead URL to `settings.json`.
- Uses `profile/` as the WebView2 user data folder, so login state stays local to this tool.
- Global hotkey: `Ctrl+Shift+Y` shows or hides the window.
- `A-` / `A+` changes zoom and remembers it.
- `Auto` enables edge auto-hide for left, right, and top edges.
- `Full` switches to borderless in-window reading mode without changing the original window size.
- `F11` toggles Full. `Esc` exits Full first, otherwise hides the window.
- Tray icon supports show/hide, home, and exit.

## Reset

Delete `settings.json` to reset window size, zoom, Auto state, and last URL.

Delete `profile/` to clear WebView2 login/cache data.

## Dependencies

The WebView2 WinForms package is cached in `packages/`. The machine still needs Microsoft Edge WebView2 Runtime, which is already present on most Windows systems.

If dependencies are missing, run:

```powershell
.\Install-Dependencies.ps1
```
