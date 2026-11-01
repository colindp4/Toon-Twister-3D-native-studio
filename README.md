# Toon Twister 3D: Reimagined — Windows installer

In **Direct**, use **Record voice** to choose a microphone and character, record a line, and add it to the timeline. Recording begins only after pressing Record. Windows microphone access must be enabled. **Speech bubbles** lets you choose a character, text, start time, and duration; bubbles follow the character and camera, appear on their own timeline lane, and are included in video exports. Microphone recordings are stored in the local user audio library; keep those files with your user profile when transferring projects.

The video exporter offers MP4 H.264, MP4 H.265/HEVC, and WebM VP9, at 480p through 2160p and 24, 30, or 60 fps. Choose image quality and encoding speed separately. NVIDIA H.264 encoding is offered after a local encoder test succeeds. GPU encoding speeds up compression; drawing the frames remains a separate part of export. Scene aspect ratios are preserved.

## Publishing updates

The editor checks the latest public GitHub Release at startup and via its Updates button. After the user agrees, it downloads the release ZIP, verifies GitHub's SHA-256 asset digest, and starts Setup. Setup reuses the installed editor's original-game path and language; if the path is unavailable the user selects their own original installation. The editor asks the user to save their work before closing.

The current editor version is **1.1.1**. Publish future updates as a GitHub Release with a higher numeric tag, such as `v1.2.0`, and exactly one ZIP asset. The ZIP must contain `Setup.exe`, `ToonTwisterNative.payload`, and `ffmpeg-win-x64.zip` (they may be in a single folder). Setup must support automatic updates and have file version 1.1.1.0 or newer. Update `CurrentRelease` and the Setup project version for each subsequent release. A GitHub source-code ZIP is not an installer ZIP. Do not include original game assets.

For Windows 11 x64. Download the repository ZIP, choose **Extract all**, and keep the extracted files together. Then double-click **Setup.exe**. The other files are installer payloads; you do not need to open them.

Setup defaults to English, with Dutch available in the language selector. Backgrounds do not receive automatic outlines, and transparent scenery is rendered back to front. Setup asks for a legally obtained original Cartoon Twister 3-D installation and a separate destination folder. It copies the original media locally, converts the 3D models, animations, and supported audio, installs Toon Twister 3D: Reimagined and its video encoder, and can launch the editor. It builds the original facial rigs from xml/lipsync.lst and renders the asset selection thumbnails locally. Installation checks both before reporting success. The original game folder remains untouched. Conversion can take a few minutes.

## Keep these files beside Setup.exe

- `ToonTwisterNative.payload` — editor program; Setup installs it automatically.
- `ffmpeg-win-x64.zip` — video encoder; Setup installs it automatically.
- `AssetConverter-Source.zip` and `AssetConverter-LICENSE-NOTICE.txt` — corresponding GPL converter source and license.
- `FFmpeg-LICENSE.txt` and `FFmpeg-README.txt` — FFmpeg notices.

This package contains no original game executable, models, textures, audio, movies, or converted game media. Setup creates converted assets locally from the original folder you select.

To update an existing Native Studio installation, close the editor and select its existing installation folder as the destination. Setup replaces application and generated game assets while keeping saved projects and custom audio. Other non-empty folders are rejected.



Character contours follow the animated surface and exclude the mouth interior. Speech blends original mouth poses smoothly using the same time-based curve for playback, timeline seeking, and video export.

Short original action loops are eased so they no longer flash through a few frames (including Patrick's Yell). Direct also has longer studio actions such as Dramatic yell, Slow nod, and Cheer. On the timeline, drag across empty track space to select multiple clips, then drag one selected clip to move the group. Ctrl-click adds or removes a clip from the selection. Selecting an audio clip reveals its volume slider; its level is saved in the project and applied to MP4 export.

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


The editor and setup display the Reimagined logo. Position, height, depth, rotation, and size can be adjusted with live sliders or precise numeric input. Numeric values beyond the initial slider range extend that range.

Patrick's jump deformation is repaired during local conversion from the original installation. A restrained default contour is used for Patrick; the contour thickness remains adjustable in the editor. Existing converted game assets are rebuilt when Setup is run again.

