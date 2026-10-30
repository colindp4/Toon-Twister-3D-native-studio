using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Windows.Threading;

namespace ToonTwisterNative;

public partial class MainWindow
{
    private static readonly ConditionalWeakTable<ModelVisual3D, NativeMotion> NativeMotions = new();
    private readonly Stopwatch _motionClock = Stopwatch.StartNew();
    private readonly DispatcherTimer _motionTimer = new() { Interval = TimeSpan.FromMilliseconds(33) };
    private readonly List<RecordedAction> _recordedActions = [];
    private double _originalVerticalFov = 20;
    private string _mode = "Prep";
    private bool _playAllScenes;
    private static List<List<ActionClip>>? _catalog;
    private static readonly string[] ActorOrder = ["angelica","carl","chuckie","cindy","cosmo","fluffy","goddard","jimmy","kimi","libby","mrkrabs","patrick","plankton","spongebob","squidward","tommy","timmy","vicky","wanda","gary"];

    private void ConfigureOriginalStudio()
    {
        LoadStudioPreferences();RestoreEditorLayout();Closed+=(_,_)=>SaveEditorLayout();
        _cameraTarget = new Point3D(0.106481, 1.74, 0);
        _cameraDistance = 9.81; _cameraPitch = 0.19;
        TimelineSlider.Maximum = 45;
        _motionTimer.Tick += (_, _) =>
        {
            var t = _motionClock.Elapsed.TotalSeconds;
            foreach (var model in _models.Values)
                if (NativeMotions.TryGetValue(model.Visual, out var animation))
                {
                    if(_timer.IsEnabled||_scrubMode)ApplyPresetTransform(model,TimelineSlider.Value,_recordedActions);
                    var active = _timer.IsEnabled||_scrubMode ? _recordedActions.LastOrDefault(a => a.Model == model.Name && TimelineSlider.Value >= a.Start && TimelineSlider.Value < a.Start + a.Duration) : null;
                    var recorded=active is {Preset:""}?active:null;
                    var range=recorded is null?1:Math.Max(.1,recorded.SourceDuration);var delta=recorded is null?0:recorded.SourceOffset+TimelineSlider.Value-recorded.Start;
                    var source=active is {Preset: not ""}?BodyTime(active,TimelineSlider.Value,animation):recorded is null?animation.IdleStart+(_scrubMode?TimelineSlider.Value:t)%Math.Max(.1,animation.IdleEnd-animation.IdleStart):recorded.SourceStart+(recorded.Loop?(delta%range+range)%range:Math.Min(recorded.SourceDuration,delta));
                    if(!_timer.IsEnabled&&!_scrubMode&&_actionPreview is { } previewAction&&previewAction.Model==model.Name)
                    {var clip=previewAction.Clip;if(!string.IsNullOrEmpty(clip.preset)&&_actionPreviewBase is {} previewBase)ApplyPresetTransform(model,(DateTime.UtcNow-previewAction.Started).TotalSeconds,new[]{new RecordedAction {Model=model.Name,Preset=clip.preset,PresetBase=previewBase,Start=0,Duration=45,SourceDuration=(clip.end-clip.start)/1000d,Loop=true}});var elapsed=(DateTime.UtcNow-previewAction.Started).TotalSeconds;var duration=Math.Max(.1,(clip.end-clip.start)/1000.0);source=string.IsNullOrEmpty(clip.preset)?clip.start/1000.0+(clip.loop==1?elapsed%duration:Math.Min(elapsed,duration)):PreviewBodyTime(model.SourcePath,clip.preset,elapsed,animation);}
                    var dance=active?.Preset??(_actionPreview is {} p&&p.Model==model.Name?p.Clip.preset:"");
                    var dancePhase=active is {Preset: not ""}?PositiveModulo(active.SourceOffset+TimelineSlider.Value-active.Start,Math.Max(.1,active.SourceDuration))/Math.Max(.1,active.SourceDuration):_actionPreview is {} previewPose&&previewPose.Model==model.Name?PositiveModulo((DateTime.UtcNow-previewPose.Started).TotalSeconds,Math.Max(.1,(previewPose.Clip.end-previewPose.Clip.start)/1000d))/Math.Max(.1,(previewPose.Clip.end-previewPose.Clip.start)/1000d):0;
                    animation.Sample(source,VisemeFor(model),dance,dancePhase);
                    RefreshOutlineGeometry(model);
                }
            if (_assetPreview is not null && NativeMotions.TryGetValue(_assetPreview,out var preview)) preview.Sample(preview.IdleStart+t%Math.Max(.1,preview.IdleEnd-preview.IdleStart));
        };
        _motionTimer.Start();
        Closed += (_,_) => { _motionTimer.Stop(); _timer.Stop(); StopAudio();foreach(var pending in _pendingSoundOpens)pending.Close();_pendingSoundOpens.Clear(); };
        SetMode("Prep");
        _libraryOpen = false; LibraryToggle_Click(this,new RoutedEventArgs());
        AssetCategory.SelectedIndex = 1;
        StatusText.Text = "1. Kies een set. 2. Plaats acteurs en props. 3. Ga naar Direct voor echte personageacties.";
        if (Environment.GetCommandLineArgs().Contains("--render-check"))
        { _motionTimer.Stop(); Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(RenderOriginalChecks)); }
        if (Environment.GetCommandLineArgs().Contains("--render-outline"))
        { _motionTimer.Stop(); Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(RenderOutlineCheck)); }
        if (Environment.GetCommandLineArgs().Contains("--render-gallery"))
        { _motionTimer.Stop(); Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(RenderOriginalGallery)); }
    }

    private void StudioMode_Click(object sender,RoutedEventArgs e) => SetMode((sender as Button)?.Tag?.ToString() ?? "Prep");
    private void SetMode(string mode)
    {
        // Library selections are viewport-only previews. Only the explicit add button
        // promotes them into the document, so leaving Prep must discard the preview.
        if (mode != "Prep" && _assetPreview is not null)
        {
            SceneViewport.Children.Remove(_assetPreview);
            _assetPreview = null;
        }
        _mode=mode;
        LibraryPane.Visibility=mode=="Prep"?Visibility.Visible:Visibility.Collapsed;
        DirectPane.Visibility=mode=="Direct"?Visibility.Visible:Visibility.Collapsed;
        EditorPane.Visibility=mode=="Edit"?Visibility.Visible:Visibility.Collapsed;
        ModeHint.Text=mode switch {"Prep"=>"Kies je set, acteurs en props", "Direct"=>"Kies een acteur en geef hem een actie", _=>"Bekijk je film en sla je project op"};
        RefreshActionChoices();
        RenderTimeline();
    }
    private void Category_Click(object sender,RoutedEventArgs e)
    {
        if (!_libraryOpen) LibraryToggle_Click(this,e);
        AssetSearch.Clear();
        AssetCategory.SelectedIndex=int.Parse(((Button)sender).Tag.ToString()!);
        LoadAssetCategory();
    }
    private static List<ActionClip> ClipsFor(string path)
    {
        if (_catalog is null)
        {
            var file=Path.Combine(AppContext.BaseDirectory,"Assets","Converted-v3","actions.json");
            _catalog=File.Exists(file)?JsonSerializer.Deserialize<List<List<ActionClip>>>(File.ReadAllText(file))??[]:[];
        }
        var key=Regex.Replace(Path.GetFileNameWithoutExtension(path),"_(hi|me|lo)$","").Replace("act_","");
        var index=Array.IndexOf(ActorOrder,key);
        return index>=0 && index<_catalog.Count?_catalog[index]:[];
    }
    private void RefreshActionChoices()
    {
        if (ActionList is null || ActionCategory is null) return;
        ActionList.Items.Clear();
        if (ObjectList.SelectedItem is not string selected || !_models.TryGetValue(selected,out var model)) return;
        var category=ActionCategory.SelectedIndex switch {0=>"Walks",1=>"Moods",3=>"Studio",_=>"Actions"};
        foreach(var clip in AvailableClips(model.SourcePath).Where(c=>c.cat==category)) ActionList.Items.Add(clip);
        if(ActionList.Items.Count>0)ActionList.SelectedIndex=0;
        RefreshAudioChoices();
    }
    private void ActionCategory_Changed(object sender,SelectionChangedEventArgs e) { if(IsInitialized) RefreshActionChoices(); }
    private void ActionChoice_Changed(object sender,SelectionChangedEventArgs e)
    {EndActionPreview();if(ActionDurationBox is not null && ActionList.SelectedItem is ActionClip clip){ActionDurationBox.Text=(clip.loop==1?3:Math.Max(.1,(clip.end-clip.start)/1000.0)).ToString("0.00",System.Globalization.CultureInfo.InvariantCulture);if(_mode=="Direct"&&!_timer.IsEnabled&&ObjectList.SelectedItem is string model){_scrubMode=false;if(_models.TryGetValue(model,out var selectedModel))_actionPreviewBase=new SavedTransform {X=selectedModel.X,Y=selectedModel.Y,Z=selectedModel.Z,RotationY=selectedModel.RotationY,Scale=selectedModel.Scale};_actionPreview=(model,clip,DateTime.UtcNow);}}}
    private void AddAction_Click(object sender,RoutedEventArgs e)
    {
        if(ObjectList.SelectedItem is not string selected || !_models.TryGetValue(selected,out var model) || ActionList.SelectedItem is not ActionClip clip)
        {StatusText.Text="Selecteer links eerst een acteur en kies daarna een actie.";return;}
        InsertActionAt(selected,clip,TimelineSlider.Value);
    }
    private void InsertActionAt(string selected,ActionClip clip,double requestedStart)
    {
        EndActionPreview();if(!_models.TryGetValue(selected,out var model))return;
        if(!NativeMotions.TryGetValue(model.Visual,out var motion)) {StatusText.Text="Dit object heeft geen geconverteerde skeletanimatie.";return;}
        var sourceDuration=Math.Max(.1,(clip.end-clip.start)/1000.0);
        var duration=clip.loop==1 && double.TryParse(ActionDurationBox.Text.Replace(',','.'),System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out var requested)?Math.Clamp(requested,.1,TimelineSlider.Maximum):sourceDuration;
        var start=FreeActionStart(selected,requestedStart,duration);
        if(start<0) {StatusText.Text="Er is onvoldoende vrije ruimte op dit spoor voor deze actie.";return;}
        _timer.Stop();StopAudio();
        PushTimelineUndo();
        _recordedActions.Add(new RecordedAction {Model=selected,Name=clip.desc,Start=start,Duration=duration,SourceStart=clip.start/1000.0,SourceDuration=sourceDuration,Loop=clip.loop==1,BodyStart=BodyFor(model.SourcePath,clip.preset)?.start/1000d??0,BodyDuration=BodyFor(model.SourcePath,clip.preset) is {} body?(body.end-body.start)/1000d:0,BodySpeed=BodySpeedFor(clip.preset),Preset=clip.preset,PresetBase=new SavedTransform {X=model.X,Y=model.Y,Z=model.Z,RotationY=model.RotationY,Scale=model.Scale}});
        motion.Sample(clip.start/1000.0);
        SeekTimeline(start);
        RefreshMovieTracks();
        StatusText.Text=$"{clip.desc} toegevoegd op {start:0.0} s. Klik Afspelen om je scène te bekijken.";
    }
    private void RefreshMovieTracks()
    {
        MovieTracks.Items.Clear();
        foreach(var action in _recordedActions.OrderBy(a=>a.Start))MovieTracks.Items.Add(new TrackDisplay($"{FriendlyAssetName(action.Model)}  ·  {action.Name}  ·  {action.Start:0.0}–{action.Start+action.Duration:0.0} s",action));
        foreach(var sound in _recordedSounds.OrderBy(a=>a.Start))MovieTracks.Items.Add(new TrackDisplay($"♫ {sound.Name}  ·  {sound.Start:0.0}–{sound.Start+sound.Duration:0.0} s",Sound:sound));
        if(_selectedTrack is not null)MovieTracks.SelectedItem=MovieTracks.Items.OfType<TrackDisplay>().FirstOrDefault(t=>ReferenceEquals(t.Action,_selectedTrack.Action)&&ReferenceEquals(t.Sound,_selectedTrack.Sound));
        RenderTimeline();
    }
    private void RemoveAction_Click(object sender,RoutedEventArgs e)
    {
        if(MovieTracks.SelectedItem is TrackDisplay track){PushTimelineUndo();if(track.Action is not null)_recordedActions.Remove(track.Action);if(track.Sound is not null)_recordedSounds.Remove(track.Sound);}
        _selectedTrack=null;
        RefreshMovieTracks();
    }
    private void UseOriginalCamera(string path)
    {
        if(!path.Contains("\\bg\\") && _models.Values.Any(m=>m.SourcePath.Contains("\\bg\\")))return;
        var file=Path.ChangeExtension(path,".scene.json");if(!File.Exists(file))return;
        using var doc=JsonDocument.Parse(File.ReadAllText(file));
        var cameras=doc.RootElement.GetProperty("nodes").EnumerateArray().Where(n=>n.GetProperty("kind").GetString()=="View").ToArray();
        if(cameras.Length==0)return;
        var node=cameras.FirstOrDefault(n=>n.GetProperty("name").GetString()=="standard_cam");if(node.ValueKind==JsonValueKind.Undefined)node=cameras[0];
        var m=node.GetProperty("matrix").EnumerateArray().Select(x=>x.GetDouble()).ToArray();
        var position=new Point3D(m[12]*.01,m[14]*.01,-m[13]*.01);
        var look=new Vector3D(-m[8],-m[10],m[9]);look.Normalize();
        _cameraDistance=Math.Max(.5,position.Z/Math.Max(.1,-look.Z));
        _cameraTarget=position+look*_cameraDistance;
        _cameraPitch=Math.Asin(Math.Clamp(-look.Y,-1,1));_cameraYaw=Math.Atan2(-look.X,-look.Z);
        var horizontal=node.GetProperty("fov").GetDouble();
        _originalVerticalFov=horizontal;
        UpdateViewportAspect();UpdateCamera();
    }
    private static string FriendlyAssetName(string path)
    {
        var name=Regex.Replace(Path.GetFileNameWithoutExtension(path.Replace("W3D: ","")),"_(hi|me|lo)$","");
        var known=new Dictionary<string,string>{{"spongebob","SpongeBob"},{"mrkrabs","Mr. Krabs"},{"squidward","Squidward"},{"patrick","Patrick"},{"timmy","Timmy"},{"jimmy","Jimmy Neutron"},{"goddard","Goddard"}};
        if(name.StartsWith("act_"))return known.GetValueOrDefault(name[4..])??System.Globalization.CultureInfo.InvariantCulture.TextInfo.ToTitleCase(name[4..]);
        return name.Replace("bg_sponge_","Bikini Bottom · ").Replace("bg_fop_","Fairly OddParents · ").Replace("bg_jimmy_","Jimmy Neutron · ").Replace("bg_rugrats_","Rugrats · ").Replace("3dp_","").Replace('_',' ');
    }
    private sealed class ActionClip {public string name{get;set;}="";public int start{get;set;}public int end{get;set;}public int loop{get;set;}public string cat{get;set;}="";public string desc{get;set;}="";public string preset{get;set;}="";public override string ToString()=>StudioLocalization.IsEnglish?StudioLocalization.Translate(desc):desc;}
    private sealed class RecordedAction {public string Model{get;set;}="";public string Name{get;set;}="";public double Start{get;set;}public double Duration{get;set;}public double SourceStart{get;set;}public double SourceDuration{get;set;}public double SourceOffset{get;set;}public bool Loop{get;set;}public string Preset{get;set;}="";public SavedTransform? PresetBase{get;set;}public double BodyStart{get;set;}public double BodyDuration{get;set;}public double BodySpeed{get;set;}=1;}
    private sealed class NativeMotion
    {
        private readonly byte[] _data;
        private readonly int _count,_frames;
        private readonly float _fps;
        private readonly List<(MeshGeometry3D Mesh,int[] Indices)> _meshes;
        private readonly NativeFace? _face;
        private readonly NativeFace? _flossRig;
        private readonly bool _flossArms;
        private int _lastFrame=-1;
        public double IdleStart{get;}
        public double IdleEnd{get;}
        public bool FlossArms=>_flossArms;
        public NativeMotion(string path,List<(MeshGeometry3D Mesh,int[] Indices)> meshes)
        {
            _data=File.ReadAllBytes(Path.ChangeExtension(path,".motion"));
            if(_data.Length<16 || System.Text.Encoding.ASCII.GetString(_data,0,4)!="TTM1")throw new InvalidDataException("Ongeldige native motion-cache.");
            _count=BitConverter.ToInt32(_data,4);_frames=BitConverter.ToInt32(_data,8);_fps=BitConverter.ToSingle(_data,12);_meshes=meshes;
            _flossArms=Path.GetFileName(path).Contains("spongebob",StringComparison.OrdinalIgnoreCase);
            if(16L+(long)_count*_frames*12!=_data.LongLength)throw new InvalidDataException("Onvolledige native motion-cache.");
            if(File.Exists(Path.ChangeExtension(path,".face")))_face=new NativeFace(Path.ChangeExtension(path,".face"),_count);
            if(File.Exists(Path.ChangeExtension(path,".flossrig")))_flossRig=new NativeFace(Path.ChangeExtension(path,".flossrig"),_count);
            var idle=ClipsFor(path).FirstOrDefault(c=>c.name=="idle")??ClipsFor(path).FirstOrDefault(c=>c.name=="neutral_cycle");
            IdleStart=(idle?.start??0)/1000.0;IdleEnd=idle is null?Math.Min(2,(_frames-1)/_fps):idle.end/1000.0;
            Sample(IdleStart);
        }
        public void Sample(double seconds,int viseme=0,string preset="",double phase=0)
        {
            var tick=HashCode.Combine((int)(seconds*60),Math.Clamp(viseme,0,8),preset,(int)(phase*60));if(tick==_lastFrame)return;_lastFrame=tick;
            var mouth=preset=="floss"&&_flossRig is not null?_flossRig.Pose(seconds,viseme,phase):viseme>0?_face?.Pose(seconds,viseme):null;
            var framePosition=Math.Clamp(seconds*_fps,0,_frames-1);var frame=(int)framePosition;var next=Math.Min(frame+1,_frames-1);var amount=framePosition-frame;
            var floats=MemoryMarshal.Cast<byte,float>(_data.AsSpan(16+frame*_count*12,_count*12));
            var after=MemoryMarshal.Cast<byte,float>(_data.AsSpan(16+next*_count*12,_count*12));
            foreach(var (mesh,indices) in _meshes)
            {
                var points=new Point3D[indices.Length];for(var i=0;i<indices.Length;i++){Point3D point;if(mouth is not null&&mouth.TryGetValue(indices[i],out var posed))point=posed;else{var v=indices[i]*3;if(v+2>=floats.Length)throw new InvalidDataException("Mesh en animatie hebben verschillende vertexindices.");point=new Point3D((floats[v]+(after[v]-floats[v])*amount)*.01,(floats[v+1]+(after[v+1]-floats[v+1])*amount)*.01,(floats[v+2]+(after[v+2]-floats[v+2])*amount)*.01);}
                    points[i]=point;}
                mesh.Positions=new Point3DCollection(points);
            }
        }
    }
}





