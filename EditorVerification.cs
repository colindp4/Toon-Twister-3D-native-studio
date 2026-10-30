using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;

namespace ToonTwisterNative;
public partial class MainWindow
{
    private async System.Threading.Tasks.Task SaveViewportArtifact(string path)
    {
        SceneViewport.UpdateLayout();await System.Threading.Tasks.Task.Delay(120);
        var drawing=new DrawingVisual();using(var dc=drawing.RenderOpen())dc.DrawRectangle(new VisualBrush(SceneViewport),null,new Rect(0,0,1024,576));
        var bitmap=new RenderTargetBitmap(1024,576,96,96,PixelFormats.Pbgra32);bitmap.Render(drawing);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using var file=File.Create(path);encoder.Save(file);
    }
    private async System.Threading.Tasks.Task VerifyEditorUpgrade(string output,string root)
    {
        var preferences=_preferences;_preferences=new StudioPreferences {Volume=.25,Fps=15,LipSync=false,Dark=true};ApplyStudioPreferences();
        if(Math.Abs(_motionTimer.Interval.TotalMilliseconds-1000d/15)>1)throw new Exception("Preview frame-rate setting failed.");
        _preferences=preferences;ApplyStudioPreferences();
        SceneDurationBox.Text="75";SetSceneDuration_Click(this,new RoutedEventArgs());var timed=JsonSerializer.Deserialize<SceneDocument>(JsonSerializer.Serialize(CaptureScene()))!;
        if(SceneDuration(timed)!=75||TimelineSlider.Maximum!=75)throw new Exception("Scene length does not save or extend the timeline.");SceneDurationBox.Text="Auto";SetSceneDuration_Click(this,new RoutedEventArgs());
        var voiceTest=Path.Combine(output,"stereo-voice-test.wav");
        using(var writer=new BinaryWriter(File.Create(voiceTest))){writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));writer.Write(36+48000*4);writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));writer.Write(16);writer.Write((short)1);writer.Write((short)2);writer.Write(48000);writer.Write(192000);writer.Write((short)4);writer.Write((short)16);writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));writer.Write(192000);for(var sample=0;sample<48000;sample++){writer.Write((short)0);writer.Write((short)(sample<24000?4000*Math.Sin(sample*.1):0));}}
        var envelope=AudioMouthEnvelope(voiceTest);if(envelope.Length!=30||!envelope.Take(15).Any(c=>c>'2')||envelope[^1]!='1')throw new Exception("Stereo voice envelope does not detect right-channel speech or close after silence.");
        var speaker=_models.Values.First(m=>NativeMotions.TryGetValue(m.Visual,out _));var importReference="UserAudio/test-"+Guid.NewGuid().ToString("N")+".wav";Directory.CreateDirectory(UserAudioDirectory);File.Copy(voiceTest,ResolveAudioPath(importReference));
        try
        {
            var voice=JsonSerializer.Deserialize<ImportedAudio>(JsonSerializer.Serialize(new ImportedAudio {Name="Own voice check",File=importReference,Category="voice",Model=speaker.Name,LipData=envelope,EnvelopeVersion=2}))!;
            var importedMusic=new ImportedAudio {Name="Own music check",File=importReference,Category="music",Model=speaker.Name,LipData=envelope,EnvelopeVersion=2};
            File.WriteAllText(Path.Combine(UserAudioDirectory,"manifest.json"),JsonSerializer.Serialize(new[]{voice}));ToolCombo.SelectedIndex=4;RefreshAudioChoices();
            if(AudioList.Items.OfType<AudioEntry>().Single(a=>a.file==importReference).Category!="voice")throw new Exception("Imported speech was lost when reopening the audio library.");
            File.WriteAllText(Path.Combine(UserAudioDirectory,"manifest.json"),JsonSerializer.Serialize(new[]{importedMusic}));RefreshAudioChoices();
            if(AudioList.Items.OfType<AudioEntry>().Single(a=>a.file==importReference).Category!="music")throw new Exception("Saved music choice did not refresh in the library.");ToolCombo.SelectedIndex=0;
            InsertSoundAt(ImportedEntry(voice),0);InsertSoundAt(ImportedEntry(importedMusic),0);
            for(var attempt=0;attempt<50&&_recordedSounds.Count(s=>s.File==importReference)<2;attempt++)await System.Threading.Tasks.Task.Delay(100);
            var ownVoice=_recordedSounds.Single(s=>s.File==importReference&&s.Category=="voice");var ownMusic=_recordedSounds.Single(s=>s.File==importReference&&s.Category=="music");
            if(ownVoice.Model!=speaker.Name||ownVoice.LipData!=envelope||ownMusic.Model!=""||ownMusic.LipData!=""||Lane(new TrackDisplay("music",Sound:ownMusic))!="|music")throw new Exception("Imported speech/music routing or mouth binding failed.");
            _scrubMode=true;TimelineSlider.Value=.2;if(VisemeFor(speaker)!=MouthAt(envelope,.2))throw new Exception("Imported voice did not drive selected actor's mouth.");
            File.WriteAllText(Path.Combine(output,"own-audio-result.txt"),"PASS: persisted speech/music selection; real WAV import into voice and music lanes; voice actor/lip envelope preserved; music has no actor or lip data; voice drives actor mouth.");
        }
        finally{StopAudio();_recordedSounds.RemoveAll(s=>s.File==importReference);File.Delete(ResolveAudioPath(importReference));RefreshMovieTracks();}
        var texturePath=Path.Combine(root,"bg","bg_sponge_17_hi_textures","bg_sponge_17_t_ship.jpg");var texture=LoadOriginalTexture(texturePath);
        if(texture.Format!=PixelFormats.Bgra32)throw new Exception("Original alpha plane was discarded.");
        var rgba=new byte[texture.PixelWidth*texture.PixelHeight*4];texture.CopyPixels(rgba,texture.PixelWidth*4,0);
        if(!Enumerable.Range(0,rgba.Length/4).Any(i=>rgba[i*4+3]==0)||!Enumerable.Range(0,rgba.Length/4).Any(i=>rgba[i*4+3]==255))throw new Exception("Original mask has no transparent and opaque pixels.");
        foreach(var id in new[]{"01","07","17","18"})
        {
            while(SceneViewport.Children.Count>1)SceneViewport.Children.RemoveAt(1);
            var path=Path.Combine(root,"bg",$"bg_sponge_{id}_hi.obj");SceneViewport.Children.Add(LoadObj(path));UseOriginalCamera(path);
            await SaveViewportArtifact(Path.Combine(output,$"background-{id}-alpha-fixed.png"));
        }
        var actor=_models["SpongeBob"];SceneViewport.Children.Add(actor.Visual);NativeMotions.TryGetValue(actor.Visual,out var motion);if(motion is null)throw new Exception("Missing actor animation.");
        _cameraTarget=new Point3D(.25,.46,0);_cameraDistance=.65;_cameraPitch=0;_cameraYaw=0;_originalVerticalFov=45;UpdateViewportAspect();UpdateCamera();
        motion.Sample(motion.IdleStart,1);var closed=((Model3DGroup)actor.Visual.Content).Children.OfType<GeometryModel3D>().SelectMany(m=>((MeshGeometry3D)m.Geometry).Positions).ToArray();
        await SaveViewportArtifact(Path.Combine(output,"mouth-closed.png"));
        motion.Sample(motion.IdleStart,4);var open=((Model3DGroup)actor.Visual.Content).Children.OfType<GeometryModel3D>().SelectMany(m=>((MeshGeometry3D)m.Geometry).Positions).ToArray();
        if(closed.Zip(open).Sum(p=>(p.First-p.Second).Length)<.001||open.Any(p=>!double.IsFinite(p.X+p.Y+p.Z)))throw new Exception("Original mouth overrides did not produce a valid different face.");
        await SaveViewportArtifact(Path.Combine(output,"mouth-speaking.png"));
        foreach(var file in Directory.EnumerateFiles(Path.Combine(root,"w3d"),"*.face")){var check=new NativeMotion(Path.ChangeExtension(file,".obj"),[]);check.Sample(check.IdleStart,4);}
        var clip=ClipsFor(actor.SourcePath).First(c=>c.name=="jump");_recordedActions.Clear();ActionDurationBox.Text="3";InsertActionAt(actor.Name,clip,0);InsertActionAt(actor.Name,clip,0);
        if(_recordedActions.Count!=2||_recordedActions[1].Start<_recordedActions[0].Start+_recordedActions[0].Duration-.001)throw new Exception("Inserting an action overwrote an existing one.");
        _recordedActions.Clear();var action=new RecordedAction {Model=actor.Name,Name="Jump",Start=2,Duration=2,SourceStart=clip.start/1000.0,SourceDuration=2};_recordedActions.Add(action);
        _dragTrack=new TrackDisplay("Jump",action);_clipDragOrigin=new Point(0,0);_clipOldStart=2;_clipOldDuration=2;_clipOldOffset=0;_pixelsPerSecond=40;_clipDragKind="move";
        if(Math.Abs(DragProposal(120).Start-5)>.001)throw new Exception("Dragging does not map pixels to time.");
        _clipDragKind="left";var trimmed=DragProposal(20);if(Math.Abs(trimmed.Start-2.5)>.001||Math.Abs(trimmed.Duration-1.5)>.001||Math.Abs(trimmed.Offset-.5)>.001)throw new Exception("Trim does not preserve source alignment.");
        _dragTrack=null;PushTimelineUndo();action.Start=5;UndoTimeline_Click(this,new RoutedEventArgs());if(_recordedActions.Single().Start!=2)throw new Exception("Timeline undo failed.");RedoTimeline_Click(this,new RoutedEventArgs());if(_recordedActions.Single().Start!=5)throw new Exception("Timeline redo failed.");
        ObjectList.SelectedItem=actor.Name;TimelineSlider.Value=0;AddKeyframe_Click(this,new RoutedEventArgs());TimelineSlider.Value=2;_models[actor.Name].X+=.5;ApplyTransform(_models[actor.Name]);AddKeyframe_Click(this,new RoutedEventArgs());
        if(_keyframes[actor.Name].Count!=2||!TimelineCanvas.Children.OfType<System.Windows.Shapes.Polygon>().Any())throw new Exception("Position keys were not drawn on the timeline.");
        _selectedKey=(actor.Name,2);DeletePositionKey();UndoTimeline_Click(this,new RoutedEventArgs());if(_keyframes[actor.Name].Count!=2)throw new Exception("Position key deletion could not be undone.");
        using var dialogue=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"Assets","Audio","dialogue.json")));var line=dialogue.RootElement[13].EnumerateArray().First(v=>v.GetProperty("lipdata").GetString()!.Distinct().Count()>1);
        var lips=line.GetProperty("lipdata").GetString()!;_recordedSounds.Clear();_recordedSounds.Add(new RecordedSound {Name="Dialogue",Model=actor.Name,LipData=lips,File=line.GetProperty("file").GetString()+".mp3",Start=0,Duration=lips.Length/30.0,OriginalDuration=lips.Length/30.0,SourceOffset=0,Category="voice"});
        _scrubMode=true;TimelineSlider.Value=.2;if(VisemeFor(actor)!=MouthAt(lips,.2))throw new Exception("Dialogue is not aligned with original 30-fps lipdata.");
        _projectPath=Path.Combine(output,"editor-roundtrip.ttnative");SaveProject_Click(this,new RoutedEventArgs());var saved=JsonSerializer.Deserialize<SceneDocument>(File.ReadAllText(_projectPath))!;RestoreMovie(saved);
        if(_recordedSounds.Single().Model!=actor.Name||_recordedSounds.Single().LipData!=lips)throw new Exception("Voice binding was lost on save/load.");
        var audioCatalog=JsonSerializer.Deserialize<List<AudioEntry>>(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"Assets","Audio","catalog.json")))!;
        var music=audioCatalog.First(a=>a.name=="mus_Paradise");InsertSoundAt(music,44);
        for(var attempt=0;attempt<50&&!_recordedSounds.Any(s=>s.File==music.file);attempt++)await System.Threading.Tasks.Task.Delay(100);
        var trimmedMusic=_recordedSounds.FirstOrDefault(s=>s.File==music.file);if(trimmedMusic is null||Math.Abs(trimmedMusic.Duration-1)>.01||trimmedMusic.OriginalDuration<=1)throw new Exception($"Long original music was not trimmed: duration={trimmedMusic?.Duration}, source={trimmedMusic?.OriginalDuration}, status={StatusText.Text}");
        ActorColumn.Width=new GridLength(210);InspectorColumn.Width=new GridLength(260);ToolRow.Height=new GridLength(200);TimelineRow.Height=new GridLength(260);UpdateLayout();
        if(Math.Abs(ActorColumn.ActualWidth-210)>1)throw new Exception("Panel sizing failed.");
        var original=InspectorHost.Child;FloatPanel(InspectorHost,"Properties check");if(!_floatingPanels.TryGetValue(InspectorHost,out var floating)||floating.Content!=original)throw new Exception("Floating panel did not receive its content.");floating.Close();if(InspectorHost.Child!=original)throw new Exception("Floating panel did not dock back.");ResetEditorLayout();
        RefreshMovieTracks();await SaveEditorWindowArtifact(Path.Combine(output,"editor-timeline.png"));
    }
    private async System.Threading.Tasks.Task SaveEditorWindowArtifact(string path)
    {
        UpdateLayout();await System.Threading.Tasks.Task.Delay(100);var bitmap=new RenderTargetBitmap((int)ActualWidth,(int)ActualHeight,96,96,PixelFormats.Pbgra32);bitmap.Render(this);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using var file=File.Create(path);encoder.Save(file);
    }
}

