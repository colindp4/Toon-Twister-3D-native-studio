using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ToonTwisterNative;
public partial class MainWindow
{
    private List<AudioEntry>? _audioCatalog;
    private readonly List<RecordedSound> _recordedSounds=[];
    private readonly Dictionary<RecordedSound,MediaPlayer> _playingSounds=[];
    private readonly HashSet<MediaPlayer> _pendingSoundOpens=[];
    private MediaPlayer? _soundPreview;
    private string _previewVoiceModel="",_previewLipData="";
    private void SoundTool_Changed(object sender,SelectionChangedEventArgs e)
    {
        if(!IsInitialized || ToolCombo is null || AudioList is null)return;
        var audio=ToolCombo.SelectedIndex>0;
        AudioList.Visibility=audio?Visibility.Visible:Visibility.Collapsed;
        ActionList.Visibility=audio?Visibility.Collapsed:Visibility.Visible;
        ActionCategory.Visibility=audio?Visibility.Collapsed:Visibility.Visible;
        ActionDurationPanel.Visibility=audio?Visibility.Collapsed:Visibility.Visible;
        DirectHelpText.Text=audio?"Sleep het geluid naar de tijdlijn. Dubbelklik plaatst het op de afspeelkop. Stemmen bewegen automatisch de mond.":"Sleep een actie naar het spoor van de acteur. Klik op een actie om die direct te bekijken.";
        InsertActionButton.Visibility=audio?Visibility.Collapsed:Visibility.Visible;
        InsertSoundButton.Visibility=audio?Visibility.Visible:Visibility.Collapsed;
        PreviewSoundButton.Visibility=audio?Visibility.Visible:Visibility.Collapsed;
        ImportAudioButton.Visibility=ToolCombo.SelectedIndex==4?Visibility.Visible:Visibility.Collapsed;
        OwnAudioVoice.Visibility=ToolCombo.SelectedIndex==4?Visibility.Visible:Visibility.Collapsed;
        RefreshAudioChoices();
    }
    private void RefreshAudioChoices()
    {
        if(AudioList is null || ToolCombo is null)return;
        if(ToolCombo.SelectedIndex==4){ShowImportedAudio();return;}
        AudioList.Items.Clear();
        if(_audioCatalog is null)
        {
            var path=Path.Combine(AppContext.BaseDirectory,"Assets","Audio","catalog.json");
            _audioCatalog=File.Exists(path)?JsonSerializer.Deserialize<List<AudioEntry>>(File.ReadAllText(path))??[]:[];
        }
        IEnumerable<AudioEntry> entries=_audioCatalog;
        if(ToolCombo.SelectedIndex==1)
        {
            if(ObjectList.SelectedItem is not string selected || !_models.TryGetValue(selected,out var actor))return;
            var key=System.Text.RegularExpressions.Regex.Replace(Path.GetFileNameWithoutExtension(actor.SourcePath),"_(hi|me|lo)$","").Replace("act_","");
            var index=Array.IndexOf(ActorOrder,key);
            var dialoguePath=Path.Combine(AppContext.BaseDirectory,"Assets","Audio","dialogue.json");
            if(index<0 || !File.Exists(dialoguePath))return;
            using var doc=JsonDocument.Parse(File.ReadAllText(dialoguePath));
            if(index>=doc.RootElement.GetArrayLength())return;
            var byName=_audioCatalog.GroupBy(a=>a.name).ToDictionary(g=>g.Key,g=>g.First(),StringComparer.OrdinalIgnoreCase);
            foreach(var line in doc.RootElement[index].EnumerateArray())
                if(byName.TryGetValue(line.GetProperty("file").GetString()??"",out var sound))
                    AudioList.Items.Add(new AudioEntry {name=sound.name,file=sound.file,archive=sound.archive,Label=line.GetProperty("desc").GetString()??sound.name,Model=selected,LipData=line.TryGetProperty("lipdata",out var lips)?lips.GetString()??"":""});
            return;
        }
        entries=entries.Where(a=>ToolCombo.SelectedIndex==4?a.archive=="UserAudio":ToolCombo.SelectedIndex==2?a.archive.Equals("Music.cxt",StringComparison.OrdinalIgnoreCase):a.archive.Equals("sfx.cxt",StringComparison.OrdinalIgnoreCase));
        foreach(var sound in entries.OrderBy(a=>a.name))AudioList.Items.Add(sound);
    }
    private void PreviewSound_Click(object sender,RoutedEventArgs e)
    {
        if(AudioList.SelectedItem is not AudioEntry sound)return;
        _soundPreview?.Close();_soundPreview=new MediaPlayer {Volume=_preferences.Volume};
        _previewVoiceModel=sound.Model;_previewLipData=sound.LipData;
        _soundPreview.MediaEnded+=(_,_)=>{_previewVoiceModel="";_previewLipData="";};
        _soundPreview.Open(new Uri(ResolveAudioPath(sound.file)));_soundPreview.Play();
        StatusText.Text=$"Luisteren: {sound.Label}";
    }
    private void AddSound_Click(object sender,RoutedEventArgs e)
    {
        if(AudioList.SelectedItem is not AudioEntry sound){StatusText.Text="Selecteer eerst een geluidsfragment.";return;}
        InsertSoundAt(sound,TimelineSlider.Value);
    }
    private void InsertSoundAt(AudioEntry sound,double start)
    {
        if(sound.Category=="voice"&&(!_models.TryGetValue(sound.Model,out var speaker)||!NativeMotions.TryGetValue(speaker.Visual,out _)))
        {var choice=ChooseAudioUsage(sound.Label,"voice",sound.Model);if(choice is null)return;sound.Category=choice.Value.Category;sound.Model=choice.Value.Model;if(sound.Category=="music")sound.LipData="";}
        var targetScene=_sceneIndex;
        var player=new MediaPlayer();
        _pendingSoundOpens.Add(player);
        player.MediaOpened+=(_,_)=>
        {
            _pendingSoundOpens.Remove(player);
            if(_sceneIndex!=targetScene){player.Close();return;}
            var originalDuration=player.NaturalDuration.HasTimeSpan?player.NaturalDuration.TimeSpan.TotalSeconds:2;
            originalDuration=Math.Max(.1,originalDuration);var duration=Math.Min(originalDuration,TimelineSlider.Maximum-start);
            if(duration<.1){StatusText.Text="Zet de afspeelkop iets eerder: de scène is vol.";player.Close();return;}
            _timer.Stop();StopAudio();
            PushTimelineUndo();
            _recordedSounds.Add(new RecordedSound {Name=sound.Label,File=sound.file,Start=start,Duration=duration,OriginalDuration=originalDuration,Model=sound.Model,LipData=sound.LipData,Category=!string.IsNullOrEmpty(sound.Category)?sound.Category:sound.archive.Equals("Music.cxt",StringComparison.OrdinalIgnoreCase)?"music":string.IsNullOrEmpty(sound.Model)?"sfx":"voice"});
            SeekTimeline(start);
            RefreshMovieTracks();StatusText.Text=$"{sound.Label} toegevoegd op {start:0.0} s"+(duration<originalDuration?$" (ingekort tot {duration:0.0} s).":".");player.Close();
        };
        player.MediaFailed+=(_,error)=>{_pendingSoundOpens.Remove(player);StatusText.Text="Het geluidsfragment kon niet worden geopend: "+error.ErrorException.Message;player.Close();};
        player.Open(new Uri(ResolveAudioPath(sound.file)));
    }
    private void SyncAudioPlayback(double time)
    {
        foreach(var sound in _playingSounds.Keys.Where(s=>time<s.Start||time>=s.Start+s.Duration).ToArray())
        {_playingSounds[sound].Close();_playingSounds.Remove(sound);}
        foreach(var sound in _recordedSounds.Where(s=>time>=s.Start&&time<s.Start+s.Duration))
        {
            if(_playingSounds.TryGetValue(sound,out var existing)){if(existing.NaturalDuration.HasTimeSpan&&Math.Abs(existing.Position.TotalSeconds-SoundTime(sound,time))>.2){existing.Position=TimeSpan.FromSeconds(SoundTime(sound,time));existing.Play();}continue;}
            var player=new MediaPlayer {Volume=_preferences.Volume};_playingSounds[sound]=player;
            player.MediaOpened+=(_,_)=>{player.Position=TimeSpan.FromSeconds(SoundTime(sound,TimelineSlider.Value));player.Play();};
            player.MediaEnded+=(_,_)=>{if(sound.Loop&&TimelineSlider.Value<sound.Start+sound.Duration){player.Position=TimeSpan.FromSeconds(SoundTime(sound,TimelineSlider.Value));player.Play();}};
            player.Open(new Uri(ResolveAudioPath(sound.File)));
        }
    }
    private void StopAudio()
    {foreach(var player in _playingSounds.Values)player.Close();_playingSounds.Clear();_soundPreview?.Close();_previewVoiceModel="";_previewLipData="";}
    private sealed class AudioEntry
    {
        public string Category{get;set;}="";
        public string name{get;set;}="";public string file{get;set;}="";public string archive{get;set;}="";
        public string Model{get;set;}="";public string LipData{get;set;}="";
        private string? _label;
        [JsonIgnore]public string Label{get=>_label??name.Replace("mus_","").Replace("sfx_","").Replace('_',' ');set=>_label=value;}
        public override string ToString()=>Label;
    }
    private sealed class RecordedSound {public string Name{get;set;}="";public string File{get;set;}="";public double Start{get;set;}public double Duration{get;set;}public double SourceOffset{get;set;}public double OriginalDuration{get;set;}public bool Loop{get;set;}public string Model{get;set;}="";public string LipData{get;set;}="";public string Category{get;set;}="sfx";}
    private static double SoundTime(RecordedSound sound,double timeline)=>sound.Loop&&sound.OriginalDuration>.001?PositiveModulo(sound.SourceOffset+timeline-sound.Start,sound.OriginalDuration):Math.Max(0,sound.SourceOffset+timeline-sound.Start);
    private static double PositiveModulo(double value,double range)=>(value%range+range)%range;
    private int VisemeFor(SceneModel model)
    {
        if(!_preferences.LipSync)return 0;
        if(_previewVoiceModel==model.Name&&_soundPreview is not null&&!string.IsNullOrEmpty(_previewLipData))return MouthAt(_previewLipData,_soundPreview.Position.TotalSeconds);
        if(!_timer.IsEnabled&&!_scrubMode)return 0;
        var sound=_recordedSounds.LastOrDefault(s=>s.Model==model.Name&&TimelineSlider.Value>=s.Start&&TimelineSlider.Value<s.Start+s.Duration);
        if(sound is null||string.IsNullOrEmpty(sound.LipData))return 0;
        var time=SoundTime(sound,TimelineSlider.Value);
        if(_timer.IsEnabled&&_playingSounds.TryGetValue(sound,out var player))time=player.Position.TotalSeconds;
        return MouthAt(sound.LipData,time);
    }
    private static int MouthAt(string data,double time)
    {var i=(int)Math.Floor(Math.Max(0,time)*30);return i<data.Length&&data[i]>='1'&&data[i]<='8'?data[i]-'0':1;}
    private void HydrateVoiceBindings()
    {
        var file=Path.Combine(AppContext.BaseDirectory,"Assets","Audio","dialogue.json");if(!File.Exists(file))return;using var doc=JsonDocument.Parse(File.ReadAllText(file));
        foreach(var sound in _recordedSounds)
        {
            if(sound.OriginalDuration<=0)sound.OriginalDuration=sound.Duration+sound.SourceOffset;
            if(sound.File.StartsWith("mus_",StringComparison.OrdinalIgnoreCase))sound.Category="music";
            if(!string.IsNullOrEmpty(sound.LipData)&&!string.IsNullOrEmpty(sound.Model))continue;
            foreach(var actor in _models.Values.Where(m=>string.IsNullOrEmpty(sound.Model)||m.Name==sound.Model))
            {
                var name=System.Text.RegularExpressions.Regex.Replace(Path.GetFileNameWithoutExtension(actor.SourcePath),"_(hi|me|lo)$","").Replace("act_","");var index=Array.IndexOf(ActorOrder,name);if(index<0||index>=doc.RootElement.GetArrayLength())continue;
                var line=doc.RootElement[index].EnumerateArray().FirstOrDefault(v=>v.GetProperty("file").GetString()==Path.GetFileNameWithoutExtension(sound.File));if(line.ValueKind==JsonValueKind.Undefined)continue;
                sound.Model=actor.Name;sound.Category="voice";sound.LipData=line.TryGetProperty("lipdata",out var lips)?lips.GetString()??"":"";break;
            }
        }
    }
    private void ShiftClip_Click(object sender,RoutedEventArgs e)
    {
        if(MovieTracks.SelectedItem is not TrackDisplay track)return;
        PushTimelineUndo();
        var index=MovieTracks.SelectedIndex;var delta=double.Parse(((Button)sender).Tag.ToString()!,System.Globalization.CultureInfo.InvariantCulture);
        if(track.Action is not null)track.Action.Start=Math.Clamp(track.Action.Start+delta,0,TimelineSlider.Maximum-track.Action.Duration);
        if(track.Sound is not null)track.Sound.Start=Math.Clamp(track.Sound.Start+delta,0,TimelineSlider.Maximum-track.Sound.Duration);
        RefreshMovieTracks();MovieTracks.SelectedIndex=index;
    }
    private sealed record TrackDisplay(string Text,RecordedAction? Action=null,RecordedSound? Sound=null)
    {public double Left=>(Action?.Start??Sound?.Start??0)*17;public double Width=>Math.Max(4,(Action?.Duration??Sound?.Duration??0)*17);public Brush Color=>Sound is null?Brushes.MediumSeaGreen:Brushes.Goldenrod;public override string ToString()=>Text;}
}



