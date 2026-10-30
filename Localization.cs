using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace ToonTwisterNative;

/// <summary>Applies the language selected by the setup program to native WPF UI text.</summary>
internal static class StudioLocalization
{
    private static readonly bool EnglishSelected = ReadLanguagePreference();
    private static readonly Dictionary<string, string> ExtraEnglish = new TranslationMap(StringComparer.Ordinal)
    {
        ["＋  Aan scène toevoegen"] = "＋  Add to scene",
        ["Voorbeeldfout:"] = "Preview error:", ["Stem"] = "Voice", ["Muziek"] = "Music", ["Effecten"] = "Effects", ["Kies een acteur"] = "Choose a character", ["Acties"] = "Actions",
        ["Sleep om te verplaatsen. Sleep een rand om bij te snijden of een herhaling te verlengen."] = "Drag to move. Drag an edge to trim or extend a loop.",
        ["Voeg eerst een acteur toe en sleep de actie naar zijn spoor."] = "Add a character first, then drag the action to its track.",
        ["✂ Knippen aan (V)"] = "✂ Cut mode (V)", ["Klik op een fragment om daar te knippen. V of Esc schakelt terug naar slepen."] = "Click a clip to cut it. Press V or Esc to return to drag mode.",
        ["Sleep fragmenten of hun randen. Spatie: afspelen/pauze."] = "Drag clips or their edges. Space: play/pause.", ["Knip minimaal 0,1 seconde van de rand."] = "Trim at least 0.1 seconds from the edge.",
        ["Fragment geknipt; beide delen zijn apart verplaatsbaar. Ctrl+Z: ongedaan."] = "Clip cut; both parts can be moved separately. Ctrl+Z to undo.",
        ["BEELD · jouw studio"] = "VIDEO · your studio", ["Snel — lichtere textuurfiltering"] = "Fast — lighter texture filtering", ["Mooi — vloeiende textuurfiltering"] = "High quality — smooth texture filtering",
        ["15 fps — zuinig"] = "15 FPS — efficient", ["30 fps — normaal"] = "30 FPS — standard", ["60 fps — vloeiend"] = "60 FPS — smooth",
        ["Spraak — mond beweegt mee"] = "Voice — animate mouth", ["Achtergrondmuziek — geen mondbeweging"] = "Background music — no lip sync",
        ["Hele film — alle scènes"] = "Full movie — all scenes", ["Alleen deze scène"] = "This scene only", ["HD — 720 pixels hoog"] = "HD — 720 pixels high", ["Full HD — 1080 pixels hoog"] = "Full HD — 1080 pixels high",
        ["30 beelden per seconde. De beeldverhouding van elke\nscène blijft behouden; afwijkende scènes krijgen randen."] = "30 frames per second. Each scene keeps its aspect ratio; other aspect ratios are letterboxed.",
        ["MP4-export geannuleerd. Je project is behouden."] = "MP4 export cancelled. Your project is preserved.", ["MP4 afronden…"] = "Finalizing MP4…", ["De meegeleverde MP4-encoder ontbreekt. Start vanuit de volledige Native Editor-map."] = "The included MP4 encoder is missing. Run the app from the complete Native Editor folder.",
        ["Geluidsbestand ontbreekt"] = "Audio file not found", ["De MP4-encoder kon niet starten."] = "The MP4 encoder could not start.", ["Video-encoder: "] = "Video encoder: ",
        ["Eigen audio · gebruik"] = "Custom audio · usage", ["Geluidsbestanden"] = "Audio files", ["Gebruik bijgewerkt voor nieuwe fragmenten. Bestaande fragmenten op de tijdlijn blijven zoals ze waren."] = "Usage updated for new clips. Existing timeline clips are unchanged.",
        ["Audio importeren"] = "Import audio", ["Audio importeren: "] = "Importing audio: ", ["Importeren niet voltooid: "] = "Import did not finish: ", ["Eigen audio kiezen"] = "Choose custom audio",
        ["Donkere studio-omlijsting"] = "Dark studio frame", ["GELUID · afluisteren"] = "AUDIO · preview", ["Volume bij afspelen en beluisteren; MP4 behoudt het originele volume."] = "Playback and preview volume; MP4 keeps the original volume.",
        ["Automatische mondbeweging bij spraak"] = "Automatic lip sync for speech", ["✓ Toepassen en bewaren"] = "✓ Apply and save", ["Instellingen bewaren"] = "Save settings",
        ["Film exporteren als MP4"] = "Export movie as MP4", ["MP4 · H.264 beeld + AAC geluid"] = "MP4 · H.264 video + AAC audio", ["Kies bestand en exporteer"] = "Choose file and export", ["MP4 maken"] = "Creating MP4", ["Scènes voorbereiden…"] = "Preparing scenes…", ["Export annuleren…"] = "Cancelling export…",
        ["Niet genoeg vrije ruimte op dit spoor."] = "There is not enough free space on this track.", ["Project kon niet worden geopend"] = "Could not open project", ["Project kon niet worden opgeslagen"] = "Could not save project", ["OBJ-import mislukt"] = "OBJ import failed",
        ["Stuiteren"] = "Bounce", ["Rondje draaien"] = "Spin", ["Heen en weer dansen"] = "Dance side to side", ["Vrolijk buigen"] = "Cheerful bow", ["Floss-achtige dans"] = "Floss-style dance", ["Orange-Justice-achtige chaos"] = "Orange Justice-style chaos", ["Robotdans"] = "Robot dance", ["Overdreven L-dans"] = "Over-the-top L dance", ["De worm"] = "The worm", ["Dansramp"] = "Dance disaster", ["Spaghetti-benen"] = "Spaghetti legs", ["Marionet"] = "Puppet", ["Disco zijstap"] = "Disco sidestep", ["Feestsprongen"] = "Party jumps", ["Stiekeme disco"] = "Sneaky disco",
        ["1. Kies een set. 2. Plaats acteurs en props. 3. Ga naar Direct voor echte personageacties."] = "1. Choose a background. 2. Add characters and props. 3. Open Direct for character actions.",
        ["Kies je set, acteurs en props"] = "Choose a background, characters, and props", ["Kies een acteur en geef hem een actie"] = "Choose a character and assign an action", ["Bekijk je film en sla je project op"] = "Preview your movie and save your project",
        ["Nieuw project gestart."] = "New project started.", ["Geen assets gevonden. Probeer een andere zoekterm."] = "No assets found. Try another search term.", ["Dit model kon niet worden getoond."] = "This model could not be previewed.", ["Selecteer eerst een asset."] = "Select an asset first.",
        ["Voor dit bestand is geen 3D-modelconversie beschikbaar."] = "No 3D model conversion is available for this file.", ["Dit model staat al in de scène."] = "This model is already in the scene.", ["Afspelen voltooid."] = "Playback complete.", ["Camera slepen: draai het beeld."] = "Drag camera: orbit view.", ["Camera slepen: verschuif het beeld."] = "Drag camera: pan view.",
        ["Kopie gemaakt. Sleep het model om het te verplaatsen."] = "Duplicate created. Drag the model to move it.", ["Voeg eerst een model toe en selecteer dat in de scène."] = "Add a model to the scene and select it first.", ["Nog geen keyframes"] = "No keyframes yet", ["Vul numerieke waarden in."] = "Enter numeric values.", ["Scèneduur: vul 1 tot 300 seconden in, of Auto."] = "Scene length: enter 1–300 seconds, or Auto.",
        ["Deze acteur heeft die actie niet. Kies een actie uit zijn eigen menu."] = "This character does not have that action. Choose one from its own menu.", ["Klik eerst op een camerashot in de tijdlijn."] = "Select a camera shot on the timeline first.", ["Camerashot verwijderd."] = "Camera shot deleted.", ["Scèneduur volgt automatisch je acties en geluid."] = "Scene length follows your actions and audio automatically.",
        ["Scène"] = "Scene", ["Scène 1 / 1"] = "Scene 1 / 1", ["Dikte 2,5%"] = "Thickness 2.5%", ["＋ Camerashot"] = "＋ Camera shot", ["Shot bijwerken"] = "Update shot", ["Shot wissen"] = "Delete shot", ["MP4 exporteren"] = "Export MP4", ["21:9 ultrawide"] = "21:9 ultrawide", ["16:9 origineel"] = "16:9 original", ["4:3 klassiek"] = "4:3 classic"
        , ["Project kon niet worden geopend"] = "Could not open project", ["Project kon niet worden opgeslagen"] = "Could not save project", ["OBJ-import mislukt"] = "OBJ import failed", ["Instellingen bewaren"] = "Save settings", ["Audio importeren"] = "Import audio", ["MP4-export"] = "MP4 export", ["Geluidsbestanden"] = "Audio files", ["Alle bestanden"] = "All files"
    };

