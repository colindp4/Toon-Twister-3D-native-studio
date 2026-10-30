use std::{fs,io::Write,path::Path};
use vm_rust::director::chunks::w3d::{W3dScene,skeleton};
use vm_rust::director::static_datum::StaticDatum;
pub fn value(d:&StaticDatum)->serde_json::Value { use StaticDatum::*; match d { Int(i)=>serde_json::json!(i),Float(f)=>serde_json::json!(f),String(s)|Symbol(s)=>serde_json::json!(s),List(v)=>serde_json::Value::Array(v.iter().map(value).collect()),PropList(p)=>serde_json::Value::Object(p.iter().filter_map(|(k,v)| match k {String(s)|Symbol(s)=>Some((s.clone(),value(v))),_=>None}).collect()),_=>serde_json::Value::Null } }
pub fn catalog(input:&Path,output:&Path) { if let Ok(b)=fs::read(input.join("xml/animlist.lst")) { if let Ok(d)=vm_rust::player::xtra::vlist::parse_vlist(&b) { let _=fs::write(output.join("actions.json"),serde_json::to_vec_pretty(&value(&d)).unwrap()); } } }
fn mul(a:&[f32;16],b:&[f32;16])->[f32;16] { let mut o=[0.;16]; for c in 0..4 {for r in 0..4 {for k in 0..4 {o[c*4+r]+=a[k*4+r]*b[c*4+k];}}}o }
fn point(m:&[f32;16],p:[f32;3])->[f32;3] {[m[0]*p[0]+m[4]*p[1]+m[8]*p[2]+m[12],m[1]*p[0]+m[5]*p[1]+m[9]*p[2]+m[13],m[2]*p[0]+m[6]*p[1]+m[10]*p[2]+m[14]]}
fn identity()->[f32;16] {[1.,0.,0.,0.,0.,1.,0.,0.,0.,0.,1.,0.,0.,0.,0.,1.]}
fn rigid_inverse(m:&[f32;16])->[f32;16] {let mut o=identity();for c in 0..3{for r in 0..3{o[c*4+r]=m[r*4+c];}}let t=point(&o,[-m[12],-m[13],-m[14]]);o[12]=t[0];o[13]=t[1];o[14]=t[2];o}
pub fn face(scene:&W3dScene,dest:&Path,lip_path:&Path)->Result<(),Box<dyn std::error::Error>> {
 let stem=dest.file_stem().unwrap().to_string_lossy();if !stem.starts_with("act_")||!stem.ends_with("_hi"){return Ok(())}
 let order=["angelica","carl","chuckie","cindy","cosmo","fluffy","goddard","jimmy","kimi","libby","mrkrabs","patrick","plankton","spongebob","squidward","tommy","timmy","vicky","wanda","gary"];
 let Some(index)=order.iter().position(|n|stem==format!("act_{n}_hi"))else{return Ok(())};
 let Some(rig)=scene.skeletons.first()else{return Ok(())};let Some(motion)=scene.motions.iter().find(|m|m.name==rig.name)else{return Ok(())};
 let lips:serde_json::Value=serde_json::from_slice(&fs::read(lip_path)?)?;
 let Some(shapes)=lips[index].as_array()else{return Ok(())};
 let mut overrides=vec![];let mut affected=vec![false;rig.bones.len()];
 for shape in shapes.iter().take(8){let mut rows=vec![];for bone in shape.as_array().unwrap(){
  let id=bone["boneid"].as_u64().unwrap() as usize;if id==0{continue}if id>rig.bones.len(){return Err("mouth bone ID outside skeleton".into())}let id=id-1;
  let rot=&bone["rot"];let mut m=vm_rust::player::handlers::datum_handlers::transform3d::euler_to_matrix(rot[0].as_f64().unwrap(),rot[1].as_f64().unwrap(),rot[2].as_f64().unwrap()).map(|f|f as f32);
  for i in 0..3{m[12+i]=bone["pos"][i].as_f64().unwrap() as f32;}rows.push((id,m));affected[id]=true;
 }overrides.push(rows);}
 if stem=="act_spongebob_hi" {for i in [25usize,26,38,39,51,57]{affected[i]=true;}}
 for i in 0..rig.bones.len(){let p=rig.bones[i].parent_index;if p>=0&&affected[p as usize]{affected[i]=true;}}
 let mut vertices=vec![];let mut vertex_count=0;let basis=skeleton::export_basis_transform();
 for (resource,meshes) in &scene.clod_meshes{
  let mut node=scene.find_transform_for_resource_pub(*resource).unwrap_or(identity());if let Some(f)=scene.model_root_com.get(&resource.as_str().to_ascii_lowercase()){node=mul(&node,&rigid_inverse(f));}let world=mul(&basis,&node);
  for mesh in meshes{for (i,p) in mesh.positions.iter().enumerate(){
   if let (Some(joints),Some(weights))=(mesh.bone_indices.get(i),mesh.bone_weights.get(i)){
    if joints.iter().zip(weights).any(|(&j,&w)|w>0.00001&&affected.get(j as usize)==Some(&true)){vertices.push((vertex_count,*p,world,joints.clone(),weights.clone()));}
   }vertex_count+=1;
  }}
 }
 vertex_count+=scene.raw_meshes.iter().map(|m|m.positions.len()).sum::<usize>();
 let fps=15f32;let frames=(motion.duration()*fps).ceil() as u32+1;
 let mut out=std::io::BufWriter::new(fs::File::create(dest.with_extension("face"))?);
 out.write_all(b"TTF1")?;for n in [vertex_count as u32,rig.bones.len() as u32,frames,fps.to_bits(),vertices.len() as u32]{out.write_all(&n.to_le_bytes())?;}
 let inv=skeleton::build_inverse_bind_matrices(rig);
 for (i,b) in rig.bones.iter().enumerate(){out.write_all(&b.parent_index.to_le_bytes())?;out.write_all(&b.length.to_le_bytes())?;for f in inv[i]{out.write_all(&f.to_le_bytes())?;}}
 out.write_all(&(overrides.len() as u32).to_le_bytes())?;
 for shape in overrides{out.write_all(&(shape.len() as u32).to_le_bytes())?;for (id,m) in shape{out.write_all(&(id as u32).to_le_bytes())?;for f in m{out.write_all(&f.to_le_bytes())?;}}}
 for (index,p,world,joints,weights) in vertices{out.write_all(&(index as u32).to_le_bytes())?;for f in p{out.write_all(&f.to_le_bytes())?;}for f in world{out.write_all(&f.to_le_bytes())?;}out.write_all(&(joints.len() as u32).to_le_bytes())?;for (j,w) in joints.iter().zip(weights){out.write_all(&j.to_le_bytes())?;out.write_all(&w.to_le_bytes())?;}}
 for frame in 0..frames{for m in skeleton::build_bone_matrices(rig,Some(motion),frame as f32/fps){for f in m{out.write_all(&f.to_le_bytes())?;}}}
 Ok(())
}
// Native vertex animation cache. Frames keep the original source vertex order;
// the desktop importer maps indexed triangles back to these shared vertices.
pub fn animation(scene:&W3dScene,dest:&Path)->Result<(),Box<dyn std::error::Error>> {
 if !dest.file_stem().unwrap().to_string_lossy().ends_with("_hi"){return Ok(())}
 let Some(rig)=scene.skeletons.first() else{return Ok(())};
 let Some(motion)=scene.motions.iter().find(|m|m.name==rig.name).or(scene.motions.iter().find(|m|m.tracks.len()>1)) else{return Ok(())};
 let duration=motion.duration();if duration<=0. {return Ok(())}
 let count=scene.clod_meshes.values().flat_map(|m|m.iter()).map(|m|m.positions.len()).sum::<usize>()+scene.raw_meshes.iter().map(|m|m.positions.len()).sum::<usize>();
 let fps=15.;let frames=(duration*fps).ceil() as u32+1;
 let mut file=std::io::BufWriter::new(fs::File::create(dest.with_extension("motion"))?);
 file.write_all(b"TTM1")?;file.write_all(&(count as u32).to_le_bytes())?;file.write_all(&frames.to_le_bytes())?;file.write_all(&fps.to_le_bytes())?;
 let inv=skeleton::build_inverse_bind_matrices(rig);let basis=skeleton::export_basis_transform();
 for frame in 0..frames {
  let pose=skeleton::build_bone_matrices(rig,Some(motion),(frame as f32/fps).min(duration));
  let skin:Vec<_>=pose.iter().zip(&inv).map(|(p,i)|mul(p,i)).collect();
  for (resource,meshes) in &scene.clod_meshes {
   let node=scene.find_transform_for_resource_pub(*resource).unwrap_or(identity());
   let folded=scene.model_root_com.get(&resource.as_str().to_ascii_lowercase());
   let node=if let Some(folded)=folded{mul(&node,&rigid_inverse(folded))}else{node};
   let world=mul(&basis,&node);
   for mesh in meshes {for (v,p) in mesh.positions.iter().enumerate(){
    let mut posed=*p;
    if let (Some(joints),Some(weights))=(mesh.bone_indices.get(v),mesh.bone_weights.get(v)) {
     let mut sum=[0.;3];let mut total=0.;for (&j,&w) in joints.iter().zip(weights){if let Some(m)=skin.get(j as usize){let q=point(m,*p);for k in 0..3{sum[k]+=q[k]*w;}total+=w;}}
     if total>0.0001 {for k in 0..3{sum[k]/=total;}posed=sum;}
    }
    let q=point(&world,posed);for f in q{file.write_all(&f.to_le_bytes())?;}
   }}
  }
  for mesh in &scene.raw_meshes{for p in &mesh.positions{for f in point(&basis,*p){file.write_all(&f.to_le_bytes())?;}}}
 }
 Ok(())
}


