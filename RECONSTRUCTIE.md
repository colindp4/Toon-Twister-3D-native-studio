# Reconstructie op basis van de originele bestanden

## Vastgestelde fouten en reparaties

- De oude OBJ-export spiegelde CLOD-U en de WPF-import keerde V nogmaals om. De native W3D-route gebruikt nu dezelfde UV-conventie als de W3D-renderer en een absolute 0..1 texture viewport.
- BONES_BLOCK (0xFFFFFF4B) bevat legacy uitbreidingen per bot. De oude parser las die als de volgende naam en gooide het hele skelet weg. De nieuwe route leest de vaste botheader en zoekt de volgende gevalideerde header: ASCII-naam, bestaande ouder, eindige waarden en genormaliseerde quaternion. Alle verwachte botrecords moeten aanwezig zijn. Dit is een gevalideerde aanpak voor deze assets; de onbekende uitbreidingen zijn nog geen volledige IFX-specificatieimplementatie.
- Timmy heeft 76 botten. De oorspronkelijke motion-tracks worden nu met de inverse bindmatrices naar een native vertexanimatiecache omgerekend. De cache behoudt dezelfde vertexvolgorde als de OBJ. Onzichtbare helperresources zonder zichtbare modelnode worden weggelaten.
- Alle originele modellen worden met dezelfde cm-naar-native factor 0,01 geplaatst. Het eerdere onafhankelijk normaliseren van ieder model/decor maakte hun onderlinge maten onjuist.
- De decorcamera komt uit standard_cam in de scènehiërarchie. De IFX-Z-up basis wordt omgezet naar Y-up. De opgeslagen fieldOfView is de verticale hoek. Het oorspronkelijke verticale beeld blijft behouden bij ultrawide.
- Zichtbare modelvoorzijden gebruiken de juiste triangle winding; de oorspronkelijke decors blijven dubbelzijdig waar nodig. Actoren gebruiken diffuse texturen met neutraal omgevingslicht.

## Bronnen in deze installatie