    private static readonly Dictionary<string, string> English = new TranslationMap(StringComparer.Ordinal)
    {
        ["Nieuw"]="New", ["Openen"]="Open", ["Opslaan"]="Save", ["MP4 exporteren"]="Export MP4", ["▶ Film afspelen"]="▶ Play movie", ["⚙ Instellingen"]="⚙ Settings", ["Indeling"]="Layout",
        ["1  Prep"]="1  Prep", ["2  Direct"]="2  Direct", ["3  Edit"]="3  Edit", ["＋ Scène"]="＋ Scene", ["Scène 1 / 1"]="Scene 1 / 1",
        ["IN JE SCÈNE"]="IN YOUR SCENE", ["＋ OBJ importeren"]="＋ Import OBJ", ["Bibliotheek"]="Library", ["Camera reset"]="Reset camera", [" · Sleep acteur: verplaats · Rechts slepen: camera · Wiel: zoom"]=" · Drag actor: move · Right-drag: camera · Wheel: zoom",
        ["GESELECTEERD"]="SELECTED", ["Kies een acteur of prop"]="Choose a character or prop", ["Links / rechts"]="Left / right", ["Hoogte"]="Height", ["Diepte"]="Depth", ["Draaien"]="Rotation", ["Grootte"]="Size", ["Zwarte contour"]="Black outline", ["Beeldformaat"]="Aspect ratio", ["21:9 ultrawide"]="21:9 ultrawide", ["16:9 origineel"]="16:9 original", ["4:3"]="4:3",
        ["In beeld zetten"]="Focus selection", ["Kopie"]="Duplicate", ["Wissen"]="Delete", ["Sets / decors"]="Sets / backgrounds", ["Actors / personages"]="Actors / characters", ["Props / voorwerpen"]="Props / objects", ["Zoek een naam"]="Search by name", ["Kies een decor of personage"]="Choose a background or character", ["✓ Plaats in scène"]="✓ Add to scene",
        ["Act — acties"]="Act — actions", ["Talk — stem"]="Talk — voice", ["Music — muziek"]="Music — music", ["SFX — effecten"]="SFX — effects", ["Eigen audio"]="Custom audio", ["ACT — originele acties"]="ACT — original actions", ["Walks — lopen"]="Walks", ["Moods — emoties"]="Moods", ["Actions — acties"]="Actions", ["Studio — nieuwe bewegingen"]="Studio — new moves", ["＋ Actie op tijdlijn"]="＋ Add action to timeline", ["＋ Eigen audio kiezen…"]="＋ Choose custom audio…", ["Spraak / muziek…"]="Voice / music…", ["▶ Beluisteren"]="▶ Preview", ["＋ Geluid op tijdlijn"]="＋ Add sound to timeline",
        ["TIJDLIJN"]="TIMELINE", ["▶ Afspelen"]="▶ Play", ["✂ Knippen (V)"]="✂ Cut (V)", ["＋ Positie"]="＋ Position", ["＋ Camerashot"]="＋ Camera shot", ["Shot bijwerken"]="Update shot", ["Shot wissen"]="Delete shot", ["Scèneduur"]="Scene length", ["Vastklikken 0,1 s"]="Snap 0.1 s", ["Zoom"]="Zoom", ["Sleep acties/geluid hierheen · Randen slepen = bijsnijden"]="Drag actions/audio here · Drag edges to trim",
        ["Project opslaan (Ctrl+S)"]="Save project (Ctrl+S)", ["Contourdikte als percentage van de modelgrootte"]="Outline thickness as a percentage of model size", ["Sleep om het paneel breder of smaller te maken"]="Drag to resize the panel", ["Zoek een naam"]="Search by name", ["Sleep een actie naar het spoor van de acteur. Klik om de actie te bekijken."]="Drag an action to the character's track. Click to preview it.", ["Duur (s), bij herhalen"]="Duration (s), loop", ["✓"]="✓",
        ["Nieuw project gestart."]="New project started.", ["Geen assets gevonden. Probeer een andere zoekterm."]="No assets found. Try another search term.", ["Dit model kon niet worden getoond."]="This model could not be previewed.", ["Selecteer eerst een asset."]="Select an asset first.", ["Voor dit bestand is geen 3D-modelconversie beschikbaar."]="No 3D model conversion is available for this file.", ["Dit model staat al in de scène."]="This model is already in the scene.", ["Kies eerst een decor en voeg een personage toe."]="Choose a background and add a character first.", ["Afspelen voltooid."]="Playback complete.", ["Camera slepen: draai het beeld."]="Drag camera: orbit view.", ["Camera slepen: verschuif het beeld."]="Drag camera: pan view.", ["Kopie gemaakt. Sleep het model om het te verplaatsen."]="Duplicate created. Drag the model to move it.", ["Voeg eerst een model toe en selecteer dat in de scène."]="Add a model to the scene and select it first.", ["Nog geen keyframes"]="No keyframes yet", ["Vul numerieke waarden in."]="Enter numeric values.", ["Scèneduur: vul 1 tot 300 seconden in, of Auto."]="Scene length: enter 1–300 seconds, or Auto.", ["Scèneduur volgt automatisch je acties en geluid."]="Scene length follows your actions and audio automatically.", ["Zwarte contour"]="Black outline", ["Aan scène toevoegen"]="Add to scene", ["Alleen 3D-modellen toevoegen"]="Only 3D models can be added",
        ["nickelodeon  TOON TWISTER 3-D"]="nickelodeon  TOON TWISTER 3-D", ["Actors"]="Characters", ["Sets"]="Backgrounds", ["Props"]="Props", ["Scène"]="Scene", ["Scèneduur"]="Scene length", ["Contourdikte als percentage van de modelgrootte"]="Outline thickness as a percentage of model size", ["Sleep om het paneel breder of smaller te maken"]="Drag to resize the panel", ["Sleep om de panelen hoger of lager te maken"]="Drag to resize the panels", ["Zoek een naam"]="Search by name", ["Bewaar de huidige camerastand op de positie van de afspeelkop"]="Save the current camera view at the playhead", ["Vervang het gekozen shot door de huidige camerastand"]="Replace the selected shot with the current camera view", ["Ongedaan maken (Ctrl+Z)"]="Undo (Ctrl+Z)", ["Opnieuw (Ctrl+Y)"]="Redo (Ctrl+Y)", ["Scèneduur toepassen"]="Apply scene length", ["Schuif om de lengte van deze scène in seconden te kiezen"]="Choose this scene's length in seconds", ["Scèneduur in seconden (1–300)"]="Scene length in seconds (1–300)", ["Afspelen/pauze (Spatie)"]="Play/pause (Space)", ["V: schaar. Klik op het fragment. Esc: weer slepen."]="V: cut mode. Click a clip. Esc: return to drag mode.", ["Kies het gebruik en het sprekende personage voor dit bestand."]="Choose how to use this file and which character speaks.", ["Sleep een actie naar het spoor van de acteur. Klik om de actie te bekijken."]="Drag an action to the character's track. Click to preview it.", ["Duur (s), bij herhalen"]="Duration (s), loop", ["Bekijk je film en de acties hieronder. Sla de scène met acties en bewegingen op met Opslaan."]="Preview your movie and its actions below. Save the scene, actions, and movements with Save.", ["＋ Eigen audio kiezen…"]="＋ Choose custom audio…", ["＋ Geluid op tijdlijn"]="＋ Add audio to timeline", ["Spraak / muziek…"]="Voice / music…", ["▶ Bekijk film"]="▶ Preview movie", ["Shot wissen"]="Delete shot", ["Shot bijwerken"]="Update shot", ["MP4 exporteren"]="Export MP4", ["Duur (s), bij herhalen"]="Duration (s), loop", ["4:3 klassiek"]="4:3 classic", ["21:9 ultrawide"]="21:9 ultrawide",
        ["1. Kies een set. 2. Plaats acteurs en props. 3. Ga naar Direct voor echte personageacties."]="1. Choose a background. 2. Add characters and props. 3. Open Direct for character actions.", ["Kies je set, acteurs en props"]="Choose a background, characters, and props", ["Kies een acteur en geef hem een actie"]="Choose a character and assign an action", ["Bekijk je film en sla je project op"]="Preview your movie and save your project", ["Kies je decor, acteurs en props"]="Choose a background, characters, and props", ["Kies een acteur en geef hem een actie"]="Choose a character and assign an action",
        ["Voeg eerst een personage aan de scène toe."]="Add a character to the scene first.", ["Selecteer links eerst een acteur en kies daarna een actie."]="Select a character on the left, then choose an action.", ["Dit object heeft geen geconverteerde skeletanimatie."]="This object has no converted skeletal animation.", ["Er is onvoldoende vrije ruimte op dit spoor voor deze actie."]="There is not enough free space on this track for this action.", ["Klik Afspelen om je scène te bekijken."]="Click Play to preview your scene.", ["Dikte 2,5%"]="Thickness 2.5%", ["Scène 1 / 1"]="Scene 1 / 1", ["21:9 ultrawide"]="21:9 ultrawide", ["16:9 origineel"]="16:9 original", ["4:3 klassiek"]="4:3 classic",
        ["Selecteer links eerst een acteur en kies daarna een actie."]="Select a character on the left, then choose an action.", ["Dit object heeft geen geconverteerde skeletanimatie."]="This object has no converted skeletal animation.", ["Er is onvoldoende vrije ruimte op dit spoor voor deze actie."]="There is not enough free space on this track for this action.", ["Klik Afspelen om je scène te bekijken."]="Click Play to preview your scene.", ["Eigen audio · gebruik"]="Custom audio · usage", ["Voeg eerst een personage aan de scène toe."]="Add a character to the scene first.", ["Kies wie spreekt. De mond volgt het volume van de spraak, ook in de MP4."]="Choose who speaks. The mouth follows the voice volume, including in the MP4.", ["Dit bestand komt op het muziekspoor en stuurt geen mond aan."]="This file goes on the music track and does not animate a mouth.", ["✓ Deze keuze gebruiken"]="✓ Use this choice", ["Selecteer eerst een eigen audiobestand."]="Select a custom audio file first.", ["Eigen audio kiezen"]="Choose custom audio", ["Alle bestanden"]="All files", ["Geluidbestanden"]="Audio files", ["Film exporteren als MP4"]="Export movie as MP4", ["MP4 · H.264 beeld + AAC geluid"]="MP4 · H.264 video + AAC audio", ["Kies bestand en exporteer"]="Choose file and export", ["MP4 maken"]="Creating MP4", ["Scènes voorbereiden…"]="Preparing scenes…", ["Annuleren"]="Cancel", ["Export annuleren…"]="Cancelling export…", ["Maak eerst een scène om te exporteren."]="Create a scene before exporting.", ["Studio-instellingen"]="Studio settings", ["Donkere studio-omlijsting"]="Dark studio frame", ["GELUID · afluisteren"]="AUDIO · preview", ["Volume bij afspelen en beluisteren; MP4 behoudt het originele volume."]="Playback and preview volume; MP4 keeps the original volume.", ["Automatische mondbeweging bij spraak"]="Automatic lip sync for speech", ["✓ Toepassen en bewaren"]="✓ Apply and save", ["Beeldkwaliteit en fps gelden voor het werkvenster.\nMP4 wordt met 30 fps geëxporteerd."]="Preview quality and FPS apply to the editor.\nMP4 exports at 30 FPS.", ["Voeg eerst een model toe en selecteer dat in de scène."]="Add a model to the scene and select it first.", ["Fragment verwijderen"]="Remove clip", ["Fragment aangepast. Langer dan de bron = herhalen. Ctrl+Z maakt dit ongedaan."]="Clip updated. Extending past the source loops it. Ctrl+Z to undo.", ["Hier overlappen twee acties van dezelfde acteur. Het fragment staat weer op zijn vorige plek."]="Two actions for this character overlap here. The clip was returned to its previous position.", ["Het cameraspoor is voor camerashots. Voeg een shot toe met ＋ Camerashot."]="The camera track is for camera shots. Add one with ＋ Camera shot.", ["Deze acteur heeft die actie niet. Kies een actie uit zijn eigen menu."]="This character does not have that action. Choose one from its own menu.", ["Klik eerst op een camerashot in de tijdlijn."]="Select a camera shot on the timeline first.", ["Camerashot verwijderd."]="Camera shot deleted.", ["Volume: "]="Volume: "
    };

