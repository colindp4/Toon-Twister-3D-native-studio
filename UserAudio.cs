using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;

namespace ToonTwisterNative;
public partial class MainWindow
{
    private sealed class ImportedAudio {public string Name{get;set;}="";public string File{get;set;}="";public string LipData{get;set;}="";public int EnvelopeVersion{get;set;}public string Category{get;set;}="music";public string Model{get;set;}="";}
    private static string UserAudioDirectory=>Environment.GetCommandLineArgs().Contains("--render-check")
        ? Path.Combine(AppContext.BaseDirectory,"Verification","UserAudio")
        : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"ToonTwisterNative","UserAudio");
    private static IEnumerable<string> LegacyUserAudioDirectories()
    {
        var folder=new DirectoryInfo(AppContext.BaseDirectory);
        for(var i=0;i<3&&folder is not null;i++,folder=folder.Parent)
        {
            yield return Path.Combine(folder.FullName,"Assets","UserAudio");
            yield return Path.Combine(folder.FullName,"Native Editor","Assets","UserAudio");
        }
    }
    private static string ResolveAudioPath(string file)
    {
        if(!file.Replace('\\','/').StartsWith("UserAudio/",StringComparison.OrdinalIgnoreCase))
            return Path.Combine(AppContext.BaseDirectory,"Assets","Audio",Path.GetFileName(file));
        var name=Path.GetFileName(file);var current=Path.Combine(UserAudioDirectory,name);
        if(File.Exists(current))return current;
        return LegacyUserAudioDirectories().Select(folder=>Path.Combine(folder,name)).FirstOrDefault(File.Exists)??current;
    }
    private static void MigrateLegacyUserAudio()
    {
        if(Environment.GetCommandLineArgs().Contains("--render-check"))return;
        Directory.CreateDirectory(UserAudioDirectory);
        var manifest=Path.Combine(UserAudioDirectory,"manifest.json");
        var entries=File.Exists(manifest)?JsonSerializer.Deserialize<List<ImportedAudio>>(File.ReadAllText(manifest))??[]:new List<ImportedAudio>();
        var changed=false;
        foreach(var folder in LegacyUserAudioDirectories().Distinct(StringComparer.OrdinalIgnoreCase).Where(Directory.Exists))
        {
            var oldManifest=Path.Combine(folder,"manifest.json");if(!File.Exists(oldManifest))continue;
            foreach(var item in JsonSerializer.Deserialize<List<ImportedAudio>>(File.ReadAllText(oldManifest))??[])
            {
                var source=Path.Combine(folder,Path.GetFileName(item.File));var target=Path.Combine(UserAudioDirectory,Path.GetFileName(item.File));
                if(!File.Exists(source))continue;
                if(!File.Exists(target))File.Copy(source,target);
                if(entries.Any(entry=>entry.File.Equals(item.File,StringComparison.OrdinalIgnoreCase)))continue;
                entries.Add(item);changed=true;
            }
        }
        if(changed)File.WriteAllText(manifest,JsonSerializer.Serialize(entries,new JsonSerializerOptions {WriteIndented=true}));
    }
    private static List<ImportedAudio> ReadImportedAudio()
    {MigrateLegacyUserAudio();var path=Path.Combine(UserAudioDirectory,"manifest.json");return File.Exists(path)?JsonSerializer.Deserialize<List<ImportedAudio>>(File.ReadAllText(path))??[]:[];}
    private void ShowImportedAudio()
    {
        AudioList.Items.Clear();
        var imported=ReadImportedAudio();var updated=false;
        foreach(var item in imported.Where(a=>File.Exists(ResolveAudioPath(a.File))&&a.EnvelopeVersion<2)){item.LipData=AudioMouthEnvelope(ResolveAudioPath(item.File));item.EnvelopeVersion=2;updated=true;foreach(var sound in _recordedSounds.Where(s=>s.File==item.File&&!string.IsNullOrEmpty(s.Model)))sound.LipData=item.LipData;}
        if(updated)File.WriteAllText(Path.Combine(UserAudioDirectory,"manifest.json"),JsonSerializer.Serialize(imported));
        foreach(var item in imported.Where(a=>File.Exists(ResolveAudioPath(a.File))).OrderBy(a=>a.Name))
            AudioList.Items.Add(ImportedEntry(item));
        if(AudioList.Items.Count>0)AudioList.SelectedIndex=0;
    }
    private static AudioEntry ImportedEntry(ImportedAudio item)=>new() {name=item.Name,file=item.File,archive="UserAudio",Label=item.Category=="voice"?$"Spraak · {item.Model} · {item.Name}":$"Muziek · {item.Name}",Model=item.Category=="voice"?item.Model:"",LipData=item.Category=="voice"?item.LipData:"",Category=item.Category=="voice"?"voice":"music"};
    private (string Category,string Model)? ChooseAudioUsage(string title,string category="voice",string model="")
    {
        var window=new Window {Title="Eigen audio · gebruik",Owner=this,Width=450,Height=300,ResizeMode=ResizeMode.NoResize,WindowStartupLocation=WindowStartupLocation.CenterOwner,Background=Brushes.Honeydew};
        var panel=new StackPanel {Margin=new Thickness(20)};window.Content=panel;
        panel.Children.Add(new TextBlock {Text=title,FontWeight=FontWeights.Bold,TextTrimming=TextTrimming.CharacterEllipsis,Margin=new Thickness(0,0,0,12)});
        var actors=_models.Values.Where(m=>NativeMotions.TryGetValue(m.Visual,out _)).Select(m=>m.Name).ToArray();
        var type=new ComboBox {ItemsSource=new[]{"Spraak — mond beweegt mee","Achtergrondmuziek — geen mondbeweging"},SelectedIndex=category=="voice"&&actors.Length>0?0:1};panel.Children.Add(type);
        var actor=new ComboBox {ItemsSource=actors,Margin=new Thickness(0,10,0,0)};actor.SelectedItem=actors.Contains(model)?model:actors.Contains(ObjectList.SelectedItem as string)?ObjectList.SelectedItem:actors.FirstOrDefault();panel.Children.Add(actor);
        var help=new TextBlock {Margin=new Thickness(0,12,0,8),TextWrapping=TextWrapping.Wrap};panel.Children.Add(help);
        void Refresh(){actor.IsEnabled=type.SelectedIndex==0;help.Text=type.SelectedIndex==0?(actors.Length==0?"Voeg eerst een personage aan de scène toe.":"Kies wie spreekt. De mond volgt het volume van de spraak, ook in de MP4."):"Dit bestand komt op het muziekspoor en stuurt geen mond aan.";}
        type.SelectionChanged+=(_,_)=>Refresh();Refresh();var accept=new Button {Content="✓ Deze keuze gebruiken",Padding=new Thickness(10)};panel.Children.Add(accept);accept.Click+=(_,_)=>{if(type.SelectedIndex==0&&actor.SelectedItem is null){Refresh();return;}window.DialogResult=true;};
        if(window.ShowDialog()!=true)return null;
        if(type.SelectedIndex==0){_preferences.LipSync=true;Directory.CreateDirectory(Path.GetDirectoryName(PreferencesPath)!);File.WriteAllText(PreferencesPath,JsonSerializer.Serialize(_preferences));}
        return(type.SelectedIndex==0?"voice":"music",type.SelectedIndex==0?actor.SelectedItem!.ToString()!:"");
    }
    private void OwnAudioVoice_Click(object sender,RoutedEventArgs e)
    {
        if(AudioList.SelectedItem is not AudioEntry selected){StatusText.Text="Selecteer eerst een eigen audiobestand.";return;}
        var imported=ReadImportedAudio();var item=imported.FirstOrDefault(i=>i.File==selected.file);if(item is null)return;
        var choice=ChooseAudioUsage(item.Name,item.Category,item.Model);if(choice is null)return;item.Category=choice.Value.Category;item.Model=choice.Value.Model;
        File.WriteAllText(Path.Combine(UserAudioDirectory,"manifest.json"),JsonSerializer.Serialize(imported));ShowImportedAudio();AudioList.SelectedItem=AudioList.Items.OfType<AudioEntry>().FirstOrDefault(a=>a.file==item.File);
        StatusText.Text="Gebruik bijgewerkt voor nieuwe fragmenten. Bestaande fragmenten op de tijdlijn blijven zoals ze waren.";
    }
    private async void ImportAudio_Click(object sender,RoutedEventArgs e)
    {
        var dialog=new OpenFileDialog {Title=StudioLocalization.Translate("Eigen audio kiezen"),Filter=$"{StudioLocalization.Translate("Geluidsbestanden")}|*.mp3;*.wav;*.m4a;*.aac;*.flac;*.ogg;*.wma;*.aiff;*.aif|{StudioLocalization.Translate("Alle bestanden")}|*.*",Multiselect=true};
        if(dialog.ShowDialog(this)!=true)return;
        var choice=ChooseAudioUsage(dialog.FileNames.Length==1?Path.GetFileName(dialog.FileName):$"{dialog.FileNames.Length} audiobestanden");if(choice is null)return;
        ImportAudioButton.IsEnabled=false;var imported=ReadImportedAudio();Directory.CreateDirectory(UserAudioDirectory);
        try
        {
            foreach(var source in dialog.FileNames)
            {
                StatusText.Text="Audio importeren: "+Path.GetFileName(source);
                string hash;using(var sourceStream=File.OpenRead(source))hash=Convert.ToHexString(await SHA256.HashDataAsync(sourceStream))[..24].ToLowerInvariant();
                var file=hash+".wav";var destination=Path.Combine(UserAudioDirectory,file);
                if(!File.Exists(destination))
                {
                    var staging=Path.Combine(UserAudioDirectory,hash+".part.wav");
                    var ffmpeg=Path.Combine(AppContext.BaseDirectory,"Encoder","ffmpeg.exe");
                    var info=new ProcessStartInfo(ffmpeg){UseShellExecute=false,CreateNoWindow=true,RedirectStandardError=true};foreach(var arg in new[]{"-hide_banner","-loglevel","error","-y","-i",source,"-vn","-ac","2","-ar","48000","-c:a","pcm_s16le",staging})info.ArgumentList.Add(arg);
                    try{using var process=Process.Start(info)??throw new IOException("De audio-converter kon niet starten.");var errors=process.StandardError.ReadToEndAsync();await process.WaitForExitAsync();if(process.ExitCode!=0)throw new IOException("Audio-conversie mislukt: "+await errors);File.Move(staging,destination,true);}
                    finally{if(File.Exists(staging))File.Delete(staging);}
                }
                var reference="UserAudio/"+file;var name=Path.GetFileNameWithoutExtension(source);
                var item=imported.FirstOrDefault(a=>a.File==reference);
                if(item is null){item=new ImportedAudio {Name=name,File=reference,LipData=AudioMouthEnvelope(destination)};imported.Add(item);}
                else item.Name=name;
                item.Category=choice.Value.Category;item.Model=choice.Value.Model;item.EnvelopeVersion=2;
                File.WriteAllText(Path.Combine(UserAudioDirectory,"manifest.json"),JsonSerializer.Serialize(imported,new JsonSerializerOptions {WriteIndented=true}));
            }
            ShowImportedAudio();StatusText.Text=$"{dialog.FileNames.Length} eigen audiobestand(en) geïmporteerd. Sleep ze naar de tijdlijn.";
        }
        catch(Exception error){MessageBox.Show(this,StudioLocalization.Translate(error.Message),StudioLocalization.Translate("Audio importeren"),MessageBoxButton.OK,MessageBoxImage.Error);StatusText.Text="Importeren niet voltooid: "+error.Message;}
        finally{ImportAudioButton.IsEnabled=true;}
    }
    private static string AudioMouthEnvelope(string path)
    {
        using var stream=File.OpenRead(path);using var reader=new BinaryReader(stream);if(new string(reader.ReadChars(4))!="RIFF")return "";reader.ReadUInt32();if(new string(reader.ReadChars(4))!="WAVE")return "";
        int channels=2,sampleRate=48000,bits=16;long dataStart=0;int dataLength=0;
        while(stream.Position+8<=stream.Length)
        {var id=new string(reader.ReadChars(4));var length=reader.ReadInt32();var next=stream.Position+length+(length&1);if(id=="fmt "){var format=reader.ReadInt16();channels=reader.ReadInt16();sampleRate=reader.ReadInt32();reader.ReadInt32();reader.ReadInt16();bits=reader.ReadInt16();if(format!=1)return "";}else if(id=="data"){dataStart=stream.Position;dataLength=length;break;}stream.Position=next;}
        if(dataLength<=0||bits!=16||channels<=0||sampleRate<=0)return "";
        var count=(int)Math.Ceiling(dataLength/(double)(sampleRate*channels*2)*30);if(count>30*60*30)return "";
        var envelope=new char[count];var levels=new double[count];var samplesPerFrame=Math.Max(1,sampleRate/30);stream.Position=dataStart;
        for(var frame=0;frame<count;frame++)
        {double power=0;var sampled=0;for(var sample=0;sample<samplesPerFrame&&stream.Position+2*channels<=dataStart+dataLength;sample++){double mixed=0;for(var channel=0;channel<channels;channel++){var value=reader.ReadInt16()/32768d;mixed+=value*value;}power+=mixed/channels;sampled++;}levels[frame]=sampled>0?Math.Sqrt(power/sampled):0;}
        var ordered=levels.OrderBy(v=>v).ToArray();var reference=Math.Max(.025,ordered[Math.Min(count-1,(int)(count*.9))]);double smoothed=0;
        for(var frame=0;frame<count;frame++){var target=levels[frame]<.005?0:Math.Clamp(levels[frame]/reference,0,1);smoothed+= (target-smoothed)*(target>smoothed?.7:.45);if(target==0&&smoothed<.13)smoothed=0;envelope[frame]=(char)('1'+Math.Clamp((int)Math.Round(smoothed*7),0,7));}
        return new string(envelope);
    }
}
