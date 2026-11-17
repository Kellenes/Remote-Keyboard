# Remote Keyboard

Remote Keyboard lets you press keys and send text to a Windows PC from a browser on your phone.

## Quick start

1. Download and extract the Windows build. Keep the `wwwroot` folder next to `KeyboardController.exe`. [Downloads](https://github.com/Kellenes/Remote-Keyboard/releases)
2. Run `KeyboardController.exe`. Right-click its tray icon and select **Show logs** to see the startup status.
3. When the public link is ready, select **Copy link** in the tray menu and open it on your phone. On the PC running the app, you can also open `http://127.0.0.1:8181/`.
4. Tap the buttons to send key presses. If the layout has a text field, type your text and press **Enter** on the page to send it.

## Layouts

- `wwwroot/Classic.html` is the included layout. You can rename or remove it.
- To add a layout, save a page with the [layout editor](https://github.com/Kellenes/Remote-Keyboard-Grid-Builder) and place the `.html` file directly in `wwwroot`. Refresh the page and select the layout from the dropdown.
- The home page opens the first layout alphabetically. A link to a removed layout goes to the first remaining one.

## Settings

The app creates `settings.json` next to the executable on first launch. The default local port is `8181`. Leave `publicUrl` empty to use Localtunnel, or follow the [ngrok setup guide](NGROK.md) to use your own URL. Restart the app after editing the settings.

Anyone who can access the public page can send keystrokes to your PC. Keep the link private and close the app from the tray when you finish.