    public static bool IsEnglish
    {
        get => EnglishSelected;
    }

    private static bool ReadLanguagePreference()
    {
        try { return File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "studio-language.txt")).Trim().Equals("en", StringComparison.OrdinalIgnoreCase); }
        catch { return false; }
    }

    static StudioLocalization()
    {
        foreach (var pair in ExtraEnglish) English[pair.Key] = pair.Value;
    }

    public static void Apply(Window window)
    {
        if (!IsEnglish) return;
        if (English.TryGetValue(window.Title, out var title)) window.Title = title;
        Visit(window);
    }

    private static void Visit(DependencyObject node)
    {
        if (node is TextBlock text) text.Text = Translate(text.Text);
        if (node is ContentControl content && content.Content is string contentText) content.Content = Translate(contentText);
        if (node is HeaderedContentControl header && header.Header is string headerText) header.Header = Translate(headerText);
        if (node is FrameworkElement element && element.ToolTip is string toolTip) element.ToolTip = Translate(toolTip);
        if (node is ItemsControl items && items.ItemsSource is IEnumerable<string> source)
        {
            var original = source.ToArray();
            var translated = original.Select(Translate).ToArray();
            if (!original.SequenceEqual(translated, StringComparer.Ordinal)) items.ItemsSource = translated;
        }
        if (node is ItemsControl controls)
            foreach (var item in controls.Items)
                if (item is DependencyObject itemNode) Visit(itemNode);
        foreach (var child in LogicalTreeHelper.GetChildren(node))
            if (child is DependencyObject dependency) Visit(dependency);
    }

    internal static string Translate(string value)
    {
        if (!IsEnglish) return value;
        if (English.TryGetValue(value, out var exact)) return exact;
        if (value.StartsWith("Dikte ", StringComparison.Ordinal)) return "Thickness " + value[6..];
        if (value.StartsWith("Scène ", StringComparison.Ordinal)) return "Scene " + value[6..];
        if (value.StartsWith("Geselecteerd: ", StringComparison.Ordinal)) return "Selected: " + value[14..];
        if (value.StartsWith("Toegevoegd: ", StringComparison.Ordinal)) return "Added: " + value[12..];
        if (value.StartsWith("Echt model toegevoegd aan de scène: ", StringComparison.Ordinal)) return "Model added to the scene: " + value[36..];
        if (value.StartsWith("Voorbeeld geladen: ", StringComparison.Ordinal)) return "Preview loaded: " + value[19..];
        if (value.StartsWith("Model geïmporteerd: ", StringComparison.Ordinal)) return "Model imported: " + value[20..];
        if (value.StartsWith("MP4 opgeslagen: ", StringComparison.Ordinal)) return "MP4 saved: " + value[16..];
        if (value.StartsWith("MP4-export mislukt: ", StringComparison.Ordinal)) return "MP4 export failed: " + value[20..];
        if (value.StartsWith("Project geladen: ", StringComparison.Ordinal)) return "Project loaded: " + value[17..];
        if (value.StartsWith("Opgeslagen: ", StringComparison.Ordinal)) return "Saved: " + value[12..];
        if (value.StartsWith("Volume: ", StringComparison.Ordinal)) return "Volume: " + value[8..];
        if (value.StartsWith("Audio importeren: ", StringComparison.Ordinal)) return "Importing audio: " + value[18..];
        if (value.StartsWith("Importeren niet voltooid: ", StringComparison.Ordinal)) return "Import did not finish: " + value[26..];
        if (value.EndsWith(" eigen audiobestand(en) geïmporteerd. Sleep ze naar de tijdlijn.", StringComparison.Ordinal)) return value.Replace(" eigen audiobestand(en) geïmporteerd. Sleep ze naar de tijdlijn.", " custom audio file(s) imported. Drag them to the timeline.", StringComparison.Ordinal);
        if (value.StartsWith("Keyframe gezet op ", StringComparison.Ordinal)) return "Keyframe set at " + value[18..].Replace(" s voor ", " s for ", StringComparison.Ordinal);
        if (value.StartsWith("Transformatie bijgewerkt: ", StringComparison.Ordinal)) return "Transform updated: " + value[26..];
        if (value.StartsWith("Deze scène duurt ", StringComparison.Ordinal)) return "This scene is " + value[17..];
        if (value.StartsWith("Model verplaatst naar X ", StringComparison.Ordinal)) return value.Replace("Model verplaatst naar X ", "Model moved to X ", StringComparison.Ordinal);
        if (value.StartsWith("Model verwijderd: ", StringComparison.Ordinal)) return "Model deleted: " + value[18..];
        if (value.StartsWith("Loslaten om het fragment op ", StringComparison.Ordinal)) return value.Replace("Loslaten om het fragment op ", "Release to place the clip at ", StringComparison.Ordinal).Replace(" s te plaatsen.", " s.", StringComparison.Ordinal);
        if (value.Contains(" · Acties", StringComparison.Ordinal)) return value.Replace(" · Acties", " · Actions", StringComparison.Ordinal);
        if (value.Contains(" · Stem", StringComparison.Ordinal)) return value.Replace(" · Stem", " · Voice", StringComparison.Ordinal);
        if (value.StartsWith("♫ Muziek", StringComparison.Ordinal)) return value.Replace("♫ Muziek", "♫ Music", StringComparison.Ordinal);
        if (value.StartsWith("✦ Effecten", StringComparison.Ordinal)) return value.Replace("✦ Effecten", "✦ Effects", StringComparison.Ordinal);
        if (value.StartsWith("Spraak · ", StringComparison.Ordinal)) return value.Replace("Spraak · ", "Voice · ", StringComparison.Ordinal);
        if (value.StartsWith("Muziek · ", StringComparison.Ordinal)) return value.Replace("Muziek · ", "Music · ", StringComparison.Ordinal);
        return value;
    }
}

// Collection initializers call Add; assigning here intentionally lets a later
// wording refinement replace an earlier translation without breaking startup.
internal sealed class TranslationMap : Dictionary<string, string>
{
    public TranslationMap(IEqualityComparer<string> comparer) : base(comparer) { }
    public new void Add(string key, string value) => this[key] = value;
}

internal static class StudioLocalizationExtensions
{
    public static void StartStudioLocalization(this Window window)
    {
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        timer.Tick += (_, _) =>
        {
            foreach (Window openWindow in Application.Current.Windows)
                StudioLocalization.Apply(openWindow);
        };
        window.Closed += (_, _) => timer.Stop();
        timer.Start();
        window.Loaded += (_, _) => StudioLocalization.Apply(window);
    }
}
