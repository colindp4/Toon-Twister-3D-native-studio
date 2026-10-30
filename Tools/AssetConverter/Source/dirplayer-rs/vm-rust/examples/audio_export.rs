use std::{env,fs,path::PathBuf};
use vm_rust::director::{file::read_director_file_bytes,chunks::{Chunk,sound::SoundChunk}};
#[path="native_support/mod.rs"] mod native_support;
fn main(){
 let args:Vec<_>=env::args().collect();let input=PathBuf::from(&args[1]);let output=PathBuf::from(&args[2]);fs::create_dir_all(&output).unwrap();let mut catalog=vec![];
 if let Ok(bytes)=fs::read(input.join("xml/audiodata.lst")){if let Ok(d)=vm_rust::player::xtra::vlist::parse_vlist(&bytes){fs::write(output.join("dialogue.json"),serde_json::to_vec_pretty(&native_support::value(&d)).unwrap()).unwrap();}}
 for entry in fs::read_dir(&input).unwrap().flatten(){let path=entry.path();if !path.extension().is_some_and(|e|e=="cxt"||e=="dxr"){continue}
 let name=path.file_name().unwrap().to_string_lossy();let bytes=fs::read(&path).unwrap();
 let file=match read_director_file_bytes(&bytes,&name,"file:///toon/"){Ok(f)=>f,Err(e)=>{eprintln!("{}: {e}",name);continue}};
 for cast in &file.casts{for (id,member) in &cast.members{
 let children:Vec<_>=member.children.iter().flatten().collect();
 let header=children.iter().find_map(|c|if let Chunk::SndHeader(h)=c{Some(h)}else{None});
 let samples=children.iter().find_map(|c|if let Chunk::SndSamples(s)=c{Some(s)}else{None});
 let sound=if let (Some(h),Some(s))=(header,samples){Some(SoundChunk::from_snd_header_and_samples(h,s))}else{children.iter().find_map(|c|match c{Chunk::Sound(s)=>Some(s.clone()),Chunk::Media(m)=>Some(SoundChunk::from_media(m)),_=>None})};
 if let Some(sound)=sound{
 let membername=member.chunk.member_info.as_ref().map(|i|i.name.clone()).unwrap_or(format!("sound_{id}"));let codec=sound.codec();
 let ext=if codec=="mp3"{"mp3"}else if codec=="raw_pcm"{"wav"}else{eprintln!("UNSUPPORTED {membername}: {codec}");continue};
 let safe:String=membername.chars().map(|c|if c.is_alphanumeric()||c=='_'||c=='-'{c}else{'_'}).collect();let filename=format!("{safe}.{ext}");
 fs::write(output.join(&filename),if ext=="wav"{sound.to_wav()}else{sound.data()}).unwrap();
 catalog.push(serde_json::json!({"name":membername,"file":filename,"archive":name,"codec":codec,"rate":sound.sample_rate(),"channels":sound.channels(),"duration":sound.sample_count() as f64/sound.sample_rate().max(1) as f64}));
 }
 }}
 }
 fs::write(output.join("catalog.json"),serde_json::to_vec_pretty(&catalog).unwrap()).unwrap();println!("exported {} sounds",catalog.len());
}
