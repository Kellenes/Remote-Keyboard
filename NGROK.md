# Set up ngrok for Remote Keyboard

Run ngrok on the same Windows PC as Remote Keyboard. The examples use port `8181`. If you changed `port` in `settings.json`, use that number in all the commands and addresses below.

## 1. Check the local page

Start `KeyboardController.exe` and open `http://127.0.0.1:8181/` on the PC. Check that a layout appears. The app creates `settings.json` next to the executable on first launch.

## 2. Install and connect ngrok

Install ngrok and sign in to its dashboard. [Windows download](https://ngrok.com/download/windows) · [Dashboard](https://dashboard.ngrok.com/)

Open **Your Authtoken** in the dashboard, copy your token and run this command in PowerShell with your value in place of `YOUR_AUTHTOKEN`:

```powershell
ngrok config add-authtoken "YOUR_AUTHTOKEN"
```

## 3. Start the tunnel and find the link

```powershell
ngrok http http://127.0.0.1:8181
```

Copy the HTTPS address from the **Forwarding** line in the ngrok terminal. Your assigned domain is also listed under **Domains** in the ngrok dashboard. Leave the terminal running while you use the tunnel. [ngrok CLI quickstart](https://ngrok.com/docs/gateway/endpoints/agent-cli-quickstart)

## 4. Set the URL in Remote Keyboard

Open `settings.json` next to `KeyboardController.exe`. Set `publicUrl` to the HTTPS address you copied, using the domain alone without a layout filename or query string.

```json
{
  "port": 8181,
  "publicUrl": "https://your-assigned-name.ngrok-free.dev"
}
```

Exit Remote Keyboard from its tray menu, then start it again with ngrok still running. Use **Show logs** to check which URL the app is using. Once the ngrok URL is accepted, select **Copy link** and open it on your phone.

If the app switches to Localtunnel, check that the tunnel is still running, that its port matches `settings.json` and that the local page works on the PC.
