# Toon Twister 3-D Native Studio — Windows installer

For Windows 11 x64. Download the repository ZIP, choose **Extract all**, and keep the extracted files together. Then double-click **Setup.exe**. The other files are installer payloads; you do not need to open them.

Setup asks for a legally obtained original Cartoon Twister 3-D installation and a separate destination folder. It copies the original media locally, converts the 3D models, animations, and supported audio, installs Native Studio and its video encoder, and can launch the editor. The original game folder remains untouched. Conversion can take a few minutes.

## Keep these files beside Setup.exe

- `ToonTwisterNative.payload` — editor program; Setup installs it automatically.
- `ffmpeg-win-x64.zip` — video encoder; Setup installs it automatically.
- `AssetConverter-Source.zip` and `AssetConverter-LICENSE-NOTICE.txt` — corresponding GPL converter source and license.
- `FFmpeg-LICENSE.txt` and `FFmpeg-README.txt` — FFmpeg notices.

This package contains no original game executable, models, textures, audio, movies, or converted game media. Setup creates converted assets locally from the original folder you select.
