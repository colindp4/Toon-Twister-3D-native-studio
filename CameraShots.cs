using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace ToonTwisterNative;
public partial class MainWindow
{
    private sealed class CameraShot
    {
        public CameraShot() { }
        public string Name { get; set; } = "";
        public double Start { get; set; }
        public SavedCamera Camera { get; set; } = new();
    }

    private readonly List<CameraShot> _cameraShots = [];
    private CameraShot? _selectedCameraShot;
    private SavedCamera? _baseCamera;
    private Border? _dragCameraBorder;
    private double _dragCameraX, _dragCameraStart;

    private SavedCamera CaptureCurrentCamera()
    {
        var camera = (PerspectiveCamera)SceneViewport.Camera;
        return new SavedCamera
        {
            Position = [camera.Position.X, camera.Position.Y, camera.Position.Z],
            Look = [camera.LookDirection.X, camera.LookDirection.Y, camera.LookDirection.Z],
            VerticalFov = _originalVerticalFov
        };
    }

    private void SetEditorCamera(SavedCamera saved)
    {
        var look = new Vector3D(saved.Look[0], saved.Look[1], saved.Look[2]);
        _cameraDistance = Math.Max(.5, look.Length);
        _cameraTarget = new Point3D(saved.Position[0], saved.Position[1], saved.Position[2]) + look;
        look.Normalize();
        _cameraPitch = Math.Asin(Math.Clamp(-look.Y, -1, 1));
        _cameraYaw = Math.Atan2(-look.X, -look.Z);
        _originalVerticalFov = saved.VerticalFov;
        UpdateViewportAspect();
    }

    private static CameraShot? ShotAt(IEnumerable<CameraShot> shots, double time) =>
        shots.Where(s => s.Start <= time + .0001).OrderBy(s => s.Start).LastOrDefault();

    private static SavedCamera CameraAt(IReadOnlyList<CameraShot> shots, SavedCamera? fallback, double time)
    {
        var ordered=shots.OrderBy(s=>s.Start).ToArray();
        var next=Array.FindIndex(ordered,s=>s.Start>time+.0001);
        if(next<0)return ordered.Length==0?fallback??new SavedCamera():ordered[^1].Camera;
        var from=next==0?fallback??ordered[0].Camera:ordered[next-1].Camera;
        var to=ordered[next].Camera;
        var start=next==0?0:ordered[next-1].Start;var end=ordered[next].Start;
        var t=end<=start?1:Math.Clamp((time-start)/(end-start),0,1);t=t*t*(3-2*t);
        double Mix(double a,double b)=>a+(b-a)*t;
        var position=Enumerable.Range(0,3).Select(i=>Mix(from.Position[i],to.Position[i])).ToArray();
        var target=Enumerable.Range(0,3).Select(i=>Mix(from.Position[i]+from.Look[i],to.Position[i]+to.Look[i])).ToArray();
        return new SavedCamera {Position=position,Look=Enumerable.Range(0,3).Select(i=>target[i]-position[i]).ToArray(),VerticalFov=Mix(from.VerticalFov,to.VerticalFov)};
    }

    private void ApplyCameraShotAt(double time)
    {
        if(_cameraShots.Count>0||_baseCamera is not null)SetEditorCamera(CameraAt(_cameraShots,_baseCamera,time));
    }

    private void AddCameraShot_Click(object sender, RoutedEventArgs e)
    {
        PausePlayback();
        var time = Math.Round(TimelineSlider.Value, 2);
        if (_cameraShots.Count == 0) _baseCamera = CaptureCurrentCamera();
        PushTimelineUndo();
        var shot = _cameraShots.FirstOrDefault(s => Math.Abs(s.Start - time) < .01);
        if (shot is null)
        {
            shot = new CameraShot { Name = $"Shot {_cameraShots.Count + 1}", Start = time };
            _cameraShots.Add(shot);
        }
        shot.Camera = CaptureCurrentCamera();
        _selectedCameraShot = shot;
        UpdateSceneDurationControls();
        StatusText.Text = $"{shot.Name} op {time:0.00} s opgeslagen. Verplaats de camera en voeg op een later moment nog een shot toe.";
    }

