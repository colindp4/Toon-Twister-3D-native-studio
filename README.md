# Toon Twister 3-D Native Studio — Windows installer
<img width="1378" height="876" alt="image" src="https://github.com/user-attachments/assets/4347bb3a-979d-4ba0-aee2-35b0d975c2ba" /> <img width="1372" height="907" alt="image" src="https://github.com/user-attachments/assets/6fa5aa3a-b816-4a30-b0c8-74338e7b672b" />


For Windows 11 x64. Download the repository ZIP, choose **Extract all**, and keep the extracted files together. Then double-click **Setup.exe**. The other files are installer payloads; you do not need to open them.

Setup defaults to English, with Dutch available in the language selector. Backgrounds do not receive automatic outlines, and transparent scenery is rendered back to front. Setup asks for a legally obtained original Cartoon Twister 3-D installation and a separate destination folder. It copies the original media locally, converts the 3D models, animations, and supported audio, installs Native Studio and its video encoder, and can launch the editor. It builds the original facial rigs from xml/lipsync.lst and renders the asset selection thumbnails locally. Installation checks both before reporting success. The original game folder remains untouched. Conversion can take a few minutes.

## Keep these files beside Setup.exe

- `ToonTwisterNative.payload` — editor program; Setup installs it automatically.
- `ffmpeg-win-x64.zip` — video encoder; Setup installs it automatically.
- `AssetConverter-Source.zip` and `AssetConverter-LICENSE-NOTICE.txt` — corresponding GPL converter source and license.
- `FFmpeg-LICENSE.txt` and `FFmpeg-README.txt` — FFmpeg notices.

This package contains no original game executable, models, textures, audio, movies, or converted game media. Setup creates converted assets locally from the original folder you select.

To update an existing Native Studio installation, close the editor and select its existing installation folder as the destination. Setup replaces application and generated game assets while keeping saved projects and custom audio. Other non-empty folders are rejected.



Character contours follow the animated surface and exclude the mouth interior. Speech blends original mouth poses smoothly using the same time-based curve for playback, timeline seeking, and video export.

## Import your own characters

The installer includes GLB 2.0 character import and automatic basic character setup in the native Windows editor. No plug-in, cloud upload, or browser is needed.

1. Click **Import model** and choose a **.glb** with embedded PNG/JPEG textures. Export without Draco, Meshopt, or KTX2 compression.
2. Preview your character and save it to **My models**. Body-part mappings are optional, under **Advanced mappings**.
3. Click **Add to scene**. Selecting a library item only previews it.
4. Use **Direct** for walking, waving, looking around, nodding, talking gestures, and embedded animation clips.
5. Choose **Talk** or **Custom audio**, import a sound and add it to the character's voice track. Audio attachment works even if mouth animation is disabled.

Existing skeletons and mouth shapes take priority. When these are absent, the editor estimates a simple movement rig from the model's proportions and adds a stylized speech mouth on its surface. This is a geometric approximation for upright characters facing forward, not universal anatomical recognition. Unusual shapes, poses, or orientations can need adjustment; the automatic body and mouth can be switched off in the import preview. Models with an authored rig and facial shapes give more accurate results.

The renderer uses base-color textures/materials; advanced PBR effects are not reproduced. Bake varying vertex colors into the base-color texture before export. The limit is 128 MB per model and 250,000 source vertices; smaller models are recommended for smooth editing.

Imported GLB data and character settings are included when you save a .ttnative project. Your model library is stored in your Windows user profile and survives editor updates. Existing imported models get the automatic fallback when reopening them in the updated editor. Imported user models are not included in this GitHub package.

## Scene editing

Select an item in **In your scene** and press **Delete**, or use the **Delete** button below the list. This removes the actor/prop/background from the current scene and removes its attached actions, voice clips, and position keys. The library model and source audio files are kept. Unrelated music stays on the timeline.

**Add to scene** adds the selected character or prop as an independent object; selecting a background replaces the active background. Browsing only previews the selection, and leaving Prep restores the committed scene and camera. Adding characters or props preserves the background camera. Asset cards remain selectable as their thumbnails load; searching first is not required.
