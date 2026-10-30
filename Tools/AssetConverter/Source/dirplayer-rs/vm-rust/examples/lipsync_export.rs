use std::{env,fs};
#[path="native_support/mod.rs"] mod native_support;
fn main(){let a:Vec<_>=env::args().collect();let b=fs::read(&a[1]).unwrap();let d=vm_rust::player::xtra::vlist::parse_vlist(&b).unwrap();fs::write(&a[2],serde_json::to_vec_pretty(&native_support::value(&d)).unwrap()).unwrap();}
