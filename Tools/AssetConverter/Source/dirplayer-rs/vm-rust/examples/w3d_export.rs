use std::{env, fs, path::{Path, PathBuf}};
use vm_rust::director::chunks::w3d::parse_w3d;
#[path="native_support/mod.rs"] mod native_support;

fn main() {
    let args: Vec<String> = env::args().collect();
    if args.len() != 3 { eprintln!("usage: w3d-export <input-dir> <output-dir>"); std::process::exit(2); }
    let input = PathBuf::from(&args[1]);
    let output = PathBuf::from(&args[2]);
    fs::create_dir_all(&output).expect("create output");
    native_support::catalog(&input,&output);
    let mut files = Vec::new();
    collect(&input, &mut files);
    let (mut ok, mut failed) = (0usize, 0usize);
    for file in files {
        let relative = file.strip_prefix(&input).unwrap();
        let dest = output.join(relative).with_extension("obj");
        let Some(parent) = dest.parent() else { continue };
        fs::create_dir_all(parent).expect("create subdir");
        let data = match fs::read(&file) { Ok(d) => d, Err(e) => { eprintln!("READ {}: {e}", file.display()); failed += 1; continue; } };
        match parse_w3d(&data) {
            Ok(mut scene) => {
                scene.clod_meshes.retain(|name,_| scene.nodes.iter().any(|n| n.visibility>0 && (n.model_resource_name==*name || n.resource_name==*name)));
                if let Err(e)=native_support::face(&scene,&dest,&input.join("lipsync.json")){eprintln!("FACE {}: {e}",file.display());}
                if let Err(e)=native_support::animation(&scene,&dest) {eprintln!("ANIMATION {}: {e}",file.display());}
                let worlds = vm_rust::director::chunks::w3d::skeleton::build_node_world_matrices(&scene.nodes);
                let metadata = serde_json::json!({
                    "nodes": scene.nodes.iter().map(|n| serde_json::json!({"name":n.name.as_str(), "kind":format!("{:?}", n.node_type), "visibility":n.visibility,"resource":n.model_resource_name.as_str(),"matrix":worlds.get(&n.name), "fov":n.fov})).collect::<Vec<_>>(),
                    "skeletons":scene.skeletons.iter().map(|s| serde_json::json!({"name":s.name.as_str(), "bones":s.bones.len()})).collect::<Vec<_>>(),
                    "motions":scene.motions.iter().map(|m| serde_json::json!({"name":m.name.as_str(),"duration":m.duration(),"tracks":m.tracks.len()})).collect::<Vec<_>>()
                });
                let _ = fs::write(dest.with_extension("scene.json"), serde_json::to_vec_pretty(&metadata).unwrap());
                let stem = dest.file_stem().unwrap().to_string_lossy();
                let mtl_name = format!("{stem}.mtl");
                let obj = scene.export_obj_with_mtl(&mtl_name);
                let texture_dir = format!("{stem}_textures");
                let mtl = scene.export_mtl(&mtl_name).lines().map(|line| {
                    if let Some(file) = line.strip_prefix("map_Kd ") { format!("map_Kd {texture_dir}/{file}") } else { line.to_string() }
                }).collect::<Vec<_>>().join("\n") + "\n";
                if !obj.lines().any(|l| l.starts_with("f ")) { eprintln!("EMPTY {}", file.display()); failed += 1; continue; }
                if let Err(e) = fs::write(&dest, obj).and_then(|_| fs::write(dest.with_extension("mtl"), mtl)) { eprintln!("WRITE {}: {e}", dest.display()); failed += 1; continue; }
                let texture_path = parent.join(&texture_dir);
                for (name, bytes) in scene.export_textures() {
                    if let Err(e) = fs::create_dir_all(&texture_path).and_then(|_| fs::write(texture_path.join(name), bytes)) {
                        eprintln!("TEXTURE {}: {e}", file.display());
                    }
                }
                ok += 1;
            }
            Err(e) => { eprintln!("PARSE {}: {e}", file.display()); failed += 1; }
        }
    }
    println!("converted={ok} failed={failed}");
}
fn collect(dir: &Path, files: &mut Vec<PathBuf>) {
    let Ok(entries) = fs::read_dir(dir) else { return };
    for entry in entries.flatten() {
        let path = entry.path();
        if path.is_dir() { collect(&path, files); }
        else if path.extension().is_some_and(|e| e.eq_ignore_ascii_case("w3d")) { files.push(path); }
    }
}
