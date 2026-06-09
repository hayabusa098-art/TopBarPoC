# TopBar Work-PC Portable Deployment

Build47B packages TopBar as a self-contained `win-x64` portable application. It does not
install services, drivers, scheduled tasks, shell extensions, or machine-wide files, and it
does not change runtime behavior.

## Build The Package

From the repository root, run:

```powershell
.\scripts\publish-workpc.ps1
```

The script publishes a multi-file, self-contained package and creates:

```text
artifacts\publish\workpc\TopBarPoC-workpc-win-x64\
artifacts\publish\workpc\TopBarPoC-workpc-win-x64.zip
```

It prints the ZIP SHA-256 after packaging. Record that value before transferring the ZIP.
To publish the folder without creating a ZIP, use `-NoZip`.

## Transfer And Verify

1. Transfer `TopBarPoC-workpc-win-x64.zip` using an approved company method.
2. On the work PC, calculate its hash:

   ```powershell
   Get-FileHash .\TopBarPoC-workpc-win-x64.zip -Algorithm SHA256
   ```

3. Confirm the hash matches the value printed by the publish script.
4. Extract the ZIP to an approved user-writable location, such as
   `%LOCALAPPDATA%\TopBarPoC` when corporate policy permits it.
5. Run `TopBarPoC.exe` from the extracted folder.

Do not bypass a corporate security block. Record the exact product, rule, and message and
request allowlisting through the approved IT/security process.

## Startup Shortcut

TopBar does not register itself for startup. If company policy allows a per-user startup
shortcut:

1. Press `Win+R`, enter `shell:startup`, and press Enter.
2. Create a shortcut in that folder targeting the extracted `TopBarPoC.exe`.
3. Set the shortcut's **Start in** directory to the extracted application folder.

Prefer an IT-managed deployment or startup policy where required. Do not use a scheduled task,
service, registry `Run` entry, or administrator elevation to bypass company controls.

## Update

1. Exit TopBar using the right-edge `x` button.
2. Preserve the previous extracted folder until the new package passes a smoke test.
3. Extract the new ZIP to a separate versioned or temporary folder.
4. Run the new `TopBarPoC.exe` and verify launch, AppBar reservation, exit, and relaunch.
5. Update the startup shortcut only after verification.

Settings and logs are stored separately under `%APPDATA%\TopBarPoC` and are not overwritten by
replacing the portable application folder.

## Rollback And Uninstall

Rollback:

1. Exit TopBar.
2. Point the startup shortcut back to the previous verified folder.
3. Launch the previous `TopBarPoC.exe` and verify the AppBar reservation.

Uninstall:

1. Exit TopBar and confirm `TopBarPoC.exe` is no longer running.
2. Delete its startup shortcut, if one was created.
3. Delete the extracted portable application folder.
4. Optionally delete `%APPDATA%\TopBarPoC` to remove settings and diagnostic logs.

No MSI/product registration or machine-wide uninstall step is required.

## Corporate Security Risks

- **SmartScreen:** the unsigned executable may show an unknown-publisher warning or be blocked.
- **Microsoft Defender:** reputation or behavior monitoring may quarantine or block the package.
- **EDR:** window enumeration, AppBar registration, foreground activation, `WM_CLOSE`, DWM
  thumbnails, and topmost windows may trigger behavioral controls.
- **AppLocker:** policy may block unsigned executables or execution from user-profile paths.
- **WDAC:** application-control policy may require an approved signer, catalog, hash, or managed
  installation path.
- Company policy may prohibit execution from Downloads, Desktop, removable media, or
  `%LOCALAPPDATA%`.

Admin rights are not normally required for the portable package. They may be required by IT for
allowlisting, trusted certificate deployment, or installation into a managed location.

## Smoke Test

- Confirm the transferred ZIP hash.
- Launch TopBar and confirm the top AppBar reservation.
- Hover singleton and grouped chips and confirm previews behave normally.
- Exit using the right-edge `x` button and confirm the reservation is released.
- Relaunch and confirm normal startup.
- Follow `WORKPC_TRIAL.md` for the broader work-PC validation checklist.
