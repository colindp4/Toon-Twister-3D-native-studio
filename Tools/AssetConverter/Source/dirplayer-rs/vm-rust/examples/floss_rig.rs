mod native_support;
use std::{env,fs,path::Path};
fn main(){let a:Vec<_>=env::args().collect();let mut scene=vm_rust::director::chunks::w3d::parse_w3d(&fs::read(&a[1]).unwrap()).unwrap();scene.clod_meshes.retain(|name,_| scene.nodes.iter().any(|n| n.visibility>0 && (n.model_resource_name==*name || n.resource_name==*name)));native_support::face(&scene,Path::new(&a[2]),Path::new(&a[3])).unwrap();}

