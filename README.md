
![ez11gif-7485ed9ff88bb7b5](https://github.com/user-attachments/assets/44b57ee9-2fb1-4334-a14f-ee6103303768)


# MiniPlayer
Lightweight media control widget for the Windows taskbar
## ✨ Key Features
*   **Volume**: Control the volume of the **specific app** (Spotify, Chrome, etc.) using the mouse wheel.
*   **Dynamic Layouts**: Choose between horizontal or vertical (Top/Bottom) orientations.

*   **Persistent**: Remembers your preferred position and settings.
## 🖱 Controls
*   **Drag**: Move the player anywhere.
*   **Right-Click**: Access layout, volume, and source settings.
*   **Scroll Wheel**: Change volume of the active media source.

## 🛠 Quick Start
Requires [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).
**Build:**
```bash
dotnet build -c Release
```
**Single-File Export:**
```bash
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

## 🚀 Run without Building
1. Go to the **Releases** page on this GitHub repository.
2. Download the latest `MiniPlayer.exe`.
3. Run the file (no installation required). 
   *Note: Windows may show a SmartScreen warning because the app is unsigned. Click "More info" -> "Run anyway".*
