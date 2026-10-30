using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;

namespace ToonTwisterNative;
public partial class MainWindow
{
    private async void RenderOutlineCheck()
    {
        var output=Path.Combine(AppContext.BaseDirectory,"Verification","outline-preview.png");
        try
        {
            while(SceneViewport.Children.Count>1)SceneViewport.Children.RemoveAt(1);
            var root=Path.Combine(AppContext.BaseDirectory,"Assets","Converted-v3");
            var background=Path.Combine(root,"bg","bg_sponge_17_hi.obj");
            SceneViewport.Children.Add(LoadObj(background));UseOriginalCamera(background);
            var actorPath=Path.Combine(root,"w3d","act_spongebob_hi.obj");
            var actor=new SceneModel("SpongeBob",actorPath,LoadObj(actorPath));
            SceneViewport.Children.Add(actor.Visual);ConfigureModelOutline(actor);
            _cameraTarget=new Point3D(0,1,0);_cameraDistance=4;UpdateCamera();
            SceneViewport.UpdateLayout();await System.Threading.Tasks.Task.Delay(300);
            Directory.CreateDirectory(Path.GetDirectoryName(output)!);
            var bitmap=new RenderTargetBitmap(960,540,96,96,PixelFormats.Pbgra32);
            var drawing=new DrawingVisual();using(var context=drawing.RenderOpen())context.DrawRectangle(new VisualBrush(SceneViewport),null,new Rect(0,0,960,540));
            bitmap.Render(drawing);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using var stream=File.Create(output);encoder.Save(stream);
        }
        catch(Exception error){File.WriteAllText(Path.ChangeExtension(output,".txt"),error.ToString());}
        Application.Current.Shutdown();
    }
    private async void RenderOriginalGallery()
    {
        var root=Path.Combine(AppContext.BaseDirectory,"Assets","Converted-v3");
        var output=Path.Combine(AppContext.BaseDirectory,"Verification","CatalogThumbnails");Directory.CreateDirectory(output);
        var files=new[]{"bg","w3d"}.SelectMany(d=>Directory.EnumerateFiles(Path.Combine(root,d),"*.obj"))
            .Where(p=>Path.GetFileName(p).StartsWith("bg_")||Path.GetFileName(p).StartsWith("act_")||Path.GetFileName(p).StartsWith("3dp_"))
            .GroupBy(p=>System.Text.RegularExpressions.Regex.Replace(Path.GetFileNameWithoutExtension(p),"_(hi|me|lo)$",""))
            .Select(g=>g.OrderBy(p=>p.EndsWith("_hi.obj")?0:p.EndsWith("_me.obj")?1:2).First());
        var errors=new List<string>();
        foreach(var path in files)
        {
            try
            {
                while(SceneViewport.Children.Count>1)SceneViewport.Children.RemoveAt(1);
                var visual=LoadObj(path);SceneViewport.Children.Add(visual);UseOriginalCamera(path);
                SceneViewport.UpdateLayout();await System.Threading.Tasks.Task.Delay(70);
                var drawing=new DrawingVisual();using(var dc=drawing.RenderOpen())dc.DrawRectangle(new VisualBrush(SceneViewport),null,new Rect(0,0,160,90));
                var bitmap=new RenderTargetBitmap(160,90,96,96,PixelFormats.Pbgra32);bitmap.Render(drawing);
                var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using var file=File.Create(Path.Combine(output,Path.GetFileNameWithoutExtension(path)+".png"));encoder.Save(file);
                NativeMotions.Remove(visual);
            }
            catch(Exception error){errors.Add(path+": "+error.Message);}
        }
        File.WriteAllLines(Path.Combine(output,"errors.txt"),errors);Application.Current.Shutdown();
    }
    private async void RenderOriginalChecks()
    {
        var output=Path.Combine(AppContext.BaseDirectory,"Verification");Directory.CreateDirectory(output);
        try
        {
            while(SceneViewport.Children.Count>1)SceneViewport.Children.RemoveAt(1);
            var root=Path.Combine(AppContext.BaseDirectory,"Assets","Converted-v3");
            foreach(var texture in Directory.EnumerateFiles(Path.Combine(root,"bg","bg_sponge_17_hi_textures"),"*.jpg"))
            {
                var loaded=LoadOriginalTexture(texture);var enc=new PngBitmapEncoder();enc.Frames.Add(BitmapFrame.Create(loaded));using(var imageFile=File.Create(Path.Combine(output,Path.GetFileNameWithoutExtension(texture)+"-decoded.png")))enc.Save(imageFile);
                var bytes=File.ReadAllBytes(texture);
                for(var k=2;k+14<bytes.Length;k++)if(bytes[k-2]==255&&bytes[k-1]==217)
                {
                    var w=BitConverter.ToInt32(bytes,k);var h=BitConverter.ToInt32(bytes,k+4);var n=BitConverter.ToInt32(bytes,k+8);if(w!=loaded.PixelWidth||h!=loaded.PixelHeight||n<=0||k+12+n>bytes.Length)continue;
                    using var z=new System.IO.Compression.ZLibStream(new MemoryStream(bytes,k+12,n),System.IO.Compression.CompressionMode.Decompress);var a=new byte[w*h];z.ReadExactly(a);
                    var alpha=BitmapSource.Create(w,h,96,96,PixelFormats.Gray8,null,a,w);var alphaEncoder=new PngBitmapEncoder();alphaEncoder.Frames.Add(BitmapFrame.Create(alpha));using(var imageFile=File.Create(Path.Combine(output,Path.GetFileNameWithoutExtension(texture)+"-alpha.png")))alphaEncoder.Save(imageFile);break;
                }
            }
            var background=Path.Combine(root,"bg","bg_sponge_17_hi.obj");
            SceneViewport.Children.Add(LoadObj(background));UseOriginalCamera(background);
            foreach(var actor in new[]{"timmy","spongebob"})
            {
                var path=Path.Combine(root,"w3d",$"act_{actor}_hi.obj");
                var visual=LoadObj(path);SceneViewport.Children.Add(visual);
                foreach(var pose in new[]{"idle","jump"})
                {
                    if(NativeMotions.TryGetValue(visual,out var motion))
                    {var clip=ClipsFor(path).FirstOrDefault(c=>c.name==pose);motion.Sample(clip is null?motion.IdleStart:(clip.start+(clip.end-clip.start)*.45)/1000.0);}
                    SceneViewport.UpdateLayout();
                    await System.Threading.Tasks.Task.Delay(250);
                    var bitmap=new RenderTargetBitmap((int)Math.Max(1,SceneViewport.ActualWidth),(int)Math.Max(1,SceneViewport.ActualHeight),96,96,PixelFormats.Pbgra32);
                    var drawing=new DrawingVisual();using(var dc=drawing.RenderOpen())dc.DrawRectangle(new VisualBrush(SceneViewport),null,new Rect(0,0,bitmap.PixelWidth,bitmap.PixelHeight));
                    bitmap.Render(drawing);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using var file=File.Create(Path.Combine(output,$"{actor}-{pose}.png"));encoder.Save(file);
                    if(visual.Content is Model3DGroup g)File.AppendAllText(Path.Combine(output,"geometry.txt"),$"{actor}-{pose}: {g.Bounds}\n"+string.Join("\n",g.Children.OfType<GeometryModel3D>().Select(m=>$"{((DiffuseMaterial)m.Material).Brush.GetType().Name} {((MeshGeometry3D)m.Geometry).Positions.Count} vertices {((MeshGeometry3D)m.Geometry).TextureCoordinates.Count} UVs"))+"\n");
                }
                SceneViewport.Children.Remove(visual);
            }
            var actorPath=Path.Combine(root,"w3d","act_spongebob_hi.obj");var actorVisual=LoadObj(actorPath);
            var actorModel=new SceneModel("SpongeBob",actorPath,actorVisual,.25,0,0);_models.Add("SpongeBob",actorModel);_objects.Add("SpongeBob");SceneViewport.Children.Add(actorVisual);ApplyTransform(actorModel);RefreshObjects("SpongeBob");
            ActionCategory.SelectedIndex=2;RefreshActionChoices();ActionList.SelectedItem=ActionList.Items.OfType<ActionClip>().First(c=>c.name=="jump");TimelineSlider.Value=0;AddAction_Click(this,new RoutedEventArgs());
            if(_recordedActions.Count!=1)throw new Exception("Original action menu did not record Jump.");
            var audioFile=Directory.EnumerateFiles(Path.Combine(AppContext.BaseDirectory,"Assets","Audio"),"*.mp3").First();
            var probe=new MediaPlayer();var opened=new System.Threading.Tasks.TaskCompletionSource<double>();probe.MediaOpened+=(_,_)=>opened.TrySetResult(probe.NaturalDuration.HasTimeSpan?probe.NaturalDuration.TimeSpan.TotalSeconds:0);probe.MediaFailed+=(_,error)=>opened.TrySetException(error.ErrorException);probe.Open(new Uri(audioFile));
            if(await System.Threading.Tasks.Task.WhenAny(opened.Task,System.Threading.Tasks.Task.Delay(5000))!=opened.Task)throw new Exception("Native MP3 decoder did not open the original dialogue.");
            var seconds=await opened.Task;probe.Close();if(seconds<=0)throw new Exception("Original MP3 has no playable duration.");
            _recordedSounds.Add(new RecordedSound {Name="Original audio check",File=Path.GetFileName(audioFile),Start=0,Duration=seconds});
            _scenes.Clear();_scenes.Add(CaptureScene());_scenes.Add(new SceneDocument());_sceneIndex=0;
            _projectPath=Path.Combine(output,"roundtrip.ttnative");SaveProject_Click(this,new RoutedEventArgs());
            var restored=System.Text.Json.JsonSerializer.Deserialize<SceneDocument>(File.ReadAllText(_projectPath))!;RestoreMovie(restored);
            if(_scenes.Count!=2||_recordedActions.Count!=1||_recordedSounds.Count!=1||Math.Abs(_models["SpongeBob"].X-.25)>.001)throw new Exception("Movie save/load did not preserve scenes, actions, sound and placement.");
            await VerifyEditorUpgrade(output,root);
            await VerifyVideoEditing(output);
            File.WriteAllText(Path.Combine(output,"result.txt"),"PASS: original camera/UV and JPEG-alpha masks; actor idle/Jump; original mouth poses and dialogue timing; all face caches; MP3 decoding; non-destructive action insertion; timeline move/trim/undo/redo; resizable/floating panels; two-scene save/load including source offsets, voice binding and lipdata.");
        }
        catch(Exception error){File.WriteAllText(Path.Combine(output,"result.txt"),error.ToString());}
        Application.Current.Shutdown();
    }
}

