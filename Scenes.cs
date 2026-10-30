using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Media3D;

namespace ToonTwisterNative;
public partial class MainWindow
{
    private readonly List<SceneDocument> _scenes=[new()];
    private int _sceneIndex;
    private SceneDocument CaptureScene() => new()
    {
        Objects=[.._objects],Tracks=[.._tracks],Aspect=AspectCombo.SelectedIndex,Duration=_sceneDuration,
        Camera=_cameraShots.Count>0?_baseCamera:CaptureCurrentCamera(),CameraShots=[.._cameraShots],
        Transforms=_models.ToDictionary(p=>p.Key,p=>new SavedTransform {SourcePath=Path.GetRelativePath(AppContext.BaseDirectory,p.Value.SourcePath),X=p.Value.X,Y=p.Value.Y,Z=p.Value.Z,Scale=p.Value.Scale,RotationY=p.Value.RotationY,OutlineEnabled=p.Value.OutlineEnabled,OutlineThickness=p.Value.OutlineThickness}),
        Keyframes=_keyframes.ToDictionary(p=>p.Key,p=>p.Value.Values.ToList()),Actions=[.._recordedActions],Sounds=[.._recordedSounds]
    };
    private void ApplyScene(SceneDocument data)
    {
        _timer.Stop();StopAudio();
        _sceneDuration=data.Duration;
        _cameraShots.Clear();_cameraShots.AddRange(data.CameraShots);_selectedCameraShot=null;_baseCamera=data.Camera;
        _timelineUndo.Clear();_timelineRedo.Clear();_scrubMode=false;_actionPreview=null;
        foreach(var model in _models.Values)SceneViewport.Children.Remove(model.Visual);
        if(_assetPreview is not null){SceneViewport.Children.Remove(_assetPreview);_assetPreview=null;}
        _models.Clear();_imported.Clear();_keyframes.Clear();_objects.Clear();_objects.AddRange(data.Objects);_tracks.Clear();_tracks.AddRange(data.Tracks);
        AspectCombo.SelectedIndex=Math.Clamp(data.Aspect,0,2);
        foreach(var pair in data.Transforms)
        {
            var s=pair.Value;var path=Path.IsPathRooted(s.SourcePath)?s.SourcePath:Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,s.SourcePath));
            if(!File.Exists(path))throw new FileNotFoundException("Een origineel asset ontbreekt",path);
            var visual=LoadObj(path);visual.SetValue(FrameworkElement.TagProperty,pair.Key);
            var model=new SceneModel(pair.Key,path,visual,s.X,s.Y,s.Z,s.Scale,s.RotationY) {OutlineEnabled=s.OutlineEnabled,OutlineThickness=Math.Clamp(s.OutlineThickness,0,8)};_models.Add(pair.Key,model);_imported.Add(visual);SceneViewport.Children.Add(visual);ApplyTransform(model);ConfigureModelOutline(model);
        }
        foreach(var pair in data.Keyframes)_keyframes[pair.Key]=new SortedDictionary<double,TransformKeyframe>(pair.Value.ToDictionary(k=>k.Time));
        _recordedActions.Clear();_recordedActions.AddRange(data.Actions);_recordedSounds.Clear();_recordedSounds.AddRange(data.Sounds);
        HydrateVoiceBindings();UpdateSceneDurationControls();_selectedTrack=null;
        RefreshMovieTracks();RefreshObjects();TimelineSlider.Value=0;
        var bg=_models.Values.FirstOrDefault(m=>m.SourcePath.Contains("\\bg\\"));if(bg is not null)UseOriginalCamera(bg.SourcePath);
        if(data.Camera is {} saved)SetEditorCamera(saved);
        ApplyCameraShotAt(0);
        if(SceneLabel is not null)SceneLabel.Text=$"Scène {_sceneIndex+1} / {_scenes.Count}";
    }
    private void RestoreMovie(SceneDocument data)
    {
        _scenes.Clear();_scenes.AddRange(data.Scenes is {Count:>0}?data.Scenes:[data]);
        _sceneIndex=Math.Clamp(data.CurrentScene,0,_scenes.Count-1);ApplyScene(_scenes[_sceneIndex]);
    }
    private void ChangeScene_Click(object sender,RoutedEventArgs e)
    {
        var direction=int.Parse(((Button)sender).Tag.ToString()!);var next=_sceneIndex+direction;if(next<0||next>=_scenes.Count)return;
        _scenes[_sceneIndex]=CaptureScene();_sceneIndex=next;ApplyScene(_scenes[_sceneIndex]);
    }
    private void AddScene_Click(object sender,RoutedEventArgs e)
    {
        if(_scenes.Count>=8){StatusText.Text="Een film bevat maximaal acht scènes, zoals het origineel.";return;}
        _scenes[_sceneIndex]=CaptureScene();_scenes.Add(new SceneDocument());_sceneIndex=_scenes.Count-1;ApplyScene(_scenes[_sceneIndex]);SetMode("Prep");
        StatusText.Text="Nieuwe scène: kies een set en voeg je acteurs toe.";
    }
}


