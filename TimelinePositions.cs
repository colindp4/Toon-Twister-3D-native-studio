using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace ToonTwisterNative;
public partial class MainWindow
{
    private (string Model,double Time)? _selectedKey;
    private void RenderPositionKeys()
    {
        foreach(var pair in _keyframes)
        {
            var model=pair.Key;var lane=_timelineLanes.IndexOf(model+"|act");if(lane<0)continue;
            foreach(var frame in pair.Value.Values.ToArray())
            {
                var oldTime=frame.Time;var pressed=false;var startPoint=new Point();
                var diamond=new Polygon {Points=new PointCollection([new Point(0,6),new Point(6,0),new Point(12,6),new Point(6,12)]),Fill=Brushes.RoyalBlue,Stroke=Brushes.White,StrokeThickness=1,Cursor=Cursors.SizeWE,ToolTip=$"{model} · positie op {oldTime:0.0} s · sleep om te verplaatsen"};
                Canvas.SetLeft(diamond,TrackLabelWidth+oldTime*_pixelsPerSecond-6);Canvas.SetTop(diamond,RulerHeight+lane*TrackHeight+24);TimelineCanvas.Children.Add(diamond);
                diamond.MouseLeftButtonDown+=(_,e)=>{_selectedKey=(model,oldTime);_selectedTrack=null;ObjectList.SelectedItem=model;_timer.Stop();StopAudio();_scrubMode=true;pressed=true;startPoint=e.GetPosition(TimelineCanvas);diamond.CaptureMouse();e.Handled=true;};
                diamond.MouseMove+=(_,e)=>{if(!pressed)return;var time=Math.Clamp(SnapTime(oldTime+(e.GetPosition(TimelineCanvas).X-startPoint.X)/_pixelsPerSecond),0,45);Canvas.SetLeft(diamond,TrackLabelWidth+time*_pixelsPerSecond-6);};
                diamond.MouseLeftButtonUp+=(_,e)=>
                {
                    if(!pressed)return;pressed=false;diamond.ReleaseMouseCapture();var time=Math.Clamp(SnapTime(oldTime+(e.GetPosition(TimelineCanvas).X-startPoint.X)/_pixelsPerSecond),0,45);
                    if(Math.Abs(time-oldTime)>.001&&_keyframes.TryGetValue(model,out var frames))
                    {if(!frames.ContainsKey(time)){PushTimelineUndo();frames.Remove(oldTime);frame.Time=time;frames[time]=frame;_selectedKey=(model,time);}else StatusText.Text="Op dat tijdstip staat al een positiepunt.";}
                    SeekTimeline(time);RefreshMovieTracks();e.Handled=true;
                };
                var menu=new ContextMenu();var remove=new MenuItem {Header="Positiepunt verwijderen"};remove.Click+=(_,_)=>{_selectedKey=(model,oldTime);DeletePositionKey();};menu.Items.Add(remove);diamond.ContextMenu=menu;
            }
        }
    }
    private void DeletePositionKey()
    {
        if(_selectedKey is not {} selected||!_keyframes.TryGetValue(selected.Model,out var frames)||!frames.ContainsKey(selected.Time))return;
        PushTimelineUndo();frames.Remove(selected.Time);_selectedKey=null;UpdateKeyframeCount();RefreshMovieTracks();
    }
}
