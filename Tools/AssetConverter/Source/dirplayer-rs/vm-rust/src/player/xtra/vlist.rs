//! Reader for the typed vList 1.x files shipped with Toon Twister 3-D.
//! The format is big-endian and stores nested lists of property records.
use fxhash::FxHashMap;
use crate::{director::{lingo::datum::Datum, static_datum::{StaticDatum, static_datum_to_runtime}}, player::{DatumRef, ScriptError, reserve_player_mut}};

struct Reader<'a> { b: &'a [u8], p: usize }
impl<'a> Reader<'a> {
    fn u32(&mut self) -> Result<u32, String> {
        let s=self.b.get(self.p..self.p+4).ok_or("unexpected end of vList")?;
        self.p+=4; Ok(u32::from_be_bytes(s.try_into().unwrap()))
    }
    fn f32(&mut self) -> Result<f64, String> { Ok(f32::from_bits(self.u32()?) as f64) }
    fn header(&mut self, ty:u32, mode:u32)->Result<usize,String>{
        let t=self.u32()?; let n=self.u32()?; let m=self.u32()?;
        if t!=ty || m!=mode || n>100_000 { return Err(format!("unexpected vList block at {:#x}: type={t}, count={n}, mode={m}",self.p-12)); }
        Ok(n as usize)
    }
    fn string(&mut self)->Result<String,String>{
        if self.u32()? != 0x103 { return Err("invalid vList string tag".into()); }
        let n=self.u32()? as usize;
        if n>1_000_000 { return Err("oversized vList string".into()); }
        let s=self.b.get(self.p..self.p+n).ok_or("truncated vList string")?;
        self.p+=n;
        let s=s.strip_suffix(&[0]).unwrap_or(s);
        Ok(String::from_utf8_lossy(s).into_owned())
    }
    fn scalar(&mut self)->Result<StaticDatum,String>{
        match self.u32()? { 1=>Ok(StaticDatum::Int(self.u32()? as i32)), 0x103=>{let n=self.u32()? as usize; if n>1_000_000{return Err("oversized vList string".into())} let s=self.b.get(self.p..self.p+n).ok_or("truncated vList string")?;self.p+=n;Ok(StaticDatum::String(String::from_utf8_lossy(s.strip_suffix(&[0]).unwrap_or(s)).into_owned()))}, t=>Err(format!("unsupported vList value tag {t:#x}")) }
    }
}

pub fn parse_vlist(bytes:&[u8])->Result<StaticDatum,String>{
    if bytes.len()<0x5c || u32::from_be_bytes(bytes[0..4].try_into().unwrap())!=3 {return Err("unrecognized vList header".into())}
    let mut r=Reader{b:bytes,p:0x5c};
    let schema_n=r.header(7,1)?;
    let mut schema=Vec::with_capacity(schema_n);
    for _ in 0..schema_n { schema.push(r.string()?); }
    let groups=r.header(7,1)?;
    let mut top=Vec::with_capacity(groups);
    for _ in 0..groups {
        let n=r.header(7,2)?;
        let mut items=Vec::with_capacity(n);
        for _ in 0..n {
            // Lipsync has one extra nested group level; other record blocks
            // are type 10, mode 3, while bone records are type 10, mode 4.
            let at=r.p;
            let ty=r.u32()?; let count=r.u32()?; let mode=r.u32()?;
            r.p=at;
            if ty==7 && mode==3 {
                let nested_n=r.header(7,3)?;
                let mut records=Vec::with_capacity(nested_n);
                for _ in 0..nested_n { records.push(parse_record(&mut r,&schema,4)?); }
                items.push(StaticDatum::List(records));
            } else {
                items.push(parse_record(&mut r,&schema,3)?);
            }
        }
        top.push(StaticDatum::List(items));
    }
    Ok(StaticDatum::List(top))
}

fn parse_record(r:&mut Reader<'_>,schema:&[String],mode:u32)->Result<StaticDatum,String>{
    r.header(10,mode)?;
    // Header count is the number of fields; rewind its value is unnecessary,
    // so read count directly from the header's original location.
    let count=u32::from_be_bytes(r.b[r.p-8..r.p-4].try_into().unwrap()) as usize;
    if count>schema.len(){return Err("vList record exceeds its schema".into())}
    let mut fields=Vec::with_capacity(count);
    for _ in 0..count {
        if r.u32()?!=2{return Err("invalid vList field marker".into())}
        let idx=r.u32()? as usize;
        if idx==0||idx>schema.len(){return Err("invalid vList field index".into())}
        let value=if mode==4 {
            match r.u32()? { 1=>StaticDatum::Int(r.u32()? as i32), 0x40=>StaticDatum::List((0..4).map(|_|r.f32().map(StaticDatum::Float)).collect::<Result<Vec<_>,_>>()?), t=>return Err(format!("unsupported vList bone value tag {t:#x}")) }
        } else { r.scalar()? };
        fields.push((StaticDatum::Symbol(schema[idx-1].clone()),value));
    }
    Ok(StaticDatum::PropList(fields))
}

struct VListInstance { path:String }
pub struct VListXtraManager { instances:FxHashMap<u32,VListInstance>, next:u32 }
impl VListXtraManager {
    pub fn new()->Self { Self{instances:FxHashMap::default(),next:0} }
    pub fn create_instance(&mut self,args:&Vec<DatumRef>)->Result<u32,ScriptError>{
        // TypeHandlers::new passes the constructor subject as args[0]
        // (`new(xtra "vList", fileName, ...)`), so the filename is args[1].
        let file_name = args.get(1).ok_or_else(|| ScriptError::new(
            "vList.new requires a fileName argument".to_string(),
        ))?;
        let path=reserve_player_mut(|p| p.get_datum(file_name).string_value())?;
        self.next+=1; self.instances.insert(self.next,VListInstance{path}); Ok(self.next)
    }
    pub fn has_async(name:&str)->bool { name.eq_ignore_ascii_case("read") }
    pub async fn read(instance:u32)->Result<DatumRef,ScriptError>{
        let path=unsafe{VLIST_XTRA_MANAGER_OPT.as_ref().and_then(|m|m.instances.get(&instance)).map(|i|i.path.clone())}.ok_or_else(||ScriptError::new("vList instance has no file path".to_string()))?;
        let relative=crate::player::xtra::fileio::resolve_override_path(&path).map(|(rel,_)|rel).unwrap_or_else(||path.rsplit(['\\','/']).next().unwrap_or(&path).to_string());
        let task=reserve_player_mut(|p|p.net_manager.preload_net_thing(relative.clone()));
        let result={let p=unsafe{crate::player::player_mut()}; if !p.net_manager.is_task_done(Some(task)){p.net_manager.await_task(task).await;} p.net_manager.get_task_result(Some(task))};
        let bytes=result.and_then(Result::ok).ok_or_else(||ScriptError::new(format!("vList could not load {}",relative)))?;
        let parsed=parse_vlist(&bytes).map_err(ScriptError::new)?;
        reserve_player_mut(|p|Ok(static_datum_to_runtime(&parsed,&mut p.allocator)))
    }
    pub fn call(handler:&str)->Result<DatumRef,ScriptError>{ Err(ScriptError::new(format!("vList.{} must be called asynchronously",handler))) }
}
pub static mut VLIST_XTRA_MANAGER_OPT:Option<VListXtraManager>=None;
pub fn borrow_vlist_manager_mut<T>(f:impl FnOnce(&mut VListXtraManager)->T)->T{
    unsafe { let m=VLIST_XTRA_MANAGER_OPT.get_or_insert_with(VListXtraManager::new); f(m) }
}
