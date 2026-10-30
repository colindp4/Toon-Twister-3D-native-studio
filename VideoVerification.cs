using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ToonTwisterNative;
public partial class MainWindow
{
    private async Task VerifyVideoEditing(string output)
    {
        var actor=_models.Values.First(m=>NativeMotions.TryGetValue(m.Visual,out _));
        _recordedActions.Clear();_recordedSounds.Clear();_keyframes.Clear();
        var source=ClipsFor(actor.SourcePath).First(c=>c.name=="jump");
        var action=new RecordedAction {Model=actor.Name,Name="Cut + loop check",Start=0,Duration=4,SourceStart=source.start/1000d,SourceDuration=(source.end-source.start)/1000d,SourceOffset=.2,Loop=true};
        _recordedActions.Add(action);var track=new TrackDisplay(action.Name,action);
        if(!SplitTrack(track,1.25)||_recordedActions.Count!=2||Math.Abs(_recordedActions[1].SourceOffset-1.45)>.001)throw new Exception("Razor lost animation source alignment.");
        UndoTimeline_Click(this,new RoutedEventArgs());if(_recordedActions.Count!=1)throw new Exception("Undo split failed.");RedoTimeline_Click(this,new RoutedEventArgs());if(_recordedActions.Count!=2)throw new Exception("Redo split failed.");
        var audio=JsonSerializer.Deserialize<List<AudioEntry>>(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"Assets","Audio","catalog.json")))!.First(a=>a.archive.Equals("Music.cxt",StringComparison.OrdinalIgnoreCase));
        var original=new MediaPlayer();var opened=new TaskCompletionSource<double>();original.MediaOpened+=(_,_)=>opened.TrySetResult(original.NaturalDuration.TimeSpan.TotalSeconds);original.MediaFailed+=(_,e)=>opened.TrySetException(e.ErrorException);original.Open(new Uri(Path.Combine(AppContext.BaseDirectory,"Assets","Audio",audio.file)));
        var duration=await opened.Task.WaitAsync(TimeSpan.FromSeconds(8));original.Close();
        var sound=new RecordedSound {Name="Music loop",File=audio.file,Start=0,Duration=4,OriginalDuration=duration,SourceOffset=.1,Loop=true,Category="music"};_recordedSounds.Add(sound);
        SplitTrack(new TrackDisplay(sound.Name,Sound:sound),1);if(_recordedSounds.Count!=2||Math.Abs(_recordedSounds[1].SourceOffset-1.1)>.001)throw new Exception("Razor lost audio offset.");
        if(Math.Abs(SoundTime(new RecordedSound {OriginalDuration=2,SourceOffset=1.5,Start=1,Loop=true},2)-.5)>.001)throw new Exception("Repeated audio source position failed.");
        _dragTrack=new TrackDisplay(sound.Name,Sound:sound);_clipOldStart=0;_clipOldDuration=1;_clipOldOffset=.1;_clipDragOrigin=new Point(0,0);_clipDragKind="right";_pixelsPerSecond=40;
        if(Math.Abs(DragProposal(360).Duration-10)>.001)throw new Exception("Audio edge cannot extend beyond source.");_dragTrack=null;
        SeekTimeline(1.5);Preview_Click(this,new RoutedEventArgs());if(!_timer.IsEnabled||Math.Abs((DateTime.UtcNow-_started).TotalSeconds-1.5)>.15)throw new Exception("Playback did not resume at playhead.");PausePlayback();if(_timer.IsEnabled||Math.Abs(TimelineSlider.Value-1.5)>.01)throw new Exception("Pause changed the playhead.");
        var preset=AvailableClips(actor.SourcePath).First(c=>c.preset=="bounce");_recordedActions.Clear();ActionDurationBox.Text="2";InsertActionAt(actor.Name,preset,0);
        var movement=_recordedActions.Single();var baseline=movement.PresetBase!.Y;ApplyPresetTransform(actor,.4,_recordedActions);if(actor.Y<=baseline+.2)throw new Exception("Studio movement did not animate actor.");ApplyPresetTransform(actor,2.1,_recordedActions);if(Math.Abs(actor.Y-baseline)>.001)throw new Exception("Studio movement drifted after end.");
        var studio=AvailableClips(actor.SourcePath).Where(c=>c.cat=="Studio").ToArray();if(studio.Length<15)throw new Exception("Studio animation menu is incomplete.");
        var motion=NativeMotions.GetValue(actor.Visual,_=>throw new Exception());var danceBody=BodyFor(actor.SourcePath,"floss")!;var danceSource=(danceBody.start+(danceBody.end-danceBody.start)*.25)/1000d;
        motion.Sample(danceSource);var plain=((System.Windows.Media.Media3D.Model3DGroup)actor.Visual.Content).Children.OfType<System.Windows.Media.Media3D.GeometryModel3D>().SelectMany(m=>((System.Windows.Media.Media3D.MeshGeometry3D)m.Geometry).Positions).ToArray();
        motion.Sample(danceSource,0,"floss",.25);var swung=((System.Windows.Media.Media3D.Model3DGroup)actor.Visual.Content).Children.OfType<System.Windows.Media.Media3D.GeometryModel3D>().SelectMany(m=>((System.Windows.Media.Media3D.MeshGeometry3D)m.Geometry).Positions).ToArray();
        if(plain.Zip(swung).Sum(p=>(p.First-p.Second).Length)<.5)throw new Exception("Floss arms did not visibly move relative to original body pose.");
        for(var click=0;click<50;click++){var choice=studio[click%studio.Length];_actionPreviewBase=new SavedTransform {X=baseline,Y=baseline,Z=0,Scale=1};_actionPreview=(actor.Name,choice,DateTime.UtcNow);ApplyPresetTransform(actor,.47,new[]{new RecordedAction {Model=actor.Name,Preset=choice.preset,PresetBase=_actionPreviewBase,Start=0,Duration=3,SourceDuration=(choice.end-choice.start)/1000d,Loop=true}});EndActionPreview();if(Math.Abs(actor.PresetTiltX)+Math.Abs(actor.PresetTiltZ)>.001)throw new Exception("Fast preview switching left a tilted model.");}
        foreach(var dance in studio)
        {var body=BodyFor(actor.SourcePath,dance.preset);if(body is null)throw new Exception("Studio dance has no original body animation: "+dance.preset);var sample=new RecordedAction {Model=actor.Name,Start=0,Duration=3,SourceDuration=(dance.end-dance.start)/1000d,Loop=true,Preset=dance.preset,PresetBase=new SavedTransform {Scale=1},BodyStart=body.start/1000d,BodyDuration=(body.end-body.start)/1000d,BodySpeed=BodySpeedFor(dance.preset)};ApplyPresetTransform(actor,.47,new[]{sample});if(!double.IsFinite(actor.X+actor.Y+actor.Z+actor.RotationY+actor.PresetTiltX+actor.PresetTiltZ+BodyTime(sample,.47,NativeMotions.GetValue(actor.Visual,_=>throw new Exception()))))throw new Exception("Invalid studio dance pose: "+dance.preset);}
        ApplyPresetTransform(actor,2.1,_recordedActions);
        _recordedSounds[0].Duration=1;_recordedSounds[1].Duration=1;_recordedSounds[1].Start=1;
        var scene=JsonSerializer.Deserialize<SceneDocument>(JsonSerializer.Serialize(CaptureScene()))!;
        scene.Duration=2.5;
        // Use a full original set rather than the face close-up used by the lip tests.
        scene.Aspect=0;scene.Camera=null;scene.Keyframes.Clear();scene.Transforms[actor.Name].X=0;scene.Transforms[actor.Name].Y=0;scene.Transforms[actor.Name].Scale=1;scene.Transforms[actor.Name].RotationY=0;
        scene.Transforms["Original set"]=new SavedTransform {SourcePath="Assets\\Converted-v3\\bg\\bg_sponge_17_hi.obj"};
        scene.Actions[0].PresetBase=new SavedTransform {Scale=1};
        scene.Actions[0].Preset="floss";scene.Actions[0].SourceDuration=1.8;scene.Actions[0].SourceOffset=0;scene.Actions[0].BodyStart=danceBody.start/1000d;scene.Actions[0].BodyDuration=(danceBody.end-danceBody.start)/1000d;scene.Actions[0].BodySpeed=1;
        var next=JsonSerializer.Deserialize<SceneDocument>(JsonSerializer.Serialize(scene))!;next.Duration=3.5;next.Transforms[actor.Name].X+=1;next.Actions.Clear();
        if(SceneDuration(scene)!=2.5||SceneDuration(next)!=3.5)throw new Exception("Explicit scene duration was not preserved by save/load.");
        var render=BuildExportView(scene,420,180);PoseExportView(render,scene,.4);var bmp=new RenderTargetBitmap(420,180,96,96,PixelFormats.Pbgra32);bmp.Render(render.Root);var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(bmp));using(var stream=File.Create(Path.Combine(output,"mp4-render-frame.png")))png.Save(stream);var pixels=new byte[420*180*4];bmp.CopyPixels(pixels,420*4,0);if(pixels.Where((_,i)=>i%4!=3).Distinct().Count()<50)throw new Exception("Export renderer produced an empty image.");
        var danceShot=BuildExportView(scene,840,360);PoseExportView(danceShot,scene,.45);var danceBmp=new RenderTargetBitmap(840,360,96,96,PixelFormats.Pbgra32);danceBmp.Render(danceShot.Root);var dancePng=new PngBitmapEncoder();dancePng.Frames.Add(BitmapFrame.Create(danceBmp));using(var stream=File.Create(Path.Combine(output,"floss-preview.png")))dancePng.Save(stream);
        var before=CaptureTimeline();var undo=_timelineUndo.Count;var playhead=TimelineSlider.Value;var mp4=Path.Combine(output,"export-check.mp4");
        await ExportMovieAsync(new[]{scene,next},mp4,1680,720,(_,_)=>{},CancellationToken.None);
        if(CaptureTimeline()!=before||_timelineUndo.Count!=undo||TimelineSlider.Value!=playhead)throw new Exception("Export changed the live editor.");
        var probe=new ProcessStartInfo(Path.Combine(AppContext.BaseDirectory,"Encoder","ffprobe.exe")){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};foreach(var arg in new[]{"-v","error","-show_streams","-show_format","-of","json",mp4})probe.ArgumentList.Add(arg);
        using var process=Process.Start(probe)!;var json=await process.StandardOutput.ReadToEndAsync();await process.WaitForExitAsync();File.WriteAllText(Path.Combine(output,"mp4-probe.json"),json);using var doc=JsonDocument.Parse(json);var streams=doc.RootElement.GetProperty("streams").EnumerateArray().ToArray();
        if(!streams.Any(s=>s.GetProperty("codec_name").GetString()=="h264"&&s.GetProperty("width").GetInt32()==1680&&s.GetProperty("height").GetInt32()==720&&s.GetProperty("r_frame_rate").GetString()=="30/1")||!streams.Any(s=>s.GetProperty("codec_name").GetString()=="aac")||Math.Abs(double.Parse(doc.RootElement.GetProperty("format").GetProperty("duration").GetString()!,System.Globalization.CultureInfo.InvariantCulture)-6)>.1)throw new Exception("Export did not produce a 6-second, two-scene ultrawide HD MP4 with H264 + AAC.");
        using var cancelled=new CancellationTokenSource();cancelled.Cancel();var cancelledPath=Path.Combine(output,"cancelled.mp4");File.WriteAllText(cancelledPath,"existing destination");try{await ExportMovieAsync(new[]{scene},cancelledPath,420,180,(_,_)=>{},cancelled.Token);throw new Exception("Cancelled export unexpectedly succeeded.");}catch(OperationCanceledException){}if(File.ReadAllText(cancelledPath)!="existing destination")throw new Exception("Cancelled export damaged previous destination.");
        File.WriteAllText(Path.Combine(output,"video-editing-result.txt"),"PASS: animation/audio cuts retain source offsets; split undo/redo; edge extension; loop source position; pause/resume; native movement without drift; nonempty deterministic render; two-scene MP4 H264/AAC 1680x720 30fps 6s; export preserves live timeline and undo; cancellation preserves existing destination.");
    }
}
