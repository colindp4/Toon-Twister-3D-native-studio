# Toon Twister 3-D Native Studio

Windows-native editor source for Toon Twister 3-D. It includes the editor code and an installer shell; original game media and converted game assets are kept local and are not part of this GitHub project.

## GitHub-safe contents

The `.gitignore` intentionally excludes `Assets/`, FFmpeg executables, build output, and personal `.ttnative` projects. Use Git or GitHub Desktop so those ignored files stay out of commits. Do not upload the folder by dragging all files into a web form.

## Local installation

Run `Toon Twister 3-D Setup.exe` from this folder, choose your own original Cartoon Twister 3-D installation, and choose a separate destination. The setup checks several original-file hashes and leaves the source installation untouched.

This local setup also reads the already-converted `Assets/Converted-v3` and `Assets/Audio` cache in this folder. Those files are ignored by Git and are not included in the setup executable. A fresh GitHub clone does not contain that cache, so it cannot create a complete editor installation from the original game folder alone yet; the standalone W3D/audio conversion step still needs to be added.

## Languages

The setup includes Dutch and English UI. It saves the selected language beside the installed editor executable, and the editor applies it to its controls, menus, timeline, and in-app dialogs on startup.

## Build notes

- Windows 11, .NET 10 Desktop Runtime for the installer, and .NET 8 Windows Desktop runtime for source builds.
- The bundled editor apphost is built for `win-x64`.
- To rebuild the native editor payload, run `dotnet publish ToonTwisterNative.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:LeanInstallerBuild=true -o InstallerPayload`.
- Then publish `Installer/ToonTwisterSetup.csproj` with `--self-contained false -p:PublishSingleFile=true -o InstallerSetupPublish` and copy `InstallerSetupPublish/ToonTwisterSetup.exe` to this folder as `Toon Twister 3-D Setup.exe`.
- The setup app is framework-dependent to keep its executable below GitHub's per-file size limit; users need the .NET 10 Desktop Runtime.

## Original game content

Every user must provide a legally obtained original installation. The installer copies game files locally from that selected folder; this repository and the installer executable do not distribute the original EXE, models, textures, audio, videos, or game data.
