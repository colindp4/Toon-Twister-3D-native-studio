using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ToonTwisterNative;
public partial class MainWindow
{
    private double _sceneDuration;
    private StudioPreferences _preferences=new();
    private sealed class StudioPreferences {public double Volume{get;set;}=.8;public bool LipSync{get;set;}=true;public int Quality{get;set;}=1;public int Fps{get;set;}=30;public bool Dark{get;set;}}
    private static string PreferencesPath=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"ToonTwisterNative","settings.json");
    private void LoadStudioPreferences()
    {try{if(File.Exists(PreferencesPath)&&!Environment.GetCommandLineArgs().Contains("--render-check"))_preferences=JsonSerializer.Deserialize<StudioPreferences>(File.ReadAllText(PreferencesPath))??new();}catch{_preferences=new();}ApplyStudioPreferences();}
    private void ApplyStudioPreferences()
    {
        _preferences.Volume=Math.Clamp(_preferences.Volume,0,1);_preferences.Fps=_preferences.Fps is 15 or 30 or 60?_preferences.Fps:30;
        _motionTimer.Interval=TimeSpan.FromSeconds(1d/_preferences.Fps);_timer.Interval=_motionTimer.Interval;
        RenderOptions.SetBitmapScalingMode(SceneViewport,_preferences.Quality==0?BitmapScalingMode.LowQuality:BitmapScalingMode.HighQuality);
        foreach(var player in _playingSounds.Values)player.Volume=_preferences.Volume;if(_soundPreview is not null)_soundPreview.Volume=_preferences.Volume;
        Background=new SolidColorBrush(_preferences.Dark?Color.FromRgb(30,43,61):Color.FromRgb(245,120,22));
    }
    private void Settings_Click(object sender,RoutedEventArgs e)
    {
        var window=new Window {Owner=this,Title="Studio-instellingen",Width=460,Height=460,WindowStartupLocation=WindowStartupLocation.CenterOwner,ResizeMode=ResizeMode.NoResize,Background=new SolidColorBrush(Color.FromRgb(245,250,232))};
        var panel=new StackPanel {Margin=new Thickness(24)};window.Content=panel;
        void Label(string value){panel.Children.Add(new TextBlock {Text=value,FontWeight=FontWeights.Bold,Margin=new Thickness(0,12,0,6)});}
        Label("BEELD · jouw studio");
        var quality=new ComboBox {ItemsSource=new[]{"Snel — lichtere textuurfiltering","Mooi — vloeiende textuurfiltering"},SelectedIndex=Math.Clamp(_preferences.Quality,0,1)};panel.Children.Add(quality);
        var fps=new ComboBox {ItemsSource=new[]{"15 fps — zuinig","30 fps — normaal","60 fps — vloeiend"},SelectedIndex=_preferences.Fps==15?0:_preferences.Fps==60?2:1,Margin=new Thickness(0,6,0,0)};panel.Children.Add(fps);
        var dark=new CheckBox {Content="Donkere studio-omlijsting",IsChecked=_preferences.Dark,Margin=new Thickness(0,10,0,0)};panel.Children.Add(dark);
        Label("GELUID · afluisteren");var volume=new Slider {Minimum=0,Maximum=100,Value=_preferences.Volume*100,TickFrequency=10,ToolTip="Volume bij afspelen en beluisteren; MP4 behoudt het originele volume."};panel.Children.Add(volume);
        var volumeText=new TextBlock {Text=$"Volume: {volume.Value:0}%"};volume.ValueChanged+=(_,_)=>volumeText.Text=$"Volume: {volume.Value:0}%";panel.Children.Add(volumeText);
        var lips=new CheckBox {Content="Automatische mondbeweging bij spraak",IsChecked=_preferences.LipSync,Margin=new Thickness(0,12,0,0)};panel.Children.Add(lips);
        panel.Children.Add(new TextBlock {Text="Beeldkwaliteit en fps gelden voor het werkvenster.\nMP4 wordt met 30 fps geëxporteerd.",Margin=new Thickness(0,15,0,10),Foreground=Brushes.DimGray});
        var save=new Button {Content="✓ Toepassen en bewaren",Padding=new Thickness(12,8,12,8)};panel.Children.Add(save);
        save.Click+=(_,_)=>{_preferences.Quality=quality.SelectedIndex;_preferences.Fps=fps.SelectedIndex==0?15:fps.SelectedIndex==2?60:30;_preferences.Volume=volume.Value/100;_preferences.Dark=dark.IsChecked==true;_preferences.LipSync=lips.IsChecked==true;ApplyStudioPreferences();try{Directory.CreateDirectory(Path.GetDirectoryName(PreferencesPath)!);File.WriteAllText(PreferencesPath,JsonSerializer.Serialize(_preferences));window.Close();}catch(Exception error){MessageBox.Show(window,StudioLocalization.Translate(error.Message),StudioLocalization.Translate("Instellingen bewaren"));}};
        window.ShowDialog();
    }
    private void UpdateSceneDurationControls()
    {
        if(SceneDurationBox is null)return;
        var contentEnd=new[]{_recordedActions.Select(a=>a.Start+a.Duration).DefaultIfEmpty(0).Max(),_recordedSounds.Select(a=>a.Start+a.Duration).DefaultIfEmpty(0).Max(),_keyframes.Values.SelectMany(k=>k.Keys).DefaultIfEmpty(0).Max(),_cameraShots.Select(s=>s.Start).DefaultIfEmpty(0).Max()}.Max();
        var effective=_sceneDuration>0?_sceneDuration:Math.Min(300,Math.Max(3,contentEnd+.5));
        _updatingDurationControls=true;
        SceneDurationBox.Text=effective.ToString("0.##",System.Globalization.CultureInfo.InvariantCulture);
        SceneLengthSlider.Value=Math.Clamp(effective,1,300);SceneLengthText.Text=$"{SceneLengthSlider.Value:0.#} s";
        TimelineSlider.Maximum=Math.Clamp(Math.Max(effective,contentEnd),1,300);
        TimelineSlider.Value=Math.Min(TimelineSlider.Value,TimelineSlider.Maximum);
        _updatingDurationControls=false;
        RenderTimeline();
    }
    private void SceneLengthSlider_Changed(object sender,RoutedPropertyChangedEventArgs<double> e)
    {
        if(SceneLengthText is null)return;
        SceneLengthText.Text=$"{SceneLengthSlider.Value:0.#} s";
        if(_updatingDurationControls||!IsInitialized)return;
        PausePlayback();_sceneDuration=Math.Round(SceneLengthSlider.Value,1);UpdateSceneDurationControls();
    }
    private void SetSceneDuration_Click(object sender,RoutedEventArgs e)
    {
        var text=SceneDurationBox.Text.Trim();double value=0;
        var automatic=text.Equals("auto",StringComparison.OrdinalIgnoreCase);
        if(!automatic&&(!double.TryParse(text.Replace(',','.'),System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out value)||!double.IsFinite(value)||value<1||value>300)){StatusText.Text="Scèneduur: vul 1 tot 300 seconden in, of Auto.";return;}
        PausePlayback();_sceneDuration=automatic?0:value;UpdateSceneDurationControls();StatusText.Text=automatic?"Scèneduur volgt automatisch je acties en geluid.":$"Deze scène duurt {value:0.##} seconden.";
    }
}
