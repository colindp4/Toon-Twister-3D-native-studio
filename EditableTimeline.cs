using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace ToonTwisterNative;
public partial class MainWindow
{
    private const double TrackLabelWidth=170,TrackHeight=40,RulerHeight=27;
    private double _pixelsPerSecond=28;
    private bool _scrubMode,_seeking;
    private bool _razorMode;
    private (string Model,ActionClip Clip,DateTime Started)? _actionPreview;
    private Point _libraryDragStart;
    private readonly Stack<string> _timelineUndo=[],_timelineRedo=[];
    private TrackDisplay? _selectedTrack,_dragTrack;
    private Border? _dragBorder;
    private Point _clipDragOrigin;
    private double _clipOldStart,_clipOldDuration,_clipOldOffset;
    private string _clipDragKind="move";
    private Line? _playhead;
    private List<string> _timelineLanes=[];
    private sealed record LibraryClip(string Model,ActionClip? Action=null,AudioEntry? Sound=null);
    private sealed class TimelineState {public List<RecordedAction> Actions{get;set;}=[];public List<RecordedSound> Sounds{get;set;}=[];public List<CameraShot> CameraShots{get;set;}=[];public Dictionary<string,List<TransformKeyframe>> Keys{get;set;}=[];}

    private string CaptureTimeline()=>JsonSerializer.Serialize(new TimelineState {Actions=[.._recordedActions],Sounds=[.._recordedSounds],CameraShots=[.._cameraShots],Keys=_keyframes.ToDictionary(p=>p.Key,p=>p.Value.Values.ToList())});
    private void PushTimelineUndo(){_timelineUndo.Push(CaptureTimeline());_timelineRedo.Clear();}
    private void RestoreTimeline(string state)
    {
        _timer.Stop();StopAudio();_scrubMode=true;var data=JsonSerializer.Deserialize<TimelineState>(state)!;_recordedActions.Clear();_recordedActions.AddRange(data.Actions);_recordedSounds.Clear();_recordedSounds.AddRange(data.Sounds);_cameraShots.Clear();_cameraShots.AddRange(data.CameraShots);_selectedCameraShot=null;_keyframes.Clear();foreach(var pair in data.Keys)_keyframes[pair.Key]=new SortedDictionary<double,TransformKeyframe>(pair.Value.ToDictionary(k=>k.Time));_selectedTrack=null;_selectedKey=null;RefreshMovieTracks();UpdateKeyframeCount();ApplyCameraShotAt(TimelineSlider.Value);
    }
    private void UndoTimeline_Click(object sender,RoutedEventArgs e){if(_timelineUndo.Count==0)return;_timelineRedo.Push(CaptureTimeline());RestoreTimeline(_timelineUndo.Pop());}
    private void RedoTimeline_Click(object sender,RoutedEventArgs e){if(_timelineRedo.Count==0)return;_timelineUndo.Push(CaptureTimeline());RestoreTimeline(_timelineRedo.Pop());}
    private double SnapTime(double value)=>SnapTimeline?.IsChecked==true&&!Keyboard.Modifiers.HasFlag(ModifierKeys.Alt)?Math.Round(value*10)/10:Math.Round(value,3);
    private double TimeAt(double x)=>Math.Clamp(SnapTime((x-TrackLabelWidth)/_pixelsPerSecond),0,TimelineSlider.Maximum);
    private void TimelineZoom_Changed(object sender,RoutedPropertyChangedEventArgs<double> e){if(IsInitialized)RenderTimeline();}
    private void Timeline_SizeChanged(object sender,SizeChangedEventArgs e){if(IsInitialized)RenderTimeline();}
    private static string Lane(TrackDisplay t)=>t.Action is not null?t.Action.Model+"|act":!string.IsNullOrEmpty(t.Sound?.Model)?t.Sound.Model+"|voice":t.Sound?.Category=="music"?"|music":"|sfx";
    private static double ClipStart(TrackDisplay t)=>t.Action?.Start??t.Sound?.Start??0;
    private static double ClipDuration(TrackDisplay t)=>t.Action?.Duration??t.Sound?.Duration??0;
    private void SelectTrack(TrackDisplay track)
    {
        _selectedCameraShot=null;
        _selectedTrack=track;
        MovieTracks.SelectedItem=MovieTracks.Items.OfType<TrackDisplay>().FirstOrDefault(t=>ReferenceEquals(t.Action,track.Action)&&ReferenceEquals(t.Sound,track.Sound));
        if(track.Action is not null)ObjectList.SelectedItem=track.Action.Model;
        else if(!string.IsNullOrEmpty(track.Sound?.Model))ObjectList.SelectedItem=track.Sound.Model;
    }
    private void RenderTimeline()
    {
        if(TimelineCanvas is null||TimelineScroll is null)return;
        _pixelsPerSecond=Math.Max(12,(TimelineScroll.ActualWidth-TrackLabelWidth-25)/TimelineSlider.Maximum)*Math.Max(.5,TimelineZoom?.Value??1);
        var width=TrackLabelWidth+TimelineSlider.Maximum*_pixelsPerSecond+8;
        var tracks=MovieTracks.Items.OfType<TrackDisplay>().ToArray();
        _timelineLanes=["|camera"];
        foreach(var m in _models.Values.Where(m=>!m.SourcePath.Contains("\\bg\\"))){_timelineLanes.Add(m.Name+"|act");if(NativeMotions.TryGetValue(m.Visual,out _))_timelineLanes.Add(m.Name+"|voice");}
        foreach(var t in tracks)if(!_timelineLanes.Contains(Lane(t)))_timelineLanes.Add(Lane(t));
        if(_timelineLanes.Count==0)_timelineLanes.Add("|act");
        foreach(var lane in new[]{"|music","|sfx"})if(!_timelineLanes.Contains(lane))_timelineLanes.Add(lane);
        TimelineCanvas.Children.Clear();TimelineCanvas.Width=width;TimelineCanvas.Height=RulerHeight+_timelineLanes.Count*TrackHeight;
        for(var row=0;row<_timelineLanes.Count;row++)
        {
            var fill=new Border {Width=width,Height=TrackHeight-1,Background=row%2==0?new SolidColorBrush(Color.FromRgb(239,247,219)):Brushes.Honeydew};Canvas.SetTop(fill,RulerHeight+row*TrackHeight);TimelineCanvas.Children.Add(fill);
            var lane=_timelineLanes[row];var parts=lane.Split('|');var name=parts[1] switch {"camera"=>"🎥 Camera","voice"=>parts[0]+" · Stem","music"=>"♫ Muziek","sfx"=>"✦ Effecten",_=>string.IsNullOrEmpty(parts[0])?"Kies een acteur":parts[0]+" · Acties"};
            var label=new TextBlock {Text=name,Width=TrackLabelWidth-12,TextTrimming=TextTrimming.CharacterEllipsis,FontWeight=FontWeights.SemiBold,FontSize=11,Margin=new Thickness(6,0,0,0)};Canvas.SetTop(label,RulerHeight+row*TrackHeight+11);TimelineCanvas.Children.Add(label);
        }
        for(var second=0;second<=TimelineSlider.Maximum;second++)
        {
            var x=TrackLabelWidth+second*_pixelsPerSecond;
            var tick=new Line {X1=x,X2=x,Y1=second%5==0?8:18,Y2=TimelineCanvas.Height,Stroke=second%5==0?Brushes.DarkSeaGreen:Brushes.LightGray,StrokeThickness=.5,IsHitTestVisible=false};TimelineCanvas.Children.Add(tick);
            if(second%5==0||_pixelsPerSecond>=35){var label=new TextBlock {Text=second+" s",FontSize=10,IsHitTestVisible=false};Canvas.SetLeft(label,x+3);TimelineCanvas.Children.Add(label);}
        }
        foreach(var track in tracks)
        {
            var border=new Border {Width=Math.Max(8,ClipDuration(track)*_pixelsPerSecond),Height=29,Background=track.Sound is null?Brushes.MediumSeaGreen:track.Sound.Category=="music"?Brushes.SteelBlue:Brushes.Goldenrod,BorderBrush=_selectedTrack is not null&&ReferenceEquals(_selectedTrack.Action,track.Action)&&ReferenceEquals(_selectedTrack.Sound,track.Sound)?Brushes.DarkRed:Brushes.DarkGreen,BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(4),Tag=track,Cursor=Cursors.SizeAll,ToolTip=$"{track.Text}\nSleep om te verplaatsen. Sleep een rand om bij te snijden of een herhaling te verlengen."};
            var clipBody=new Grid {IsHitTestVisible=false};clipBody.Children.Add(new TextBlock {Text=(track.Action is {Loop:true}||track.Sound is {Loop:true}?"↻ ":"")+(track.Action?.Name??track.Sound?.Name),Margin=new Thickness(8,6,8,0),FontSize=11,Foreground=Brushes.White,TextTrimming=TextTrimming.CharacterEllipsis});
            foreach(var side in new[]{HorizontalAlignment.Left,HorizontalAlignment.Right})clipBody.Children.Add(new Border {Width=3,Height=15,Background=Brushes.White,Opacity=.65,HorizontalAlignment=side,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(3,0,3,0)});border.Child=clipBody;
            Canvas.SetLeft(border,TrackLabelWidth+ClipStart(track)*_pixelsPerSecond);Canvas.SetTop(border,RulerHeight+_timelineLanes.IndexOf(Lane(track))*TrackHeight+5);
            border.MouseLeftButtonDown+=Clip_MouseDown;border.MouseMove+=Clip_MouseMove;border.MouseLeftButtonUp+=Clip_MouseUp;
            var menu=new ContextMenu();var remove=new MenuItem {Header="Fragment verwijderen"};remove.Click+=(_,_)=>{SelectTrack(track);RemoveAction_Click(this,new RoutedEventArgs());};menu.Items.Add(remove);border.ContextMenu=menu;TimelineCanvas.Children.Add(border);
        }
        RenderCameraShots();
        if(_sceneDuration>0){var endX=TrackLabelWidth+_sceneDuration*_pixelsPerSecond;var shade=new Border {Width=Math.Max(0,TimelineCanvas.Width-endX),Height=TimelineCanvas.Height,Background=new SolidColorBrush(Color.FromArgb(70,35,40,48)),IsHitTestVisible=false};Canvas.SetLeft(shade,endX);TimelineCanvas.Children.Add(shade);TimelineCanvas.Children.Add(new Line {X1=endX,X2=endX,Y1=0,Y2=TimelineCanvas.Height,Stroke=Brushes.DarkOrange,StrokeThickness=3,IsHitTestVisible=false});}
        RenderPositionKeys();
        _playhead=new Line {Y1=0,Y2=TimelineCanvas.Height,Stroke=Brushes.Crimson,StrokeThickness=2,IsHitTestVisible=false};TimelineCanvas.Children.Add(_playhead);UpdateTimelinePlayhead();
    }
    private void UpdateTimelinePlayhead(){if(_playhead is null)return;var x=TrackLabelWidth+TimelineSlider.Value*_pixelsPerSecond;_playhead.X1=_playhead.X2=x;if(_timer.IsEnabled&&TimelineScroll is not null){if(x>TimelineScroll.HorizontalOffset+TimelineScroll.ViewportWidth-40)TimelineScroll.ScrollToHorizontalOffset(x-TimelineScroll.ViewportWidth*.7);else if(x<TimelineScroll.HorizontalOffset+20)TimelineScroll.ScrollToHorizontalOffset(Math.Max(0,x-TrackLabelWidth));}}
    private void SeekTimeline(double time)
    {
        _scrubMode=true;EndActionPreview();TimelineSlider.Value=Math.Clamp(time,0,TimelineSlider.Maximum);UpdateTimelinePlayhead();
    }
    private void Timeline_MouseDown(object sender,MouseButtonEventArgs e)
    {if(e.ChangedButton!=MouseButton.Left)return;_seeking=true;TimelineCanvas.CaptureMouse();SeekTimeline(TimeAt(e.GetPosition(TimelineCanvas).X));e.Handled=true;}
    private void Timeline_MouseMove(object sender,MouseEventArgs e){if(_seeking)SeekTimeline(TimeAt(e.GetPosition(TimelineCanvas).X));}
    private void Timeline_MouseUp(object sender,MouseButtonEventArgs e){if(!_seeking)return;_seeking=false;TimelineCanvas.ReleaseMouseCapture();e.Handled=true;}
    private void Clip_MouseDown(object sender,MouseButtonEventArgs e)
    {
        var border=(Border)sender;var track=(TrackDisplay)border.Tag;SelectTrack(track);
        if(_razorMode){SplitTrack(track,TimeAt(e.GetPosition(TimelineCanvas).X));e.Handled=true;return;}
        _dragTrack=track;_dragBorder=border;_clipDragOrigin=e.GetPosition(TimelineCanvas);
        _clipOldStart=ClipStart(track);_clipOldDuration=ClipDuration(track);_clipOldOffset=track.Action?.SourceOffset??track.Sound?.SourceOffset??0;
        var x=e.GetPosition(border).X;var edge=Math.Min(10,border.ActualWidth/3);_clipDragKind=x<=edge?"left":x>=border.ActualWidth-edge?"right":"move";
        _timer.Stop();StopAudio();_scrubMode=true;border.CaptureMouse();e.Handled=true;
    }
    private (double Start,double Duration,double Offset) DragProposal(double x)
    {
        var delta=SnapTime((x-_clipDragOrigin.X)/_pixelsPerSecond);var start=_clipOldStart;var duration=_clipOldDuration;var offset=_clipOldOffset;
        if(_clipDragKind=="move")start=Math.Clamp(SnapTime(start+delta),0,TimelineSlider.Maximum-duration);
        else if(_clipDragKind=="right")
        {
            duration=Math.Clamp(SnapTime(duration+delta),.1,Math.Max(.1,TimelineSlider.Maximum-start));
        }
        else
        {
            var min=_dragTrack?.Action is {Loop:true}||_dragTrack?.Sound is {Loop:true}?-_clipOldStart:-Math.Min(_clipOldStart,offset);
            delta=Math.Clamp(delta,min,duration-.1);start+=delta;duration-=delta;offset+=delta;
        }
        return(start,duration,offset);
    }
    private void Clip_MouseMove(object sender,MouseEventArgs e)
    {
        var border=(Border)sender;
        if(_dragTrack is null||!ReferenceEquals(border,_dragBorder))
        {var x=e.GetPosition(border).X;var edge=Math.Min(10,border.ActualWidth/3);border.Cursor=_razorMode?Cursors.Cross:x<=edge||x>=border.ActualWidth-edge?Cursors.SizeWE:Cursors.SizeAll;return;}
        var proposal=DragProposal(e.GetPosition(TimelineCanvas).X);Canvas.SetLeft(border,TrackLabelWidth+proposal.Start*_pixelsPerSecond);border.Width=Math.Max(8,proposal.Duration*_pixelsPerSecond);
        var pointer=e.GetPosition(TimelineScroll);if(pointer.X>TimelineScroll.ActualWidth-25)TimelineScroll.ScrollToHorizontalOffset(TimelineScroll.HorizontalOffset+12);else if(pointer.X<25)TimelineScroll.ScrollToHorizontalOffset(TimelineScroll.HorizontalOffset-12);
    }
    private void Clip_MouseUp(object sender,MouseButtonEventArgs e)
    {
        if(_dragTrack is null)return;var track=_dragTrack;var proposal=DragProposal(e.GetPosition(TimelineCanvas).X);
        ((Border)sender).ReleaseMouseCapture();
        if(Math.Abs(proposal.Start-_clipOldStart)>.001||Math.Abs(proposal.Duration-_clipOldDuration)>.001)
        {
            var valid=track.Action is null||!_recordedActions.Any(a=>!ReferenceEquals(a,track.Action)&&a.Model==track.Action.Model&&a.Start<proposal.Start+proposal.Duration-.001&&a.Start+a.Duration>proposal.Start+.001);
            if(valid){PushTimelineUndo();if(track.Action is not null){track.Action.Start=proposal.Start;track.Action.Duration=proposal.Duration;track.Action.SourceOffset=proposal.Offset;if(proposal.Duration+proposal.Offset>track.Action.SourceDuration+.001)track.Action.Loop=true;}else if(track.Sound is not null){track.Sound.Start=proposal.Start;track.Sound.Duration=proposal.Duration;track.Sound.SourceOffset=proposal.Offset;if(proposal.Duration+proposal.Offset>track.Sound.OriginalDuration+.001)track.Sound.Loop=true;}SeekTimeline(proposal.Start);StatusText.Text="Fragment aangepast. Langer dan de bron = herhalen. Ctrl+Z maakt dit ongedaan.";}
            else StatusText.Text="Hier overlappen twee acties van dezelfde acteur. Het fragment staat weer op zijn vorige plek.";
        }
        _dragTrack=null;_dragBorder=null;RefreshMovieTracks();e.Handled=true;
    }
    private double FreeActionStart(string model,double requested,double duration)
    {
        var start=Math.Clamp(SnapTime(requested),0,TimelineSlider.Maximum);
        foreach(var action in _recordedActions.Where(a=>a.Model==model).OrderBy(a=>a.Start))if(action.Start<start+duration-.001&&action.Start+action.Duration>start+.001)start=action.Start+action.Duration;
        return start+duration<=TimelineSlider.Maximum+.001?start:-1;
    }
    private void LibraryClip_MouseDown(object sender,MouseButtonEventArgs e)=>_libraryDragStart=e.GetPosition((IInputElement)sender);
    private void LibraryClip_MouseMove(object sender,MouseEventArgs e)
    {
        if(e.LeftButton!=MouseButtonState.Pressed||(e.GetPosition((IInputElement)sender)-_libraryDragStart).Length<5)return;
        var list=(ListBox)sender;var item=(ItemsControl.ContainerFromElement(list,e.OriginalSource as DependencyObject) as ListBoxItem)?.Content??list.SelectedItem;
        var model=ObjectList.SelectedItem as string??"";
        var payload=item is ActionClip action?new LibraryClip(model,action):item is AudioEntry sound?new LibraryClip(sound.Model,Sound:sound):null;
        if(payload is null)return;DragDrop.DoDragDrop(list,new DataObject(typeof(LibraryClip),payload),DragDropEffects.Copy);e.Handled=true;
    }
    private void LibraryClip_DoubleClick(object sender,MouseButtonEventArgs e)
    {if(sender==ActionList)AddAction_Click(this,new RoutedEventArgs());else AddSound_Click(this,new RoutedEventArgs());e.Handled=true;}
    private void Timeline_DragOver(object sender,DragEventArgs e){var row=(int)((e.GetPosition(TimelineCanvas).Y-RulerHeight)/TrackHeight);e.Effects=e.Data.GetDataPresent(typeof(LibraryClip))&&row!=0?DragDropEffects.Copy:DragDropEffects.None;if(e.Effects==DragDropEffects.Copy)StatusText.Text=$"Loslaten om het fragment op {TimeAt(e.GetPosition(TimelineCanvas).X):0.0} s te plaatsen.";e.Handled=true;}
    private void Timeline_Drop(object sender,DragEventArgs e)
    {
        if(e.Data.GetData(typeof(LibraryClip)) is not LibraryClip payload)return;
        var point=e.GetPosition(TimelineCanvas);var time=TimeAt(point.X);var row=(int)((point.Y-RulerHeight)/TrackHeight);
        if(row==0){StatusText.Text="Het cameraspoor is voor camerashots. Voeg een shot toe met ＋ Camerashot.";e.Handled=true;return;}
        var model=payload.Model;if(row>=0&&row<_timelineLanes.Count&&!string.IsNullOrEmpty(_timelineLanes[row].Split('|')[0]))model=_timelineLanes[row].Split('|')[0];
        if(payload.Action is not null)
        {
            if(!_models.TryGetValue(model,out var target)){StatusText.Text="Voeg eerst een acteur toe en sleep de actie naar zijn spoor.";return;}
            var clip=AvailableClips(target.SourcePath).FirstOrDefault(c=>c.name==payload.Action.name);
            if(clip is null){StatusText.Text="Deze acteur heeft die actie niet. Kies een actie uit zijn eigen menu.";return;}
            InsertActionAt(model,clip,time);
        }
        else if(payload.Sound is not null){var sound=payload.Sound;if(!string.IsNullOrEmpty(sound.Model))sound=new AudioEntry {name=sound.name,file=sound.file,archive=sound.archive,Label=sound.Label,Model=model,LipData=sound.LipData,Category=sound.Category};InsertSoundAt(sound,time);}
        e.Handled=true;
    }
    private bool HandleTimelineKey(KeyEventArgs e)
    {
        if(e.OriginalSource is TextBox||_exporting)return false;
        if(Keyboard.Modifiers.HasFlag(ModifierKeys.Control)&&e.Key==Key.Z){if(Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))RedoTimeline_Click(this,new RoutedEventArgs());else UndoTimeline_Click(this,new RoutedEventArgs());return true;}
        if(Keyboard.Modifiers.HasFlag(ModifierKeys.Control)&&e.Key==Key.Y){RedoTimeline_Click(this,new RoutedEventArgs());return true;}
        if(Keyboard.Modifiers.HasFlag(ModifierKeys.Control)&&e.Key==Key.S){SaveProject_Click(this,new RoutedEventArgs());return true;}
        if(e.Key==Key.V){Razor_Click(this,new RoutedEventArgs());return true;}
        if(e.Key==Key.Escape){SetRazor(false);return true;}
        if(e.Key==Key.Home){PausePlayback();SeekTimeline(0);return true;}
        if(e.Key==Key.Delete&&_selectedKey is not null){DeletePositionKey();return true;}
        if(e.Key==Key.Delete&&_selectedCameraShot is not null){DeleteCameraShot_Click(this,new RoutedEventArgs());return true;}
        if(e.Key==Key.Delete&&_selectedTrack is not null){SelectTrack(_selectedTrack);RemoveAction_Click(this,new RoutedEventArgs());return true;}
        if(e.Key==Key.Space){Preview_Click(this,new RoutedEventArgs());return true;}return false;
    }
    private void Razor_Click(object sender,RoutedEventArgs e)=>SetRazor(!_razorMode);
    private void SetRazor(bool value){_razorMode=value;RazorButton.Content=value?"✂ Knippen aan (V)":"✂ Knippen (V)";StatusText.Text=value?"Klik op een fragment om daar te knippen. V of Esc schakelt terug naar slepen.":"Sleep fragmenten of hun randen. Spatie: afspelen/pauze.";RenderTimeline();}
    private bool SplitTrack(TrackDisplay track,double time)
    {
        var left=time-ClipStart(track);var right=ClipDuration(track)-left;
        if(left<.1||right<.1){StatusText.Text="Knip minimaal 0,1 seconde van de rand.";return false;}
        PausePlayback();PushTimelineUndo();
        if(track.Action is {} action){var next=JsonSerializer.Deserialize<RecordedAction>(JsonSerializer.Serialize(action))!;next.Start=time;next.Duration=right;next.SourceOffset+=left;action.Duration=left;_recordedActions.Add(next);_selectedTrack=new TrackDisplay(next.Name,next);}
        else if(track.Sound is {} sound){var next=JsonSerializer.Deserialize<RecordedSound>(JsonSerializer.Serialize(sound))!;next.Start=time;next.Duration=right;next.SourceOffset+=left;sound.Duration=left;_recordedSounds.Add(next);_selectedTrack=new TrackDisplay(next.Name,Sound:next);}
        SeekTimeline(time);RefreshMovieTracks();StatusText.Text="Fragment geknipt; beide delen zijn apart verplaatsbaar. Ctrl+Z: ongedaan.";return true;
    }
}





