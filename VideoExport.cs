using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using System.Windows.Threading;
using Microsoft.Win32;

namespace ToonTwisterNative;
public partial class MainWindow
{
    private bool _exporting;
    private static string Number(double value)=>value.ToString("0.######",CultureInfo.InvariantCulture);
    private async void ExportMp4_Click(object sender,RoutedEventArgs e)
    {
        if(_exporting)return;
        if(_models.Count==0){StatusText.Text="Maak eerst een scène om te exporteren.";return;}
        PausePlayback();
        var dialog=new Window {Title="Film exporteren als MP4",Owner=this,Width=420,Height=285,ResizeMode=ResizeMode.NoResize,WindowStartupLocation=WindowStartupLocation.CenterOwner,Background=Brushes.Honeydew};
        var panel=new StackPanel {Margin=new Thickness(18)};
        panel.Children.Add(new TextBlock {Text="MP4 · H.264 beeld + AAC geluid",FontWeight=FontWeights.Bold,Margin=new Thickness(0,0,0,12)});
        var scope=new ComboBox {ItemsSource=new[]{"Hele film — alle scènes","Alleen deze scène"},SelectedIndex=0};panel.Children.Add(scope);
        var quality=new ComboBox {ItemsSource=new[]{"HD — 720 pixels hoog","Full HD — 1080 pixels hoog"},SelectedIndex=0};panel.Children.Add(quality);
        panel.Children.Add(new TextBlock {Text="30 beelden per seconde. De beeldverhouding van elke\nscène blijft behouden; afwijkende scènes krijgen randen.",Margin=new Thickness(3,10,0,10)});
        var export=new Button {Content="Kies bestand en exporteer"};export.Click+=(_,_)=>dialog.DialogResult=true;panel.Children.Add(export);dialog.Content=panel;
        if(dialog.ShowDialog()!=true)return;
        var save=new SaveFileDialog {Filter="MP4-video (*.mp4)|*.mp4",DefaultExt=".mp4",FileName=Path.GetFileNameWithoutExtension(_projectPath??"Mijn Toon")+".mp4"};
        if(save.ShowDialog(this)!=true)return;
        _scenes[_sceneIndex]=CaptureScene();
        // Serialize to detach the render snapshot from the live editor and its undo history.
        var scenes=JsonSerializer.Deserialize<List<SceneDocument>>(JsonSerializer.Serialize(scope.SelectedIndex==0?_scenes:[CaptureScene()]))!;
        var aspect=AspectCombo.SelectedIndex switch {1=>16d/9,2=>4d/3,_=>21d/9};var height=quality.SelectedIndex==1?1080:720;var width=(int)Math.Round(height*aspect/2)*2;
        using var cancel=new CancellationTokenSource();
        var progress=new Window {Title="MP4 maken",Owner=this,Width=430,Height=185,ResizeMode=ResizeMode.NoResize,WindowStartupLocation=WindowStartupLocation.CenterOwner,Background=Brushes.Honeydew};
        var body=new StackPanel {Margin=new Thickness(20)};var label=new TextBlock {Text="Scènes voorbereiden…"};var bar=new ProgressBar {Minimum=0,Maximum=1,Height=20,Margin=new Thickness(0,12,0,12)};var stop=new Button {Content="Annuleren"};
        stop.Click+=(_,_)=>{cancel.Cancel();stop.IsEnabled=false;label.Text="Export annuleren…";};progress.Closing+=(_,args)=>{if(_exporting){args.Cancel=true;cancel.Cancel();}};
        body.Children.Add(label);body.Children.Add(bar);body.Children.Add(stop);progress.Content=body;
        var wasMotion=_motionTimer.IsEnabled;_motionTimer.Stop();_exporting=true;IsEnabled=false;progress.Show();
        try
        {
            await ExportMovieAsync(scenes,save.FileName,width,height,(fraction,message)=>{bar.Value=fraction;label.Text=message;},cancel.Token);
            StatusText.Text="MP4 opgeslagen: "+save.FileName;
        }
        catch(OperationCanceledException){StatusText.Text="MP4-export geannuleerd. Je project is behouden.";}
        catch(Exception error){StatusText.Text="MP4-export mislukt: "+error.Message;MessageBox.Show(progress,StudioLocalization.Translate(error.Message),StudioLocalization.Translate("MP4-export"),MessageBoxButton.OK,MessageBoxImage.Error);}
        finally{_exporting=false;IsEnabled=true;progress.Close();if(wasMotion)_motionTimer.Start();}
    }