    private void UpdateCameraShot_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedCameraShot is null || !_cameraShots.Contains(_selectedCameraShot))
        { StatusText.Text = "Klik eerst op een camerashot in de tijdlijn."; return; }
        PushTimelineUndo();
        _selectedCameraShot.Camera = CaptureCurrentCamera();
        StatusText.Text = $"Camerastand van {_selectedCameraShot.Name} bijgewerkt.";
    }

    private void DeleteCameraShot_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedCameraShot is null || !_cameraShots.Contains(_selectedCameraShot))
        { StatusText.Text = "Klik eerst op een camerashot in de tijdlijn."; return; }
        PushTimelineUndo();
        _cameraShots.Remove(_selectedCameraShot);
        _selectedCameraShot = null;
        UpdateSceneDurationControls();
        ApplyCameraShotAt(TimelineSlider.Value);
        StatusText.Text = "Camerashot verwijderd.";
    }

    private void RenderCameraShots()
    {
        foreach (var shot in _cameraShots.OrderBy(s => s.Start))
        {
            var border = new Border
            {
                Width = 20, Height = 20,
                Background = new SolidColorBrush(Color.FromRgb(70, 95, 190)),
                BorderBrush = ReferenceEquals(shot, _selectedCameraShot) ? Brushes.OrangeRed : Brushes.DarkSlateBlue,
                BorderThickness = new Thickness(2), CornerRadius = new CornerRadius(10), Tag = shot,
                Cursor = Cursors.SizeAll,
                ToolTip = $"{shot.Name} · {shot.Start:0.00} s. Sleep dit punt om het camerakeyframe te verplaatsen. Delete verwijdert het punt."
            };
            border.Child = new TextBlock { Text = "●", FontSize=12, Foreground = Brushes.White, HorizontalAlignment=HorizontalAlignment.Center, VerticalAlignment=VerticalAlignment.Center, IsHitTestVisible = false };
            Canvas.SetLeft(border, TrackLabelWidth + shot.Start * _pixelsPerSecond - 10);
            Canvas.SetTop(border, RulerHeight + (TrackHeight-20)/2);
            border.MouseLeftButtonDown += CameraShot_MouseDown;
            border.MouseMove += CameraShot_MouseMove;
            border.MouseLeftButtonUp += CameraShot_MouseUp;
            TimelineCanvas.Children.Add(border);
        }
    }

    private void CameraShot_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left) return;
        var border = (Border)sender;
        _selectedTrack=null;
        _selectedCameraShot = (CameraShot)border.Tag;
        _dragCameraBorder = border;
        _dragCameraX = e.GetPosition(TimelineCanvas).X;
        _dragCameraStart = _selectedCameraShot.Start;
        SeekTimeline(_selectedCameraShot.Start);
        border.BorderBrush = Brushes.OrangeRed;
        border.CaptureMouse();
        e.Handled = true;
    }

    private void CameraShot_MouseMove(object sender, MouseEventArgs e)
    {
        if (!ReferenceEquals(_dragCameraBorder, sender) || _selectedCameraShot is null) return;
        var proposed = Math.Clamp(SnapTime(_dragCameraStart + (e.GetPosition(TimelineCanvas).X - _dragCameraX) / _pixelsPerSecond), 0, TimelineSlider.Maximum - .1);
        Canvas.SetLeft((Border)sender, TrackLabelWidth + proposed * _pixelsPerSecond);
    }

    private void CameraShot_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (!ReferenceEquals(_dragCameraBorder, sender) || _selectedCameraShot is null) return;
        var proposed = Math.Clamp(SnapTime(_dragCameraStart + (e.GetPosition(TimelineCanvas).X - _dragCameraX) / _pixelsPerSecond), 0, TimelineSlider.Maximum - .1);
        if (Math.Abs(proposed - _dragCameraStart) > .001 && !_cameraShots.Any(s => !ReferenceEquals(s, _selectedCameraShot) && Math.Abs(s.Start - proposed) < .05))
        {
            PushTimelineUndo();
            _selectedCameraShot.Start = proposed;
            SeekTimeline(proposed);
        }
        ((Border)sender).ReleaseMouseCapture();
        _dragCameraBorder = null;
        RenderTimeline();
        e.Handled = true;
    }
}
