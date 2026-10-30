use std::{env,fs};
fn main(){let args:Vec<_>=env::args().collect();let scene=vm_rust::director::chunks::w3d::parse_w3d(&fs::read(&args[1]).unwrap()).unwrap();for s in scene.skeletons{for(i,b)in s.bones.iter().enumerate(){println!("{} {} parent={} len={:.3} pos={:.3},{:.3},{:.3}",i+1,b.name.as_str(),b.parent_index,b.length,b.dir_x,b.dir_y,b.dir_z);}}}