- Guide/prep.htm, direct.htm, giving-action.htm, giving-dialogue.htm, edit.htm en de screenshots NTT_03/NTT_06.
- _projectorrays-analysis/decompiled/GPL/casts/External/MovieScript 1 - SHARED.ls: gpl_init3d, gpl_loadbg, gpl_loadchar, gpl_loadfile, getcharnumber en gpl_settransform.
- Nickelodeon Toon Twister 3-D/xml/animlist.lst: de echte categorieën, labels, bronbereiken in milliseconden en loopvlaggen.
- xml/audiodata.lst: dialoogkeuze per personage en oorspronkelijke tekstlabels.
- w3d/*.w3d en bg/*.w3d: meshes, shaders, texturen, nodes, skeletten en motion-tracks.
- Music.cxt, sfx.cxt en dlg.cxt: oorspronkelijke muziek, effecten en spraak. De Director-parser heeft deze naar losse WAV/MP3-bestanden geëxporteerd.
- _u3d-reference is een lokale referentiekopie van https://github.com/ningfei/u3d voor vergelijking van de IFX/U3D bone-layout. De legacy W3D-layout wijkt daarvan af.

## Nieuwe native functionaliteit

Prep/Direct/Edit, beeldkaarten, personagespecifieke originele acties, loops met instelbare duur, stem/muziek/effecten, geluidspreviews, audio op de tijdlijn, positiekeyframes, acht scènes, filmweergave over scènes en opslaan/heropenen van .ttnative-projecten.

## Controle

Replica/Verification/result.txt wordt geschreven door --render-check. De controle rendert originele Timmy/SpongeBob idle- en Jump-poses, plaatst Jump via het actiemenu, opent een echt MP3-fragment zonder afspelen en bewaart/herstelt twee scènes met actie, geluid en plaatsing. --render-gallery genereert de keuzeplaatjes en meldt fouten apart.

## Openstaande verschillen met het origineel

Stemopname, .tt3-import, overgangen, exacte Flash-menuvormgeving ontbreken nog. Zeven ADPCM-helpstemfragmenten worden niet geëxporteerd. De rig-parser, belichting en alle animatieclips zijn nog niet één voor één tegen een draaiend origineel vergeleken. Deze build is daarom geen volledig bewezen 1:1-replica.

## Editor-update: decors, lipsync en directe tijdlijn

- IFX-JPEG-continuations bevatten achter de JPEG-EOI een little-endian breedte/hoogte/packed-size, gevolgd door een zlib-gecomprimeerde Gray8-alfa-plane. De DIB-scanlijnen staan ondersteboven ten opzichte van het JPEG. OriginalTextures.cs decodeert en keert die scanlijnen om, behoudt RGB en rendert met DiffuseMaterial. Opaque materialen komen voor de alpha-billboards; de eerdere additive EmissiveMaterial-route is verwijderd.
- native_support::face exporteert per rig inverse bindmatrices, oorspronkelijke vertexgewichten, het model/basis-frame, acht lokale mond-overrides en de geanimeerde bone-world-frames naar TTF1. Nul-bone-ID-entries in Goddards lipstabel zijn placeholders en worden overgeslagen. De overige mondindices zijn Director-bone-indexen vanaf 1.
- NativeFace.cs past mond-overrides op de geanimeerde bone-hiërarchie toe. Gewijzigde ouders voeren hun kinderen mee; de nieuwe skinmatrices worden alleen op mond-gerelateerde vertices toegepast. Body-animaties blijven gelijktijdig actief.
- Talk-fragmenten bewaren acteur, originele lipdata, begin, bronduratie en source offset. Visemes volgen de originele reeks op 30 fps; tijdens afspelen wordt de werkelijke MediaPlayer-positie gebruikt. Verschuiven en trimmen behouden de bronuitlijning. Alle 1.197 dialoogrecords in audiodata.lst hebben lipdata.
- EditableTimeline.cs bevat canvas-sporen, library drag/drop, verplaatsing, rand-trim, loops verlengen, bronoffsets, scrubben, zoom, snapping, conflictcontrole en undo/redo. TimelinePositions.cs toont en bewerkt de positiekeyframes als blauwe ruitjes.
- EditorLayout.cs biedt GridSplitter-indeling, aparte native vensters voor panelen, terugzetten en lokale opslag van formaat/paneelgroottes/zoom. Sneltoetsen blijven ook in losgemaakte vensters actief.
- EditorVerification.cs controleert original-alpha fixtures, verschillende oorspronkelijke mondstanden, alle face-caches, dialogue timing, behoud bij project roundtrip, move/trim, undo/redo, positiepunten en native panel-reparenting. Er is tevens een echte MP3-opencontrole zonder geluidsweergave en een test van automatisch inkorten van lange muziek.

Lipsync is hiermee geïmplementeerd voor de aanwezige originele 3D-personages en dialogen. Openstaand blijven .tt3-import, opname, overgangen, zeven ADPCM-helpfragmenten en volledige visuele pariteit met iedere oorspronkelijke animatie.

`n## Video, sneltoetsen en eigen audio`n`n- Spatie hervat op de huidige tijd, V zet de schaar aan, Esc herstelt slepen, Home gaat naar 0, Ctrl+Z/Y/Shift+Z en Ctrl+S.`n- Randen van actie- en audiofragmenten zijn versleepbaar. Verlenging voorbij de bron activeert herhaling; knippen behoudt de bronoffset per deel.`n- Studio bevat zestien nieuwe choreografieën. Iedere preset gebruikt per acteur een bestaande skeletclip (bijvoorbeeld dance_cycle, walk_back_cycle, crawl_cycle, jump of tantrum_cycle) plus native translatie/rotatie/tilt; de gekozen clip en snelheid worden in .ttnative opgeslagen en ook in MP4 geëvalueerd.`n- VideoExport.cs rendert elke scène op 30 fps in een afzonderlijke WPF viewport en voert BGRA-frames en gemixte audio aan meegeleverde FFmpeg 9.0.2. H.264/AAC MP4 kan HD of Full HD en 21:9/16:9/4:3 volgen. Encoder/LICENSE en Encoder/README.txt leveren de FFmpeg-licentie en bronlink.`n- Eigen MP3/WAV/M4A/AAC/FLAC/OGG/WMA/AIFF-bestanden worden naar PCM WAV onder Assets/UserAudio omgezet. De optionele mondbeweging wordt als amplitude-envelop op 30 fps bewaard.`n

`n## Correctie danspreview en Floss`n`n- EndActionPreview zet tijdelijke X/Z-tilt en uitgangstransformatie terug; snel wisselen tussen Studio-acties laat de acteur niet scheef staan.`n- Floss verschuift de actor in een tegengestelde heupbeweging, beperkt het stuiteren en vervormt voor SpongeBob alleen buitenste armvertices vloeiend met het ritme; originele dance_cycle en gezichtsanimatie blijven intact. Dezelfde posecode wordt in de MP4-render gebruikt.`n
