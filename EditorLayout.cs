using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ToonTwisterNative;
public partial class MainWindow
{
    private readonly Dictionary<Border,Window> _floatingPanels=[];
    private static string LayoutPath=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"ToonTwisterNative","layout.json");
    private sealed class EditorLayoutState {public double Width{get;set;}=1400;public double Height{get;set;}=920;public double Actors{get;set;}=160;public double Inspector{get;set;}=220;public double Tools{get;set;}=180;public double Timeline{get;set;}=230;public double Zoom{get;set;}=1;public bool Maximized{get;set;}}
    private void RestoreEditorLayout()
    {
        if(Environment.GetCommandLineArgs().Any(a=>a.StartsWith("--render-")))return;
        try
        {
            if(!File.Exists(LayoutPath))return;var data=JsonSerializer.Deserialize<EditorLayoutState>(File.ReadAllText(LayoutPath));if(data is null)return;
            Width=Math.Clamp(data.Width,1000,SystemParameters.WorkArea.Width);Height=Math.Clamp(data.Height,720,SystemParameters.WorkArea.Height);
            ActorColumn.Width=new GridLength(Math.Clamp(data.Actors,120,450));InspectorColumn.Width=new GridLength(Math.Clamp(data.Inspector,170,500));ToolRow.Height=new GridLength(Math.Clamp(data.Tools,140,600));TimelineRow.Height=new GridLength(Math.Clamp(data.Timeline,140,600));TimelineZoom.Value=Math.Clamp(data.Zoom,.5,4);
            if(data.Maximized)WindowState=WindowState.Maximized;
        }
        catch { }
    }
    private void SaveEditorLayout()
    {
        if(Environment.GetCommandLineArgs().Any(a=>a.StartsWith("--render-")))return;
        try
        {
            var data=new EditorLayoutState {Width=RestoreBounds.Width,Height=RestoreBounds.Height,Actors=ActorColumn.ActualWidth,Inspector=InspectorColumn.ActualWidth,Tools=ToolRow.ActualHeight,Timeline=TimelineRow.ActualHeight,Zoom=TimelineZoom.Value,Maximized=WindowState==WindowState.Maximized};
            Directory.CreateDirectory(Path.GetDirectoryName(LayoutPath)!);File.WriteAllText(LayoutPath,JsonSerializer.Serialize(data));
        }
        catch { }
    }
    private void LayoutMenu_Click(object sender,RoutedEventArgs e)
    {
        var menu=new ContextMenu();
        foreach(var pair in new[]{(InspectorHost,"Eigenschappen in apart venster"),(ToolboxHost,"Keuzemenu in apart venster"),(TimelineHost,"Tijdlijn in apart venster")})
        {var item=new MenuItem {Header=pair.Item2};item.Click+=(_,_)=>FloatPanel(pair.Item1,pair.Item2);menu.Items.Add(item);}
        menu.Items.Add(new Separator());var reset=new MenuItem {Header="Standaardindeling herstellen"};reset.Click+=(_,_)=>ResetEditorLayout();menu.Items.Add(reset);menu.PlacementTarget=(Button)sender;menu.IsOpen=true;
    }
    private void FloatPanel(Border host,string title)
    {
        if(_floatingPanels.TryGetValue(host,out var existing)){existing.Activate();return;}
        var content=host.Child;if(content is null)return;host.Child=null;
        var window=new Window {Title=title,Owner=this,Width=Math.Clamp(host.ActualWidth,330,1100),Height=Math.Clamp(host.ActualHeight+60,260,700),MinWidth=300,MinHeight=220,Background=new SolidColorBrush(Color.FromRgb(235,247,205)),Content=content,WindowStartupLocation=WindowStartupLocation.CenterOwner};
        window.Resources.MergedDictionaries.Add(Resources);
        window.PreviewKeyDown+=Window_KeyDown;
        var restore=new Button {Content="Paneel staat in een apart venster. Klik hier om terug te zetten.",Padding=new Thickness(8),HorizontalAlignment=HorizontalAlignment.Stretch};restore.Click+=(_,_)=>window.Close();host.Child=restore;
        _floatingPanels.Add(host,window);window.Closed+=(_,_)=>{window.Content=null;host.Child=null;host.Child=content;_floatingPanels.Remove(host);RenderTimeline();};window.Show();
    }
    private void ResetEditorLayout()
    {
        foreach(var window in _floatingPanels.Values.ToArray())window.Close();
        ActorColumn.Width=new GridLength(160);InspectorColumn.Width=new GridLength(220);SceneRow.Height=new GridLength(1,GridUnitType.Star);ToolRow.Height=new GridLength(180);TimelineRow.Height=new GridLength(230);TimelineZoom.Value=1;
    }
}