    private sealed record ExportView(Grid Root,Viewport3D View,List<SceneModel> Models);
    private ExportView BuildExportView(SceneDocument scene,int width,int height)
    {
        var ratio=scene.Aspect switch {1=>16d/9,2=>4d/3,_=>21d/9};
        var viewWidth=Math.Min(width,height*ratio);var viewHeight=viewWidth/ratio;
        var view=new Viewport3D {Width=viewWidth,Height=viewHeight,ClipToBounds=true};
        var root=new Grid {Width=width,Height=height,Background=Brushes.Black};
        var canvas=new Grid {Width=viewWidth,Height=viewHeight,Background=new SolidColorBrush(Color.FromRgb(37,57,73)),ClipToBounds=true};canvas.Children.Add(view);root.Children.Add(canvas);
        view.Children.Add(new ModelVisual3D {Content=new AmbientLight(Colors.White)});
        var models=new List<SceneModel>();
        foreach(var pair in scene.Transforms)
        {
            var s=pair.Value;var path=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,s.SourcePath));
            var visual=LoadObj(path);var model=new SceneModel(pair.Key,path,visual,s.X,s.Y,s.Z,s.Scale,s.RotationY) {OutlineEnabled=s.OutlineEnabled,OutlineThickness=Math.Clamp(s.OutlineThickness,0,8)};models.Add(model);ApplyTransform(model);ConfigureModelOutline(model);view.Children.Add(visual);
        }
        if(scene.Camera is {} saved)view.Camera=CameraFromSaved(saved,ratio);
        else
        {
            var file=models.Select(m=>m.SourcePath).OrderBy(p=>p.Contains("\\bg\\")?0:1).Select(p=>Path.ChangeExtension(p,".scene.json")).FirstOrDefault(File.Exists);
            if(file is not null)
            {
                using var doc=JsonDocument.Parse(File.ReadAllText(file));var nodes=doc.RootElement.GetProperty("nodes").EnumerateArray().Where(n=>n.GetProperty("kind").GetString()=="View").ToArray();var node=nodes.FirstOrDefault(n=>n.GetProperty("name").GetString()=="standard_cam");if(node.ValueKind==JsonValueKind.Undefined)node=nodes.FirstOrDefault();
                if(node.ValueKind!=JsonValueKind.Undefined){var m=node.GetProperty("matrix").EnumerateArray().Select(x=>x.GetDouble()).ToArray();view.Camera=CameraFromSaved(new SavedCamera {Position=new[]{m[12]*.01,m[14]*.01,-m[13]*.01},Look=new[]{-m[8],-m[10],m[9]},VerticalFov=node.GetProperty("fov").GetDouble()},ratio);}
            }
            view.Camera??=new PerspectiveCamera(new Point3D(0,1.74,9.81),new Vector3D(0,-.15,-1),new Vector3D(0,1,0),45);
        }
        root.Measure(new Size(width,height));root.Arrange(new Rect(0,0,width,height));root.UpdateLayout();
        return new ExportView(root,view,models);
    }
    private static PerspectiveCamera CameraFromSaved(SavedCamera saved,double ratio)=>new(new Point3D(saved.Position[0],saved.Position[1],saved.Position[2]),new Vector3D(saved.Look[0],saved.Look[1],saved.Look[2]),new Vector3D(0,1,0),2*Math.Atan(Math.Tan(saved.VerticalFov*Math.PI/360)*ratio)*180/Math.PI);
    private void PoseExportView(ExportView render,SceneDocument scene,double time)
    {
        var chosenCamera=CameraAt(scene.CameraShots,scene.Camera,time);
        if(chosenCamera is not null)render.View.Camera=CameraFromSaved(chosenCamera,render.View.Width/render.View.Height);
        foreach(var model in render.Models)
        {
            if(scene.Keyframes.TryGetValue(model.Name,out var frames)&&frames.Count>0)InterpolateTransform(model,frames.OrderBy(k=>k.Time).ToArray(),time);
            ApplyPresetTransform(model,time,scene.Actions);ApplyTransform(model);
            if(!NativeMotions.TryGetValue(model.Visual,out var animation))continue;
            var action=scene.Actions.LastOrDefault(a=>a.Model==model.Name&&time>=a.Start&&time<a.Start+a.Duration);
            var source=action is {Preset: not ""}?BodyTime(action,time,animation):action is null?animation.IdleStart+PositiveModulo(time,Math.Max(.1,animation.IdleEnd-animation.IdleStart)):action.SourceStart+(action.Loop?PositiveModulo(action.SourceOffset+time-action.Start,Math.Max(.1,action.SourceDuration)):Math.Clamp(action.SourceOffset+time-action.Start,0,action.SourceDuration));
            var sound=scene.Sounds.LastOrDefault(s=>s.Model==model.Name&&time>=s.Start&&time<s.Start+s.Duration&&!string.IsNullOrEmpty(s.LipData));
            animation.Sample(source,sound is null||!_preferences.LipSync?0:MouthAt(sound.LipData,SoundTime(sound,time)),action?.Preset??"",action is {Preset: not ""}?PositiveModulo(action.SourceOffset+time-action.Start,Math.Max(.1,action.SourceDuration))/Math.Max(.1,action.SourceDuration):0);
        }
    }
    private async Task ExportMovieAsync(IReadOnlyList<SceneDocument> scenes,string destination,int width,int height,Action<double,string> progress,CancellationToken cancellation)
    {
        var ffmpeg=Path.Combine(AppContext.BaseDirectory,"Encoder","ffmpeg.exe");if(!File.Exists(ffmpeg))throw new FileNotFoundException("De meegeleverde MP4-encoder ontbreekt. Start vanuit de volledige Native Editor-map.",ffmpeg);
        const int fps=30;
        var frameCounts=scenes.Select(s=>(int)Math.Ceiling(SceneDuration(s)*fps)).ToArray();var totalFrames=frameCounts.Sum();var total=totalFrames/(double)fps;
        var temporary=Path.Combine(Path.GetDirectoryName(destination)!,"."+Path.GetFileNameWithoutExtension(destination)+"-"+Guid.NewGuid().ToString("N")+".mp4");
        var info=new ProcessStartInfo(ffmpeg){UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardError=true};
        void Args(params string[] values){foreach(var value in values)info.ArgumentList.Add(value);}
        Args("-hide_banner","-loglevel","error","-y","-f","rawvideo","-pixel_format","bgra","-video_size",$"{width}x{height}","-framerate","30","-i","pipe:0","-f","lavfi","-i","anullsrc=r=48000:cl=stereo");
        var filters=new List<string>();var mix=new List<string>{"[1:a]"};var input=2;double sceneOffset=0;
        foreach(var (scene,index) in scenes.Select((s,i)=>(s,i)))
        {
            foreach(var sound in scene.Sounds.Where(s=>s.Duration>.001&&s.Start<frameCounts[index]/(double)fps))
            {
                var path=ResolveAudioPath(sound.File);if(!File.Exists(path))throw new FileNotFoundException("Geluidsbestand ontbreekt",path);
                if(sound.Loop)Args("-stream_loop","-1");
                Args("-ss",Number(SoundTime(sound,sound.Start)),"-i",path);
                var duration=Math.Min(sound.Duration,frameCounts[index]/(double)fps-sound.Start);var delay=(long)Math.Round((sceneOffset+sound.Start)*48000);
                filters.Add($"[{input}:a]aresample=48000,atrim=duration={Number(duration)},asetpts=PTS-STARTPTS,adelay={delay}S:all=1[a{input}]");mix.Add($"[a{input}]");input++;
            }
            sceneOffset+=frameCounts[index]/(double)fps;
        }
        filters.Add(string.Join("",mix)+$"amix=inputs={mix.Count}:duration=longest:normalize=0,alimiter=limit=0.95:latency=1[aout]");
        Args("-filter_complex",string.Join(";",filters),"-map","0:v","-map","[aout]","-c:v","libx264","-preset","fast","-crf","18","-pix_fmt","yuv420p","-c:a","aac","-b:a","192k","-t",Number(total),"-movflags","+faststart",temporary);
        using var encoder=Process.Start(info)??throw new IOException("De MP4-encoder kon niet starten.");
        var errors=encoder.StandardError.ReadToEndAsync();
        using var cancelRegistration=cancellation.Register(()=>{try{if(!encoder.HasExited)encoder.Kill(true);}catch(InvalidOperationException){}});
        try
        {
            var buffer=new byte[width*height*4];var completed=0;
            foreach(var (scene,index) in scenes.Select((s,i)=>(s,i)))
            {
                cancellation.ThrowIfCancellationRequested();progress(completed/(double)totalFrames,$"Scène {index+1}/{scenes.Count} voorbereiden…");
                var render=BuildExportView(scene,width,height);
                try
                {
                    for(var frame=0;frame<frameCounts[index];frame++)
                    {
                        cancellation.ThrowIfCancellationRequested();PoseExportView(render,scene,frame/(double)fps);
                        var bitmap=new RenderTargetBitmap(width,height,96,96,PixelFormats.Pbgra32);bitmap.Render(render.Root);bitmap.CopyPixels(buffer,width*4,0);
                        await encoder.StandardInput.BaseStream.WriteAsync(buffer,cancellation);
                        completed++;
                        if(frame%3==0){progress(completed/(double)totalFrames,$"Scène {index+1}/{scenes.Count} · {completed/(double)fps:0.0} / {total:0.0} s ({completed*100/totalFrames}%)");await Dispatcher.Yield(DispatcherPriority.Background);}
                    }
                }
                finally{foreach(var model in render.Models)NativeMotions.Remove(model.Visual);render.View.Children.Clear();}
            }
            encoder.StandardInput.Close();progress(1,"MP4 afronden…");await encoder.WaitForExitAsync(cancellation);var error=await errors;
            if(encoder.ExitCode!=0)throw new IOException("Video-encoder: "+error);
            cancellation.ThrowIfCancellationRequested();File.Move(temporary,destination,true);
        }
        catch(Exception error) when(error is not OperationCanceledException&&cancellation.IsCancellationRequested){throw new OperationCanceledException(cancellation);}
        finally{try{if(!encoder.HasExited){encoder.Kill(true);await encoder.WaitForExitAsync();}}finally{if(File.Exists(temporary))File.Delete(temporary);}}
    }
    private sealed class SavedCamera
    {
        public double[] Position{get;set;}=[0,1.74,9.81];public double[] Look{get;set;}=[0,-.15,-1];public double VerticalFov{get;set;}=20;
    }
}




