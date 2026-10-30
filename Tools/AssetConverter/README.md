# Local asset converter

The setup bundles two command-line tools built from the `dirplayer-rs` Rust project. They convert W3D geometry/animation and Director audio from the original installation selected by the user. They do not contain any game media. All converted outputs are generated locally during setup and copied only into the user's separate destination folder.

The upstream project is GPL-3.0-only. Its corresponding Rust source and license are included under `Source/dirplayer-rs`. The two export examples in `vm-rust/examples` are the corresponding source for the bundled tools (`w3d_export.rs` and `audio_export.rs`); `w3d_export` uses `examples/native_support/mod.rs`.

To rebuild on Windows with Rust installed, run these commands from `Source/dirplayer-rs/vm-rust`:

```powershell
cargo build --example w3d_export --example audio_export
Copy-Item target\debug\examples\w3d_export.exe ..\..\..\w3d_export.exe
Copy-Item target\debug\examples\audio_export.exe ..\..\..\audio_export.exe
```

The editor's facial mouth-shape sidecar is an optional local enhancement and is not generated when the original install does not provide the extra lip-sync table. Model geometry, skeletal motion and supported Director audio are converted by setup.
