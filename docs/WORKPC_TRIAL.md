# TopBar Work-PC Trial

Build44B defines a portable package workflow for a personal Windows 11 work-PC trial. It does not add an installer, startup registration, code signing, or runtime behavior changes.

## Recommended Package

Use the self-contained `win-x64` portable folder. This avoids installing the .NET Desktop Runtime on the work PC and keeps rollback simple.

```powershell
.\scripts\publish-workpc.ps1
```

Default output:

```text
artifacts\publish\workpc\TopBarPoC-workpc-win-x64\
artifacts\publish\workpc\TopBarPoC-workpc-win-x64.zip
```

To publish without creating a zip:

```powershell
.\scripts\publish-workpc.ps1 -NoZip
```

## Publish Command Reference

Framework-dependent Release folder. Use only when the target PC already has the .NET 8 Desktop Runtime or IT can install it.

```powershell
dotnet publish .\TopBarPoC.csproj --configuration Release --self-contained false --output .\artifacts\publish\framework-dependent
```

Self-contained `win-x64` portable folder. This is preferred for the work-PC trial.

```powershell
dotnet publish .\TopBarPoC.csproj --configuration Release --runtime win-x64 --self-contained true -p:PublishSingleFile=false --output .\artifacts\publish\workpc\TopBarPoC-workpc-win-x64
```

Single-file `win-x64`. This is documented for completeness but is not preferred for the work-PC trial because unsigned single-file desktop executables can draw more scrutiny from SmartScreen, antivirus, EDR, AppLocker, or WDAC policy.

```powershell
dotnet publish .\TopBarPoC.csproj --configuration Release --runtime win-x64 --self-contained true -p:PublishSingleFile=true --output .\artifacts\publish\single-file-win-x64
```

## Run

1. Extract `TopBarPoC-workpc-win-x64.zip` to a user-writable folder, such as `%LOCALAPPDATA%\TopBarPoCTrial`.
2. Run `TopBarPoC.exe`.
3. If Windows blocks the executable, record the exact message and stop the trial unless local policy allows you to continue.

## Exit

Use the `x` button at the right edge of the top bar. This shuts down TopBar and unregisters its AppBar reservation.

If the process remains running, use Task Manager to end `TopBarPoC.exe`.

## Delete And Roll Back

1. Exit TopBar.
2. Delete the extracted portable folder.
3. Optionally delete `%APPDATA%\TopBarPoC` to remove settings and diagnostics logs.

TopBar does not install services, drivers, scheduled tasks, startup registration, shell extensions, or machine-wide files.

## Settings And Logs

Settings:

```text
%APPDATA%\TopBarPoC\settings.json
```

Diagnostics logs:

```text
%APPDATA%\TopBarPoC\logs\
```

Diagnostics failures are non-fatal; the app should continue if log creation is blocked.

## Known Corporate-PC Blockers

- Unsigned executable or unknown publisher warning.
- SmartScreen, antivirus, or EDR block/quarantine.
- AppLocker or WDAC policy blocking unsigned or user-profile executables.
- Company policy blocking execution from Downloads, Desktop, `%LOCALAPPDATA%`, or removable media.
- Restricted user profile preventing `%APPDATA%\TopBarPoC` writes.
- Security tools flagging shell/window behavior such as topmost transparent windows, AppBar registration, window enumeration, foreground activation, `WM_CLOSE`, or DWM thumbnails.

Admin rights should not be required to run the portable self-contained package, but may be required for policy allowlisting, trusted certificate deployment, Program Files deployment, or installing the framework-dependent runtime.

## Trial Checklist

Record pass/fail, display layout, taskbar settings, and any corporate security prompts.

- Launch TopBar from the extracted folder.
- Exit using the right-edge `x` button.
- Relaunch TopBar after exit.
- Restart Explorer and confirm TopBar re-registers or recovers cleanly.
- Sleep and resume the PC.
- Disconnect and reconnect a monitor.
- Change display scale while TopBar is running.
- Toggle Windows taskbar auto-hide.
- Confirm delete/rollback removes the extracted folder and optional `%APPDATA%\TopBarPoC` data.

## Known Runtime Risks For Trial

- Monitor hot-plug disconnect/reconnect is not fully validated.
- Sleep/resume is not yet explicitly validated.
- Top-edge taskbar auto-hide transitions were not directly reproducible on the current development machine.
- Full-screen exclusive app z-order policy is intentionally out of scope.
- Some helper or transient app windows may appear in TopBar even if they do not appear in the Windows taskbar.
