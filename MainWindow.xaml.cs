using Microsoft.Win32;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace ToonTwisterNative;

public partial class MainWindow : Window
{
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(33) };
    private readonly List<string> _objects = [];
    private readonly List<string> _tracks = ["Camera", "Character", "Audio"];
    private readonly List<ModelVisual3D> _imported = [];
    private readonly Dictionary<string, SceneModel> _models = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, SortedDictionary<double, TransformKeyframe>> _keyframes = new(StringComparer.OrdinalIgnoreCase);
    private List<AssetEntry> _assetEntries = [];
    private ModelVisual3D? _assetPreview;
    private SceneModel? _dragModel;
    private DragMode _dragMode;
    private Point _lastMouse;
    private bool _isDragging;
    private bool _updatingFields;
    private bool _updatingDurationControls;
    private bool _applyingKeyframe;
    private double _cameraDistance = 8;
    private double _cameraYaw;
    private double _cameraPitch = 0.06;
    private Point3D _cameraTarget = new(0, 0, 0);
    private DateTime _started;
    private string? _projectPath;
    private bool _libraryOpen;

    public MainWindow()
    {
        InitializeComponent();
        this.StartStudioLocalization();
        if(Environment.GetCommandLineArgs().Any(a=>a.StartsWith("--render-"))) { WindowStartupLocation=WindowStartupLocation.Manual;Left=-10000;Top=0;ShowInTaskbar=false; }
        _timer.Tick += (_, _) => UpdatePlayback();
        ObjectList.SelectedIndex = 2;
        Loaded += (_, _) => { ConfigureOriginalStudio(); UpdateViewportAspect(); UpdateCamera(); };
    }

    private void NewProject_Click(object sender, RoutedEventArgs e)
    {
        foreach (var model in _models.Values) SceneViewport.Children.Remove(model.Visual);
        if (_assetPreview is not null) SceneViewport.Children.Remove(_assetPreview);
        _models.Clear(); _imported.Clear(); _keyframes.Clear(); _assetPreview = null;
        _cameraShots.Clear();_selectedCameraShot=null;_baseCamera=null;
        _projectPath = null;
        _objects.Clear(); _recordedActions.Clear(); _recordedSounds.Clear(); StopAudio(); RefreshMovieTracks(); _timer.Stop();
        _timelineUndo.Clear();_timelineRedo.Clear();_scrubMode=false;_actionPreview=null;
        _sceneDuration=0;UpdateSceneDurationControls();_scenes.Clear();_scenes.Add(new SceneDocument());_sceneIndex=0;SceneLabel.Text="Scène 1 / 1";
        _tracks.Clear(); _tracks.AddRange(["Camera", "Character", "Audio"]);
        RefreshObjects();
        StatusText.Text = "Nieuw project gestart.";
    }

    private void OpenProject_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = "Toon Twister native project (*.ttnative)|*.ttnative|JSON project (*.json)|*.json" };
        if (dialog.ShowDialog(this) != true) return;
        LoadProject(dialog.FileName);
    }

    private void LoadProject(string path)
    {
        try
        {
            var data = JsonSerializer.Deserialize<SceneDocument>(File.ReadAllText(path)) ?? new();
            RestoreMovie(data);
            ReadImportedAudio();
            RefreshAudioChoices();
            _projectPath = path;
            RefreshObjects();
            StatusText.Text = $"Project geladen: {Path.GetFileName(_projectPath)}";
        }
        catch (Exception ex) { MessageBox.Show(this, StudioLocalization.Translate(ex.Message), StudioLocalization.Translate("Project kon niet worden geopend")); }
    }

    private void SaveProject_Click(object sender, RoutedEventArgs e)
    {
        if (_projectPath is null)
        {
            var dialog = new SaveFileDialog { Filter = "Toon Twister native project (*.ttnative)|*.ttnative", FileName = "Untitled Toon.ttnative" };
            if (dialog.ShowDialog(this) != true) return;
            _projectPath = dialog.FileName;
        }
        try
        {
            var data = CaptureScene();_scenes[_sceneIndex]=CaptureScene();data.Scenes=[.._scenes];data.CurrentScene=_sceneIndex;
            File.WriteAllText(_projectPath, JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true }));
            StatusText.Text = $"Opgeslagen: {Path.GetFileName(_projectPath)}";
        }
        catch (Exception ex) { MessageBox.Show(this, StudioLocalization.Translate(ex.Message), StudioLocalization.Translate("Project kon niet worden opgeslagen")); }
    }

    private void ObjectList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ObjectList.SelectedItem is string item)
        {
            SelectedObjectText.Text = item;
            StatusText.Text = $"Geselecteerd: {item}";
            Title = $"Toon Twister 3-D — {item}";
            var editable = _models.TryGetValue(item, out var model) && !model.SourcePath.Contains("\\bg\\");
            foreach (var box in new[] { XBox, YBox, ZBox, RotationBox, ScaleBox }) box.IsEnabled = editable;
            AddKeyframeButton.IsEnabled = editable;
            if (editable) { UpdateTransformFields(model!); UpdateKeyframeCount(); }
            _updatingFields = true;
            OutlineEnabled.IsChecked = model?.OutlineEnabled ?? false;
            OutlineWidthSlider.Value = model?.OutlineThickness ?? 0;
            OutlineWidthSlider.IsEnabled = model?.OutlineEnabled == true;
            _updatingFields = false;
            RefreshActionChoices();
        }
    }

    private void LibraryToggle_Click(object sender, RoutedEventArgs e)
    {
        _libraryOpen = true;
        SetMode("Prep");
        LoadAssetCategory();
    }

    private void AssetCategory_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (IsInitialized && _libraryOpen) { ClearAssetPreview(); LoadAssetCategory(); }
    }

    private void ClearAssetPreview()
    {
        if (_assetPreview is null) return;
        SceneViewport.Children.Remove(_assetPreview);
        _assetPreview = null;
    }

    private void LoadAssetCategory()
    {
        AssetList.Items.Clear();
        var root = Path.Combine(AppContext.BaseDirectory, "Assets");
        var category = AssetCategory.SelectedIndex;
        AddAssetButton.IsEnabled = category is >= 0 and <= 2;
        AddAssetButton.Content = AddAssetButton.IsEnabled ? "＋  Aan scène toevoegen" : "Alleen 3D-modellen toevoegen";
        var folder = category switch { 0 => "w3d", 1 => "bg", 2 => "w3d", _ => "" };
        IEnumerable<string> paths = folder.Length > 0
            ? Directory.Exists(Path.Combine(root, folder)) ? Directory.EnumerateFiles(Path.Combine(root, folder), "*.w3d").OrderBy(Path.GetFileName) : []
            : category == 3
                ? new[] { "Music.cxt", "sfx.cxt" }.Select(n => Path.Combine(root, n)).Where(File.Exists)
                : category == 4
                    ? new[] { "intro.mov", "logo_nick.mov", "logo_schol.mov", "logo_wf.mov" }.Select(n => Path.Combine(root, n)).Where(File.Exists)
                    : Directory.Exists(Path.Combine(root, "DirectorData"))
                        ? Directory.EnumerateFiles(Path.Combine(root, "DirectorData")).OrderBy(Path.GetFileName)
                        : [];
        paths = paths.Where(p => category != 0 || Path.GetFileName(p).StartsWith("act_"))
            .Where(p => category != 2 || Path.GetFileName(p).StartsWith("3dp_"))
            .GroupBy(p => System.Text.RegularExpressions.Regex.Replace(Path.GetFileNameWithoutExtension(p), "_(hi|me|lo)$", ""))
            .Select(g => g.OrderBy(p => p.EndsWith("_hi.w3d") ? 0 : p.EndsWith("_me.w3d") ? 1 : 2).First());
        _assetEntries = paths.Select(path => new AssetEntry(Path.GetRelativePath(root, path), path)).ToList();
        PopulateAssetList(selectFirst: category is >= 0 and <= 2);
        StatusText.Text = category switch
        {
            0 => "Kies een model. De viewport laat direct een voorbeeld zien.",
            1 => "Kies een achtergrond om hem direct in de viewport te bekijken.",
            2 => "Originele graphics-cast, meegeleverd als W3D-bestanden.",
            3 => "Director-audioarchieven zijn meegeleverd. Losse audiofragmenten moeten nog uit CXT worden gehaald.",
            4 => "Originele intro- en logovideo's zijn meegeleverd; afspelen hangt af van Windows-codecs.",
            _ => "Originele Director-movies en casts zijn meegeleverd. De timeline- en animatiedata is nog niet omgezet naar native keyframes."
        };
    }

    private void AssetSearch_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (IsInitialized && _libraryOpen) { ClearAssetPreview(); PopulateAssetList(selectFirst: true); }
    }

    private void PopulateAssetList(bool selectFirst)
    {
        var query = AssetSearch.Text.Trim();
        AssetList.Items.Clear();
        foreach (var asset in _assetEntries.Where(a => string.IsNullOrEmpty(query) || Path.GetFileName(a.RelativePath).Contains(query, StringComparison.OrdinalIgnoreCase) || a.ToString().Contains(query,StringComparison.OrdinalIgnoreCase)))
            AssetList.Items.Add(asset);
        if (AssetList.Items.Count > 0 && selectFirst) AssetList.SelectedIndex = 0;
        if (AssetList.Items.Count == 0) StatusText.Text = "Geen assets gevonden. Probeer een andere zoekterm.";
    }

    private void AssetList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ClearAssetPreview();
        if (AssetList.SelectedItem is not AssetEntry asset) return;
        AssetInfoText.Text = FriendlyAssetName(asset.RelativePath);
        var root = Path.Combine(AppContext.BaseDirectory, "Assets");
        var converted = Path.Combine(root, "Converted-v3", Path.ChangeExtension(asset.RelativePath, ".obj"));
        if (File.Exists(converted))
        {
            try
            {
                _assetPreview = LoadObj(converted);
                _assetPreview.SetValue(FrameworkElement.TagProperty, "Preview: " + asset.RelativePath);
                SceneViewport.Children.Add(_assetPreview);
                UseOriginalCamera(converted);
                StatusText.Text = $"Voorbeeld geladen: {Path.GetFileName(asset.RelativePath)}";
            }
            catch (Exception ex) { AssetInfoText.Text += $"\nVoorbeeldfout: {ex.Message}"; StatusText.Text = "Dit model kon niet worden getoond."; }
        }
        else
        {
            AssetInfoText.Text += "\nVoorbeeldconversie ontbreekt.";
            StatusText.Text = $"Geen native voorbeeld beschikbaar voor {Path.GetFileName(asset.RelativePath)}.";
        }
    }

    private void AddAsset_Click(object sender, RoutedEventArgs e)
    {
        if (AssetList.SelectedItem is not AssetEntry asset) { StatusText.Text = "Selecteer eerst een asset."; return; }
        var root = Path.Combine(AppContext.BaseDirectory, "Assets");
        var converted = Path.Combine(root, "Converted-v3", Path.ChangeExtension(asset.RelativePath, ".obj"));
        if (!File.Exists(converted)) { StatusText.Text = "Voor dit bestand is geen 3D-modelconversie beschikbaar."; return; }
        var item = FriendlyAssetName(asset.RelativePath);
        if (asset.RelativePath.StartsWith("bg"))
            foreach (var old in _models.Values.Where(m => m.SourcePath.Contains("\\bg\\")).ToArray())
            { SceneViewport.Children.Remove(old.Visual); _models.Remove(old.Name); _objects.Remove(old.Name); }
        if (_models.TryGetValue(item, out var alreadyAdded))
        {
            if (_assetPreview is not null) { SceneViewport.Children.Remove(_assetPreview); _assetPreview = null; }
            ObjectList.SelectedItem = item; FocusSelection_Click(sender, e); StatusText.Text = "Dit model staat al in de scène."; return;
        }
        if (_assetPreview is null || !File.Exists(converted)) _assetPreview = LoadObj(converted);
        if(!SceneViewport.Children.Contains(_assetPreview))SceneViewport.Children.Add(_assetPreview);
        _assetPreview.SetValue(FrameworkElement.TagProperty, item);
        if (!_imported.Contains(_assetPreview)) _imported.Add(_assetPreview);
        if (!_models.ContainsKey(item)) _models[item] = new SceneModel(item, converted, _assetPreview);
        if (!_objects.Contains(item)) _objects.Add(item);
        RefreshObjects(item);
        ConfigureModelOutline(_models[item]);
        AssetInfoText.Text = $"Toegevoegd: {Path.GetFileName(asset.RelativePath)}";
        StatusText.Text = $"Echt model toegevoegd aan de scène: {Path.GetFileName(asset.RelativePath)}";
        _assetPreview = null;
        RefreshActionChoices();
    }

    private void Preview_Click(object sender, RoutedEventArgs e)
    {
        if(_exporting)return;
        if (_timer.IsEnabled) {PausePlayback();return;}
        if (_models.Count == 0) { StatusText.Text = "Kies eerst een decor en voeg een personage toe."; return; }
        _playAllScenes=_mode=="Edit";
        _scrubMode=false;EndActionPreview();StopAudio();
        var time=TimelineSlider.Value;
        if(time>=SceneDuration(CaptureScene()))time=0;
        _started = DateTime.UtcNow-TimeSpan.FromSeconds(time);
        _timer.Start(); PreviewButton.Content = "Ⅱ Pauze"; PlayButton.Content = "Ⅱ Pauze";
    }

    private void PausePlayback(){_timer.Stop();_scrubMode=true;StopAudio();PreviewButton.Content="▶ Hervatten";PlayButton.Content="▶ Afspelen";}
    private static double SceneDuration(SceneDocument scene)=>scene.Duration>0?Math.Clamp(scene.Duration,1,300):Math.Min(300,Math.Max(3,new[]{scene.Actions.Select(a=>a.Start+a.Duration).DefaultIfEmpty(0).Max(),scene.Sounds.Select(a=>a.Start+a.Duration).DefaultIfEmpty(0).Max(),scene.Keyframes.Values.SelectMany(k=>k).Select(k=>k.Time).DefaultIfEmpty(0).Max(),scene.CameraShots.Select(s=>s.Start).DefaultIfEmpty(0).Max()}.Max()+.5));

    private void UpdatePlayback()
    {
        var duration=SceneDuration(CaptureScene());
        var elapsed = (DateTime.UtcNow - _started).TotalSeconds;
        if(elapsed>=duration)
        {
            if(_playAllScenes&&_sceneIndex+1<_scenes.Count){_scenes[_sceneIndex]=CaptureScene();_sceneIndex++;ApplyScene(_scenes[_sceneIndex]);_started=DateTime.UtcNow;_timer.Start();return;}
            _timer.Stop();StopAudio();PreviewButton.Content="▶ Film afspelen";PlayButton.Content="▶ Afspelen";TimelineSlider.Value=0;StatusText.Text="Afspelen voltooid.";return;
        }
        _applyingKeyframe = true; TimelineSlider.Value = elapsed; _applyingKeyframe = false;
        ApplyCameraShotAt(elapsed);
        TimeText.Text = $"  {elapsed:0.00} s / {duration:0.00} s";
        UpdateTimelinePlayhead();
        foreach (var pair in _models) if (_keyframes.TryGetValue(pair.Key, out var frames)) ApplyAnimatedFrame(pair.Value, frames, elapsed);
        SyncAudioPlayback(elapsed);
    }

    private void Aspect_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (IsInitialized) UpdateViewportAspect();
    }

    private void ViewportFrame_SizeChanged(object sender, SizeChangedEventArgs e) => UpdateViewportAspect();

    private void UpdateViewportAspect()
    {
        if (ViewportFrame is null || SceneViewport is null || AspectCombo is null) return;
        var desiredAspect = AspectCombo.SelectedIndex switch { 1 => 16.0 / 9.0, 2 => 4.0 / 3.0, _ => 21.0 / 9.0 };
        AspectLabel.Text = AspectCombo.SelectedIndex switch { 1 => "16:9", 2 => "4:3 klassiek", _ => "21:9 ultrawide" };
        var width = Math.Max(1, ViewportFrame.ActualWidth); var height = Math.Max(1, ViewportFrame.ActualHeight);
        if (width / height > desiredAspect) width = height * desiredAspect; else height = width / desiredAspect;
        SceneViewport.Width = width; SceneViewport.Height = height;
        if (SceneViewport.Camera is PerspectiveCamera camera)
        {
            var verticalFov = _originalVerticalFov;
            camera.FieldOfView = 2 * Math.Atan(Math.Tan(verticalFov * Math.PI / 360) * desiredAspect) * 180 / Math.PI;
        }
        UpdateCamera();
    }

    private void UpdateCamera()
    {
        if (SceneViewport?.Camera is not PerspectiveCamera camera) return;
        var cp = Math.Cos(_cameraPitch); var sp = Math.Sin(_cameraPitch);
        var offset = new Vector3D(_cameraDistance * Math.Sin(_cameraYaw) * cp, _cameraDistance * sp, _cameraDistance * Math.Cos(_cameraYaw) * cp);
        camera.Position = _cameraTarget + offset;
        camera.LookDirection = _cameraTarget - camera.Position;
        camera.UpDirection = new Vector3D(0, 1, 0);
    }

    private void Viewport_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (SceneViewport.Camera is not PerspectiveCamera) return;
        _lastMouse = e.GetPosition(SceneViewport); _isDragging = true; _dragModel = null;
        if (e.ChangedButton == System.Windows.Input.MouseButton.Left)
        {
            var hit = VisualTreeHelper.HitTest(SceneViewport, _lastMouse) as RayMeshGeometry3DHitTestResult;
            if (hit?.VisualHit is Visual3D visual)
            {
                Visual3D? target = visual;
                while (target is not null && !_models.Values.Any(m => ReferenceEquals(m.Visual, target)))
                    target = VisualTreeHelper.GetParent(target) as Visual3D;
                _dragModel = target is null ? null : _models.Values.FirstOrDefault(m => ReferenceEquals(m.Visual, target));
                if(_dragModel?.SourcePath.Contains("\\bg\\")==true)_dragModel=null;
                if (_dragModel is not null) ObjectList.SelectedItem = _dragModel.Name;
            }
        }
        _dragMode = e.ChangedButton switch
        {
            System.Windows.Input.MouseButton.Middle => DragMode.Pan,
            System.Windows.Input.MouseButton.Right => DragMode.Orbit,
            System.Windows.Input.MouseButton.Left when _dragModel is not null => DragMode.MoveObject,
            System.Windows.Input.MouseButton.Left => DragMode.Orbit,
            _ => DragMode.None
        };
        if (_dragMode == DragMode.MoveObject && _dragModel is not null) StatusText.Text = $"Geselecteerd: {_dragModel.Name}. Sleep om het te verplaatsen.";
        else if (_dragMode == DragMode.Orbit) StatusText.Text = "Camera slepen: draai het beeld.";
        else if (_dragMode == DragMode.Pan) StatusText.Text = "Camera slepen: verschuif het beeld.";
        ViewportFrame.CaptureMouse(); e.Handled = true;
    }

    private void Viewport_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (!_isDragging || _dragMode == DragMode.None) return;
        var point = e.GetPosition(SceneViewport); var dx = point.X - _lastMouse.X; var dy = point.Y - _lastMouse.Y;
        _lastMouse = point;
        if (_dragMode == DragMode.Orbit) { _cameraYaw -= dx * 0.008; _cameraPitch = Math.Clamp(_cameraPitch + dy * 0.008, -1.25, 1.25); UpdateCamera(); }
        else if (_dragMode == DragMode.Pan)
        {
            var units = WorldUnitsPerPixel();
            _cameraTarget = new Point3D(_cameraTarget.X - dx * units, _cameraTarget.Y + dy * units, _cameraTarget.Z);
            UpdateCamera();
        }
        else if (_dragMode == DragMode.MoveObject && _dragModel is not null)
        {
            var units = WorldUnitsPerPixel();
            if (System.Windows.Input.Keyboard.IsKeyDown(System.Windows.Input.Key.LeftShift) || System.Windows.Input.Keyboard.IsKeyDown(System.Windows.Input.Key.RightShift)) _dragModel.Z += dy * units;
            else { _dragModel.X += dx * units; _dragModel.Y -= dy * units; }
            ApplyTransform(_dragModel); UpdateTransformFields(_dragModel);
        }
    }

    private void Viewport_MouseUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.ChangedButton is System.Windows.Input.MouseButton.Left or System.Windows.Input.MouseButton.Right or System.Windows.Input.MouseButton.Middle)
        {
            if (_dragMode == DragMode.MoveObject && _dragModel is not null) StatusText.Text = $"Model verplaatst naar X {_dragModel.X:0.00}, Y {_dragModel.Y:0.00}, Z {_dragModel.Z:0.00}.";
            _isDragging = false; _dragMode = DragMode.None; _dragModel = null; ViewportFrame.ReleaseMouseCapture(); e.Handled = true;
        }
    }

    private void Viewport_MouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
    {
        _cameraDistance = Math.Clamp(_cameraDistance * (e.Delta > 0 ? 0.88 : 1.14), 0.15, 1000);
        UpdateCamera(); e.Handled = true;
    }

    private double WorldUnitsPerPixel()
    {
        if (SceneViewport.Camera is not PerspectiveCamera camera || SceneViewport.ActualHeight < 1) return 0.01;
        var aspect = Math.Max(0.1, SceneViewport.ActualWidth / SceneViewport.ActualHeight);
        var verticalFov = 2 * Math.Atan(Math.Tan(camera.FieldOfView * Math.PI / 360) / aspect);
        return 2 * _cameraDistance * Math.Tan(verticalFov / 2) / SceneViewport.ActualHeight;
    }

    private void ResetCamera_Click(object sender, RoutedEventArgs e)
    {
        var background=_models.Values.FirstOrDefault(m=>m.SourcePath.Contains("\\bg\\"));
        if(background is not null){UseOriginalCamera(background.SourcePath);return;}
        _cameraTarget = new Point3D(0, 0, 0); _cameraDistance = 8; _cameraYaw = 0; _cameraPitch = 0.06; UpdateCamera();
    }

    private void FocusSelection_Click(object sender, RoutedEventArgs e)
    {
        if (ObjectList.SelectedItem is not string selected || !_models.TryGetValue(selected, out var model)) return;
        var bounds=model.Visual.Transform.TransformBounds(model.Visual.Content.Bounds);
        _cameraTarget = new Point3D(bounds.X+bounds.SizeX/2,bounds.Y+bounds.SizeY/2,bounds.Z+bounds.SizeZ/2);
        var verticalFov = SceneViewport.Camera is PerspectiveCamera camera
            ? 2 * Math.Atan(Math.Tan(camera.FieldOfView * Math.PI / 360) / Math.Max(0.1, SceneViewport.ActualWidth / SceneViewport.ActualHeight))
            : Math.PI / 4;
        var horizontalFov=SceneViewport.Camera is PerspectiveCamera cam?cam.FieldOfView*Math.PI/180:Math.PI/4;
        _cameraDistance = Math.Clamp(Math.Max(bounds.SizeY/2/Math.Tan(verticalFov/2),bounds.SizeX/2/Math.Tan(horizontalFov/2))*1.3+bounds.SizeZ/2,.15,1000);
        UpdateCamera();
    }

    private void DuplicateObject_Click(object sender, RoutedEventArgs e)
    {
        if (ObjectList.SelectedItem is not string selected || !_models.TryGetValue(selected, out var model)) return;
        var copyName = $"{model.Name} (kopie)"; var suffix = 2;
        while (_models.ContainsKey(copyName)) copyName = $"{model.Name} (kopie {suffix++})";
        var visual = LoadObj(model.SourcePath);
        visual.SetValue(FrameworkElement.TagProperty, copyName);
        var duplicate = new SceneModel(copyName, model.SourcePath, visual, model.X + 0.5, model.Y, model.Z, model.Scale, model.RotationY) { OutlineEnabled=model.OutlineEnabled, OutlineThickness=model.OutlineThickness };
        _models.Add(copyName, duplicate); _objects.Add(copyName); _imported.Add(visual); SceneViewport.Children.Add(visual); ApplyTransform(duplicate); RefreshObjects(copyName);
        ConfigureModelOutline(duplicate);
        StatusText.Text = "Kopie gemaakt. Sleep het model om het te verplaatsen.";
    }

    private void DeleteObject_Click(object sender, RoutedEventArgs e)
    {
        if (ObjectList.SelectedItem is not string selected || !_models.Remove(selected, out var model)) return;
        SceneViewport.Children.Remove(model.Visual); _imported.Remove(model.Visual); _objects.Remove(selected); _keyframes.Remove(selected);
        _recordedActions.RemoveAll(a=>a.Model==selected);RefreshMovieTracks();
        RefreshObjects(); StatusText.Text = $"Model verwijderd: {selected}";
    }

    private void Transform_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Enter) { Transform_Changed(sender, e); System.Windows.Input.Keyboard.ClearFocus(); e.Handled = true; }
    }

    private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if(HandleTimelineKey(e)){e.Handled=true;return;}
        if(e.OriginalSource is TextBox or ComboBox)return;
        if (ObjectList.SelectedItem is not string selected || !_models.TryGetValue(selected, out var model)) return;
        if(model.SourcePath.Contains("\\bg\\"))return;
        const double step = 0.1;
        switch (e.Key)
        {
            case System.Windows.Input.Key.Left: model.X -= step; break;
            case System.Windows.Input.Key.Right: model.X += step; break;
            case System.Windows.Input.Key.Up when System.Windows.Input.Keyboard.Modifiers.HasFlag(System.Windows.Input.ModifierKeys.Shift): model.Z -= step; break;
            case System.Windows.Input.Key.Down when System.Windows.Input.Keyboard.Modifiers.HasFlag(System.Windows.Input.ModifierKeys.Shift): model.Z += step; break;
            case System.Windows.Input.Key.Up: model.Y += step; break;
            case System.Windows.Input.Key.Down: model.Y -= step; break;
            default: return;
        }
        ApplyTransform(model); UpdateTransformFields(model); e.Handled = true;
    }

    private void ApplyTransform(SceneModel model)
    {
        model.Visual.Transform = new Transform3DGroup
        {
            Children = new Transform3DCollection
            {
                new ScaleTransform3D(model.Scale, model.Scale, model.Scale),
                new RotateTransform3D(new AxisAngleRotation3D(new Vector3D(1,0,0),model.PresetTiltX)),
                new RotateTransform3D(new AxisAngleRotation3D(new Vector3D(0,0,1),model.PresetTiltZ)),
                new RotateTransform3D(new AxisAngleRotation3D(new Vector3D(0, 1, 0), model.RotationY)),
                new TranslateTransform3D(model.X, model.Y, model.Z)
            }
        };
    }

    private void UpdateTransformFields(SceneModel model)
    {
        _updatingFields = true;
        XBox.Text = model.X.ToString("0.00", CultureInfo.InvariantCulture); YBox.Text = model.Y.ToString("0.00", CultureInfo.InvariantCulture);
        ZBox.Text = model.Z.ToString("0.00", CultureInfo.InvariantCulture); RotationBox.Text = model.RotationY.ToString("0.0", CultureInfo.InvariantCulture);
        ScaleBox.Text = model.Scale.ToString("0.00", CultureInfo.InvariantCulture);
        _updatingFields = false;
    }

    private void Outline_Changed(object sender, RoutedEventArgs e)
    {
        if (_updatingFields || ObjectList.SelectedItem is not string name || !_models.TryGetValue(name, out var model)) return;
        model.OutlineEnabled = OutlineEnabled.IsChecked == true;
        model.OutlineThickness = OutlineWidthSlider.Value;
        OutlineWidthSlider.IsEnabled = model.OutlineEnabled;
        ConfigureModelOutline(model);
    }

    private void OutlineWidth_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (OutlineWidthSlider is null) return;
        if (OutlineWidthText is not null) OutlineWidthText.Text = $"Dikte {OutlineWidthSlider.Value:0.#}%";
        if (_updatingFields || ObjectList.SelectedItem is not string name || !_models.TryGetValue(name, out var model)) return;
        model.OutlineThickness = OutlineWidthSlider.Value;
        ConfigureModelOutline(model);
    }

    private void ConfigureModelOutline(SceneModel model)
    {
        if (!model.OutlineEnabled || model.OutlineThickness <= 0 || model.Visual.Content is not Model3DGroup sourceGroup)
        {
            model.Visual.Children.Remove(model.OutlineVisual);
            model.OutlineMeshes.Clear();
            return;
        }
        if (model.OutlineMeshes.Count == 0)
        {
            var outlineGroup = new Model3DGroup();
            model.OutlineVisual.Content = outlineGroup;
            foreach (var geometry in sourceGroup.Children.OfType<GeometryModel3D>())
            {
                if (geometry.Geometry is not MeshGeometry3D source || source.TriangleIndices.Count == 0) continue;
                var outline = new MeshGeometry3D { Positions = new Point3DCollection(source.Positions), TriangleIndices = new Int32Collection() };
                for (var i = 0; i + 2 < source.TriangleIndices.Count; i += 3)
                {
                    outline.TriangleIndices.Add(source.TriangleIndices[i]);
                    outline.TriangleIndices.Add(source.TriangleIndices[i + 2]);
                    outline.TriangleIndices.Add(source.TriangleIndices[i + 1]);
                }
                // The reversed hull must draw only its outward-facing back polygons.
                // WPF's Material is one-sided here; BackMaterial would paint the model's front black.
                outlineGroup.Children.Add(new GeometryModel3D(outline, new DiffuseMaterial(Brushes.Black)));
                model.OutlineMeshes.Add((source, outline));
            }
            model.Visual.Children.Add(model.OutlineVisual);
        }
        RefreshOutlineGeometry(model);
    }

    private static void RefreshOutlineGeometry(SceneModel model)
    {
        if (model.OutlineMeshes.Count == 0) return;
        var all = model.OutlineMeshes.SelectMany(pair => pair.Source.Positions).ToArray();
        if (all.Length == 0) return;
        var center = new Point3D((all.Min(p => p.X) + all.Max(p => p.X)) / 2, (all.Min(p => p.Y) + all.Max(p => p.Y)) / 2, (all.Min(p => p.Z) + all.Max(p => p.Z)) / 2);
        var factor = 1 + model.OutlineThickness / 100;
        foreach (var (source, outline) in model.OutlineMeshes)
        {
            var positions = new Point3DCollection(source.Positions.Count);
            foreach (var point in source.Positions) positions.Add(center + (point - center) * factor);
            outline.Positions = positions;
        }
    }

    private void AddKeyframe_Click(object sender, RoutedEventArgs e)
    {
        if (ObjectList.SelectedItem is not string selected || !_models.TryGetValue(selected, out var model))
        { StatusText.Text = "Voeg eerst een model toe en selecteer dat in de scène."; return; }
        PushTimelineUndo();
        if (!_keyframes.TryGetValue(selected, out var frames)) _keyframes[selected] = frames = new SortedDictionary<double, TransformKeyframe>();
        var time = Math.Round(TimelineSlider.Value, 2);
        frames[time] = new TransformKeyframe { Time = time, X = model.X, Y = model.Y, Z = model.Z, RotationY = model.RotationY, Scale = model.Scale };
        UpdateKeyframeCount(); StatusText.Text = $"Keyframe gezet op {time:0.00} s voor {model.Name}.";
        RefreshMovieTracks();
    }

    private void UpdateKeyframeCount()
    {
        if (ObjectList.SelectedItem is string selected && _keyframes.TryGetValue(selected, out var frames)) KeyframeCountText.Text = $"{frames.Count} keyframe(s)";
        else KeyframeCountText.Text = "Nog geen keyframes";
    }

    private void TimelineSlider_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!IsInitialized || _applyingKeyframe) return;
        _scrubMode=true;EndActionPreview();if(_timer.IsEnabled)_started=DateTime.UtcNow-TimeSpan.FromSeconds(TimelineSlider.Value);UpdateTimelinePlayhead();
        TimeText.Text = $"  {TimelineSlider.Value:0.00} s / {TimelineSlider.Maximum:0.00} s";
        ApplyCameraShotAt(TimelineSlider.Value);
        if (ObjectList.SelectedItem is string selected && _models.TryGetValue(selected, out var model) && _keyframes.TryGetValue(selected, out var frames) && frames.Count >= 2)
            ApplyAnimatedFrame(model, frames, TimelineSlider.Value);
    }

    private void ApplyAnimatedFrame(SceneModel model, SortedDictionary<double, TransformKeyframe> frames, double time)
    {
        if(frames.Count==0)return;
        InterpolateTransform(model,frames.Values.ToArray(),time);
        _applyingKeyframe = true; ApplyTransform(model); UpdateTransformFields(model); _applyingKeyframe = false;
    }
    private static void InterpolateTransform(SceneModel model,TransformKeyframe[] keys,double time)
    {
        var before = keys.LastOrDefault(k => k.Time <= time) ?? keys[0]; var after = keys.FirstOrDefault(k => k.Time >= time) ?? keys[^1];
        var amount = after.Time <= before.Time ? 0 : Math.Clamp((time - before.Time) / (after.Time - before.Time), 0, 1);
        double Mix(double a, double b) => a + (b - a) * amount;
        model.X = Mix(before.X, after.X); model.Y = Mix(before.Y, after.Y); model.Z = Mix(before.Z, after.Z);
        model.RotationY = Mix(before.RotationY, after.RotationY); model.Scale = Mix(before.Scale, after.Scale);
    }

    private void Transform_Changed(object sender, RoutedEventArgs e)
    {
        if (_updatingFields || ObjectList.SelectedItem is not string item || !_models.TryGetValue(item, out var model)) return;
        try
        {
            model.X = ReadNumber(XBox.Text); model.Y = ReadNumber(YBox.Text); model.Z = ReadNumber(ZBox.Text);
            model.RotationY = ReadNumber(RotationBox.Text); model.Scale = Math.Max(0.01, ReadNumber(ScaleBox.Text));
            ApplyTransform(model);
            if (!_applyingKeyframe) StatusText.Text = $"Transformatie bijgewerkt: {item}";
        }
        catch (FormatException) { StatusText.Text = "Vul numerieke waarden in."; }
    }

    private static double ReadNumber(string value) => double.Parse(value.Replace(',', '.'), CultureInfo.InvariantCulture);

    private void ImportModel_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = "Wavefront OBJ-model (*.obj)|*.obj|Alle bestanden (*.*)|*.*" };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            var visual = LoadObj(dialog.FileName);
            var name = "OBJ: " + Path.GetFileName(dialog.FileName);
            visual.SetValue(FrameworkElement.TagProperty, name);
            _imported.Add(visual); SceneViewport.Children.Add(visual); _objects.Add(name); _models[name] = new SceneModel(name, dialog.FileName, visual); ApplyTransform(_models[name]); RefreshObjects(name); ConfigureModelOutline(_models[name]);
            StatusText.Text = $"Model geïmporteerd: {Path.GetFileName(dialog.FileName)}";
        }
        catch (Exception ex) { MessageBox.Show(this, StudioLocalization.Translate(ex.Message), StudioLocalization.Translate("OBJ-import mislukt")); }
    }

    private static ModelVisual3D LoadObj(string path)
    {
        var original = path.Contains("Converted-v3", StringComparison.OrdinalIgnoreCase);
        var animated = File.Exists(Path.ChangeExtension(path, ".motion"));
        var nativeMeshes = new List<(MeshGeometry3D Mesh, int[] Indices)>();
        var vertices = new List<Point3D>();
        var normals = new List<Vector3D>();
        var uvs = new List<Point>();
        var faces = new List<ObjFace>();
        var activeMaterial = "default";
        foreach (var raw in File.ReadLines(path))
        {
            var line = raw.Trim();
            if (line.StartsWith("v ", StringComparison.Ordinal))
            {
                var p = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
                if (p.Length >= 4) vertices.Add(new Point3D(ReadNumber(p[1]), ReadNumber(p[2]), ReadNumber(p[3])));
            }
            else if (line.StartsWith("vn ", StringComparison.Ordinal))
            {
                var p = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
                if (p.Length >= 4) normals.Add(new Vector3D(ReadNumber(p[1]), ReadNumber(p[2]), ReadNumber(p[3])));
            }
            else if (line.StartsWith("vt ", StringComparison.Ordinal))
            {
                var p = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
                if (p.Length >= 3) uvs.Add(new Point(ReadNumber(p[1]), original ? ReadNumber(p[2]) : 1 - ReadNumber(p[2])));
            }
            else if (line.StartsWith("usemtl ", StringComparison.Ordinal)) activeMaterial = line[7..].Trim();
            else if (line.StartsWith("f ", StringComparison.Ordinal))
            {
                var tokens = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Skip(1).ToArray();
                var corners = tokens.Select(token =>
                {
                    var indices = token.Split('/');
                    int Resolve(string value, int count) { var index = int.Parse(value, CultureInfo.InvariantCulture); return index < 0 ? count + index : index - 1; }
                    return (
                        Vertex: Resolve(indices[0], vertices.Count),
                        Uv: indices.Length > 1 && indices[1].Length > 0 ? Resolve(indices[1], uvs.Count) : -1,
                        Normal: indices.Length > 2 && indices[2].Length > 0 ? Resolve(indices[2], normals.Count) : -1);
                }).ToArray();
                for (var i = 1; i + 1 < corners.Length; i++)
                {
                    var tri = new[] { corners[0], corners[i], corners[i + 1] };
                    if (tri.Any(c => c.Vertex < 0 || c.Vertex >= vertices.Count)) throw new InvalidDataException("OBJ bevat een ongeldige vertexindex.");
                    faces.Add(new ObjFace(tri, activeMaterial));
                }
            }
        }
        if (faces.Count == 0) throw new InvalidDataException("Dit OBJ-bestand bevat geen driehoeken.");
        var minX = vertices.Min(p => p.X); var maxX = vertices.Max(p => p.X);
        var minY = vertices.Min(p => p.Y); var maxY = vertices.Max(p => p.Y);
        var minZ = vertices.Min(p => p.Z); var maxZ = vertices.Max(p => p.Z);
        var center = new Point3D((minX + maxX) / 2, (minY + maxY) / 2, (minZ + maxZ) / 2);
        var extent = Math.Max(maxX - minX, Math.Max(maxY - minY, maxZ - minZ));
        var fit = original ? 0.01 : extent > 0.0001 ? 3.2 / extent : 1;
        if (original) center = new Point3D();
        Point3D Fit(Point3D p) => new((p.X - center.X) * fit, (p.Y - center.Y) * fit, (p.Z - center.Z) * fit);
        var mtl = ReadMaterials(path);
        var group = new Model3DGroup();
        foreach (var batch in faces.GroupBy(f => f.Material).OrderBy(batch=>
        {
            var materialInfo=mtl.GetValueOrDefault(batch.Key);if(materialInfo?.TexturePath is null||!File.Exists(materialInfo.TexturePath))return 0;
            var texture=LoadOriginalTexture(materialInfo.TexturePath);return texture.Format==PixelFormats.Bgra32||texture.Format==PixelFormats.Pbgra32?1:0;
        }))
        {
            var mesh = new MeshGeometry3D();
            foreach (var face in batch)
            {
                var points = face.Corners.Select(c => Fit(vertices[c.Vertex])).ToArray();
                var faceNormal = Vector3D.CrossProduct(points[1] - points[0], points[2] - points[0]);
                if (faceNormal.Length > 0.00001) faceNormal.Normalize(); else faceNormal = new Vector3D(0, 0, 1);
                for (var corner = 0; corner < 3; corner++)
                {
                    var sourceNormal = face.Corners[corner].Normal;
                    var normal = sourceNormal >= 0 && sourceNormal < normals.Count ? normals[sourceNormal] : faceNormal;
                    if (normal.Length > 0.00001) normal.Normalize(); else normal = faceNormal;
                    mesh.Positions.Add(points[corner]); mesh.Normals.Add(normal); mesh.TriangleIndices.Add(mesh.Positions.Count - 1);
                    var uvIndex = face.Corners[corner].Uv;
                    mesh.TextureCoordinates.Add(uvIndex >= 0 && uvIndex < uvs.Count ? uvs[uvIndex] : new Point());
                }
            }
            if(original && !path.Contains("\\bg\\"))
                for(var i=0;i<mesh.TriangleIndices.Count;i+=3){var index=mesh.TriangleIndices[i+1];mesh.TriangleIndices[i+1]=mesh.TriangleIndices[i+2];mesh.TriangleIndices[i+2]=index;}
            if (!animated) mesh.Freeze();
            else nativeMeshes.Add((mesh, batch.SelectMany(f => f.Corners.Select(c => c.Vertex)).ToArray()));
            var info = mtl.GetValueOrDefault(batch.Key) ?? new MtlInfo();
            Brush brush = Brushes.LightSkyBlue;
            if (info.TexturePath is not null && File.Exists(info.TexturePath))
            {
                try
                {
                    var bitmap = LoadOriginalTexture(info.TexturePath);
                    brush = new ImageBrush(bitmap) { Stretch = Stretch.Fill, ViewportUnits=BrushMappingMode.Absolute, Viewport=new Rect(0,0,1,1), TileMode=TileMode.Tile };
                }
                catch { }
            }
            else brush = new SolidColorBrush(info.Color);
            brush.Opacity=info.Opacity;
            Material material = new DiffuseMaterial(brush);
            group.Children.Add(new GeometryModel3D(mesh, material) { BackMaterial = original && !path.Contains("\\bg\\") ? null : material });
        }
        var result = new ModelVisual3D { Content = group };
        if (animated) NativeMotions.Add(result, new NativeMotion(path, nativeMeshes));
        return result;
    }

    private static Dictionary<string, MtlInfo> ReadMaterials(string objPath)
    {
        var result = new Dictionary<string, MtlInfo>(StringComparer.OrdinalIgnoreCase);
        var mtlLine = File.ReadLines(objPath).FirstOrDefault(l => l.StartsWith("mtllib ", StringComparison.Ordinal));
        if (mtlLine is null) return result;
        var mtlPath = Path.Combine(Path.GetDirectoryName(objPath)!, mtlLine[7..].Trim());
        if (!File.Exists(mtlPath)) return result;
        MtlInfo? current = null;
        foreach (var raw in File.ReadLines(mtlPath))
        {
            var line = raw.Trim();
            if (line.StartsWith("newmtl ", StringComparison.Ordinal)) { current = new MtlInfo(); result[line[7..].Trim()] = current; }
            else if (current is not null && line.StartsWith("Kd ", StringComparison.Ordinal))
            {
                var p = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
                if (p.Length >= 4) current.Color = Color.FromRgb(ToByte(ReadNumber(p[1])), ToByte(ReadNumber(p[2])), ToByte(ReadNumber(p[3])));
            }
            else if (current is not null && line.StartsWith("d ",StringComparison.Ordinal)) current.Opacity=Math.Clamp(ReadNumber(line[2..]),0,1);
            else if (current is not null && line.StartsWith("map_Kd ", StringComparison.Ordinal)) current.TexturePath = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(mtlPath)!, line[7..].Trim()));
        }
        return result;
        static byte ToByte(double value) => (byte)Math.Clamp((int)Math.Round(value * 255), 0, 255);
    }

    private sealed record ObjFace((int Vertex, int Uv, int Normal)[] Corners, string Material);
    private sealed class MtlInfo { public Color Color { get; set; } = Color.FromRgb(145, 185, 202); public string? TexturePath { get; set; } public double Opacity{get;set;}=1; }

    private void RefreshObjects(string? select = null)
    {
        ObjectList.Items.Clear();
        foreach (var item in _objects) ObjectList.Items.Add(item);
        if (select is not null) ObjectList.SelectedItem = select;
        else if (ObjectList.Items.Count > 0) ObjectList.SelectedIndex = Math.Min(2, ObjectList.Items.Count - 1);
    }

    private sealed class SceneDocument
    {
        public SceneDocument() { }
        public List<string> Objects { get; set; } = [];
        public List<string> Tracks { get; set; } = ["Camera", "Character", "Audio"];
        public int Aspect { get; set; }
        public Dictionary<string, SavedTransform> Transforms { get; set; } = [];
        public Dictionary<string, List<TransformKeyframe>> Keyframes { get; set; } = [];
        public List<RecordedAction> Actions { get; set; } = [];
        public List<RecordedSound> Sounds { get; set; } = [];
        public List<SceneDocument>? Scenes { get; set; }
        public int CurrentScene { get; set; }
        public double Duration {get;set;}
        public SavedCamera? Camera {get;set;}
        public List<CameraShot> CameraShots {get;set;}=[];
    }

    private sealed class SavedTransform
    {
        public SavedTransform() { }
        public string SourcePath { get; set; } = "";
        public bool OutlineEnabled { get; set; } = true;
        public double OutlineThickness { get; set; } = 2.5;
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }
        public double RotationY { get; set; }
        public double PresetTiltX {get;set;}
        public double PresetTiltZ {get;set;}
        public double Scale { get; set; } = 1;
    }

    private sealed class TransformKeyframe
    {
        public TransformKeyframe() { }
        public double Time { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }
        public double RotationY { get; set; }
        public double Scale { get; set; } = 1;
    }

    private sealed class SceneModel(string name, string sourcePath, ModelVisual3D visual, double x = 0, double y = 0, double z = 0, double scale = 1, double rotationY = 0)
    {
        public string Name { get; } = name;
        public string SourcePath { get; } = sourcePath;
        public ModelVisual3D Visual { get; } = visual;
        public double X { get; set; } = x;
        public double Y { get; set; } = y;
        public double Z { get; set; } = z;
        public double Scale { get; set; } = scale;
        public double RotationY { get; set; } = rotationY;
        public bool OutlineEnabled { get; set; } = true;
        public double OutlineThickness { get; set; } = 2.5;
        public ModelVisual3D OutlineVisual { get; } = new();
        public List<(MeshGeometry3D Source, MeshGeometry3D Outline)> OutlineMeshes { get; } = [];
        public double PresetTiltX {get;set;}
        public double PresetTiltZ {get;set;}
    }

    private enum DragMode { None, Orbit, Pan, MoveObject }

    private sealed record AssetEntry(string RelativePath, string FullPath)
    {
        public string Thumbnail => Path.Combine(AppContext.BaseDirectory,"Assets","CatalogThumbnails",Path.GetFileNameWithoutExtension(FullPath)+".png");
        public override string ToString() => FriendlyAssetName(RelativePath);
    }
}



