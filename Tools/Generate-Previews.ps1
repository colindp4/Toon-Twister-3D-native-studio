Add-Type -AssemblyName System.Drawing
$repo = Split-Path -Parent $PSScriptRoot
$out = Join-Path $repo 'screenshots'
New-Item -ItemType Directory -Path $out -Force | Out-Null

function Fill-Round($g, $brush, [float]$x, [float]$y, [float]$w, [float]$h, [float]$r) {
    $p = [Drawing.Drawing2D.GraphicsPath]::new(); $d=$r*2
    $p.AddArc($x,$y,$d,$d,180,90); $p.AddArc($x+$w-$d,$y,$d,$d,270,90)
    $p.AddArc($x+$w-$d,$y+$h-$d,$d,$d,0,90); $p.AddArc($x,$y+$h-$d,$d,$d,90,90); $p.CloseFigure()
    $g.FillPath($brush,$p); $p.Dispose()
}
function Draw-Text($g,[string]$text,[float]$x,[float]$y,[float]$size,[string]$color,[bool]$bold=$false) {
    $style=if($bold){[Drawing.FontStyle]::Bold}else{[Drawing.FontStyle]::Regular}
    $font=[Drawing.Font]::new('Segoe UI',$size,$style,[Drawing.GraphicsUnit]::Pixel)
    $brush=[Drawing.SolidBrush]::new([Drawing.ColorTranslator]::FromHtml($color))
    $g.DrawString($text,$font,$brush,$x,$y); $font.Dispose(); $brush.Dispose()
}
function Draw-Button($g,[string]$text,[float]$x,[float]$y,[float]$w,[string]$fill='#eaf4c7',[string]$ink='#183c32') {
    $brush=[Drawing.SolidBrush]::new([Drawing.ColorTranslator]::FromHtml($fill)); Fill-Round $g $brush $x $y $w 34 8; $brush.Dispose()
    Draw-Text $g $text ($x+12) ($y+8) 16 $ink $true
}
function Draw-Field($g,[string]$label,[string]$value,[float]$x,[float]$y) {
    Draw-Text $g $label $x $y 14 '#365446'
    $b=[Drawing.SolidBrush]::new([Drawing.Color]::White); Fill-Round $g $b ($x+105) ($y-4) 112 27 4; $b.Dispose()
    Draw-Text $g $value ($x+116) ($y+1) 15 '#263744'
}
function New-Preview([bool]$cameraFocus,[string]$path) {
    $bmp=[Drawing.Bitmap]::new(1600,1000,[Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g=[Drawing.Graphics]::FromImage($bmp); $g.SmoothingMode=[Drawing.Drawing2D.SmoothingMode]::AntiAlias; $g.TextRenderingHint=[Drawing.Text.TextRenderingHint]::AntiAliasGridFit
    $g.Clear([Drawing.Color]::FromArgb(17,25,35))
    $chrome=[Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(27,39,57)); Fill-Round $g $chrome 12 12 1576 976 12; $chrome.Dispose()
    # Title and mode bar.
    Draw-Text $g 'TOON TWISTER 3-D' 34 24 22 '#eff6ce' $true
    Draw-Text $g 'NATIVE STUDIO' 260 28 14 '#82c8d0' $true
    Draw-Text $g 'Example project · abstract toy scene' 620 29 15 '#bac7d4'
    Draw-Button $g 'New' 1120 20 70; Draw-Button $g 'Open' 1196 20 76; Draw-Button $g 'Save' 1278 20 70
    Draw-Button $g 'Preview ▶' 1352 20 116 '#38bed0' '#102c37'
    $bar=[Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(32,48,69)); $g.FillRectangle($bar,24,66,1552,44); $bar.Dispose()
    Draw-Button $g '1  Prep' 32 71 92; Draw-Button $g '2  Direct' 130 71 104; Draw-Button $g '3  Edit' 240 71 90
    Draw-Text $g 'Scene 1 / 1' 352 81 16 '#f0f5d9' $true
    if($cameraFocus){Draw-Text $g 'Smooth camera move · 3 points · 4.0 s' 515 81 16 '#88e0dc' $true}else{Draw-Text $g 'Choose a sample object and adjust its position' 515 81 16 '#d4dfeb'}

    # Workspace panels.
    $panel=[Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(238,246,212))
    Fill-Round $g $panel 24 122 204 520 10; Fill-Round $g $panel 1312 122 264 520 10; Fill-Round $g $panel 24 658 1552 306 10; $panel.Dispose()
    Draw-Text $g 'IN SCENE' 42 141 15 '#254534' $true
    Draw-Text $g 'OBJECTS' 42 177 14 '#6a845f' $true
    foreach($row in @(@('Camera',211),@('Stage',245),@('Toy Bot',279),@('Star prop',313))){
        if($row[0] -eq 'Toy Bot'){$hl=[Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(210,232,178)); $g.FillRectangle($hl,37,$row[1]-4,176,29);$hl.Dispose()}
        Draw-Text $g $row[0] 60 $row[1] 16 '#244233' ($row[0] -eq 'Toy Bot')
    }
    Draw-Text $g 'LIBRARY' 42 373 14 '#6a845f' $true
    foreach($c in @(@('Characters',407),@('Sets',441),@('Props',475),@('Audio',509))){$tile=[Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(223,238,190));Fill-Round $g $tile 38 $c[1] 176 28 5;$tile.Dispose();Draw-Text $g $c[0] 50 ($c[1]+5) 14 '#314f3c'}
    Draw-Text $g 'Sample assets only' 42 570 13 '#697e68'

    # Main viewport and stylized, original sample scene.
    $sky=[Drawing.Drawing2D.LinearGradientBrush]::new([Drawing.Rectangle]::new(240,122,1062,520),[Drawing.Color]::FromArgb(24,113,142),[Drawing.Color]::FromArgb(17,39,57),90)
    $g.FillRectangle($sky,240,122,1062,520);$sky.Dispose()
    $bubble=[Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(100,107,220,220)); foreach($b in @(@(330,174,31),@(1117,213,18),@(394,282,15),@(1004,161,24))){$g.FillEllipse($bubble,$b[0],$b[1],$b[2],$b[2])};$bubble.Dispose()
    $ground=[Drawing.Drawing2D.LinearGradientBrush]::new([Drawing.Rectangle]::new(240,404,1062,238),[Drawing.Color]::FromArgb(142,178,124),[Drawing.Color]::FromArgb(51,101,102),90)
    $g.FillPolygon($ground,[Drawing.Point[]]@([Drawing.Point]::new(240,455),[Drawing.Point]::new(463,363),[Drawing.Point]::new(1016,363),[Drawing.Point]::new(1302,462),[Drawing.Point]::new(1302,642),[Drawing.Point]::new(240,642)));$ground.Dispose()
    # Decorative backdrop arch.
    $archPen=[Drawing.Pen]::new([Drawing.Color]::FromArgb(65,195,191),8);$g.DrawArc($archPen,520,209,480,280,180,180);$archPen.Dispose()
    # Soft grounding shadow.
    $shadow=[Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(75,6,22,27));$g.FillEllipse($shadow,657,527,240,38);$shadow.Dispose()
    # Original generic toy bot: rounded teal head, lilac torso, orange limbs.
    $limb=[Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(255,213,145,78));
    Fill-Round $g $limb 693 477 30 88 13; Fill-Round $g $limb 831 477 30 88 13; $limb.Dispose()
    $shoe=[Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(23,37,47)); Fill-Round $g $shoe 674 548 63 24 10; Fill-Round $g $shoe 817 548 63 24 10; $shoe.Dispose()
    $body=[Drawing.Drawing2D.LinearGradientBrush]::new([Drawing.Rectangle]::new(664,357,230,160),[Drawing.Color]::FromArgb(142,108,204),[Drawing.Color]::FromArgb(91,62,148),90)
    $g.FillPolygon($body,[Drawing.Point[]]@([Drawing.Point]::new(685,374),[Drawing.Point]::new(862,374),[Drawing.Point]::new(889,478),[Drawing.Point]::new(658,478)));$body.Dispose()
    $head=[Drawing.Drawing2D.LinearGradientBrush]::new([Drawing.Rectangle]::new(671,211,217,180),[Drawing.Color]::FromArgb(79,219,198),[Drawing.Color]::FromArgb(20,124,153),45)
    $g.FillEllipse($head,675,218,210,178);$head.Dispose()
    $ring=[Drawing.Pen]::new([Drawing.Color]::FromArgb(22,55,67),7);$g.DrawEllipse($ring,675,218,210,178);$ring.Dispose()
    $antenna=[Drawing.Pen]::new([Drawing.Color]::FromArgb(246,178,80),11);$antenna.StartCap=[Drawing.Drawing2D.LineCap]::Round;$antenna.EndCap=[Drawing.Drawing2D.LineCap]::Round;$g.DrawLine($antenna,780,221,780,189);$antenna.Dispose()
    $orb=[Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(255,244,157,66));$g.FillEllipse($orb,766,174,28,28);$orb.Dispose()
    $white=[Drawing.SolidBrush]::new([Drawing.Color]::White);$dark=[Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(19,42,56));$g.FillEllipse($white,720,278,44,49);$g.FillEllipse($white,801,278,44,49);$g.FillEllipse($dark,736,294,15,20);$g.FillEllipse($dark,813,294,15,20);$white.Dispose();$dark.Dispose()
    $smile=[Drawing.Pen]::new([Drawing.Color]::FromArgb(255,241,213),8);$smile.StartCap=[Drawing.Drawing2D.LineCap]::Round;$smile.EndCap=[Drawing.Drawing2D.LineCap]::Round;$g.DrawArc($smile,748,313,65,39,10,160);$smile.Dispose()
    # Side prop and viewport label.
    $prop=[Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(244,172,67));$g.FillPolygon($prop,[Drawing.Point[]]@([Drawing.Point]::new(1004,465),[Drawing.Point]::new(1051,438),[Drawing.Point]::new(1098,465),[Drawing.Point]::new(1051,493)));$prop.Dispose()
    $prop2=[Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(64,196,168));$g.FillPolygon($prop2,[Drawing.Point[]]@([Drawing.Point]::new(1004,465),[Drawing.Point]::new(1051,493),[Drawing.Point]::new(1051,548),[Drawing.Point]::new(1004,520)));$g.FillPolygon($prop2,[Drawing.Point[]]@([Drawing.Point]::new(1051,493),[Drawing.Point]::new(1098,465),[Drawing.Point]::new(1098,520),[Drawing.Point]::new(1051,548)));$prop2.Dispose()
    Draw-Text $g 'VIEWPORT  ·  16:9' 259 134 14 '#d9f4e9' $true
    if($cameraFocus){
        $pathPen=[Drawing.Pen]::new([Drawing.Color]::FromArgb(255,224,112),5);$pathPen.DashStyle=[Drawing.Drawing2D.DashStyle]::Dash;$g.DrawBezier($pathPen,355,302,520,210,915,214,1187,306);$pathPen.Dispose()
        $pointBrush=[Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(255,223,108));foreach($pt in @(@(355,302),@(774,230),@(1187,306))){$g.FillEllipse($pointBrush,$pt[0]-9,$pt[1]-9,18,18)};$pointBrush.Dispose()
        Draw-Text $g 'CAMERA PATH · smooth interpolation' 847 150 14 '#fff1b8' $true
    }

    # Inspector.
    Draw-Text $g 'SELECTED OBJECT' 1330 141 15 '#244534' $true; Draw-Text $g 'Toy Bot' 1330 174 21 '#24394a' $true; Draw-Text $g 'Transform' 1330 214 15 '#294a38' $true
    Draw-Field $g 'Position X' '0.25' 1330 245; Draw-Field $g 'Position Y' '0.10' 1330 282; Draw-Field $g 'Depth' '0.00' 1330 319; Draw-Field $g 'Rotation' '12°' 1330 356; Draw-Field $g 'Scale' '1.00' 1330 393
    $chk=[Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(38,68,54));Fill-Round $g $chk 1331 440 17 17 3;$chk.Dispose();Draw-Text $g 'Black outline' 1358 438 15 '#314b3a' $true
    Draw-Text $g 'Thickness' 1330 475 14 '#526e57';$sl=[Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(151,184,124));Fill-Round $g $sl 1330 505 210 7 3;$sl.Dispose();$knob=[Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(41,125,112));$g.FillEllipse($knob,1397,496,23,23);$knob.Dispose()
    Draw-Button $g 'Focus selection' 1330 540 210 '#deefb8'

    # Timeline panel and tracks.
    Draw-Text $g 'TIMELINE' 42 673 17 '#254534' $true
    Draw-Button $g '▶ Play' 142 666 84;Draw-Button $g '＋ Camera shot' 234 666 155;Draw-Button $g '↶' 397 666 48;Draw-Button $g '↷' 451 666 48
    Draw-Text $g 'Scene length' 550 677 14 '#405d49' $true
    $linePen=[Drawing.Pen]::new([Drawing.Color]::FromArgb(204,218,188),1);for($sec=0;$sec -le 15;$sec+=1){$x=185+$sec*87;$g.DrawLine($linePen,$x,708,$x,947);if($sec%2 -eq 0){Draw-Text $g "$sec s" ($x+4) 710 12 '#70816e'}};$linePen.Dispose()
    $labels=@(@('CAMERA',757),@('TOY BOT · ACTIONS',802),@('TOY BOT · VOICE',847),@('MUSIC',892))
    foreach($label in $labels){Draw-Text $g $label[0] 42 $label[1] 13 '#375645' $true}
    # Camera track / markers.
    if($cameraFocus){
        $cameraPen=[Drawing.Pen]::new([Drawing.Color]::FromArgb(73,109,207),8);$cameraPen.StartCap=[Drawing.Drawing2D.LineCap]::Round;$cameraPen.EndCap=[Drawing.Drawing2D.LineCap]::Round;$g.DrawLine($cameraPen,230,765,1360,765);$cameraPen.Dispose()
        foreach($x in @(230,578,926,1360)){$mk=[Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(255,223,108));$g.FillEllipse($mk,$x-10,755,20,20);$mk.Dispose()}
    }else{$cameraClip=[Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(112,91,185));Fill-Round $g $cameraClip 229 750 730 25 6;$cameraClip.Dispose();foreach($x in @(351,715,1090,1328)){$mk=[Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(255,219,108));$g.FillEllipse($mk,$x-8,753,16,16);$mk.Dispose()}}
    $act=[Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(59,172,126));Fill-Round $g $act 230 793 275 27 6;Fill-Round $g $act 550 793 205 27 6;Fill-Round $g $act 806 793 299 27 6;$act.Dispose()
    Draw-Text $g 'Wave' 244 799 13 '#ffffff' $true;Draw-Text $g 'Turn' 565 799 13 '#ffffff' $true;Draw-Text $g 'Greet' 822 799 13 '#ffffff' $true
    $voice=[Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(221,171,52));Fill-Round $g $voice 230 838 545 25 5;$voice.Dispose();Draw-Text $g 'Voice clip · sample' 244 843 13 '#ffffff' $true
    $music=[Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(72,153,190));Fill-Round $g $music 230 883 873 25 5;$music.Dispose();Draw-Text $g 'Music bed · sample' 244 888 13 '#ffffff' $true
    $playhead=[Drawing.Pen]::new([Drawing.Color]::FromArgb(235,76,89),3);$g.DrawLine($playhead,779,731,779,925);$playhead.Dispose()
    Draw-Text $g 'ILLUSTRATIVE UI PREVIEW · ORIGINAL SAMPLE ARTWORK' 1180 931 11 '#758891'
    $bmp.Save($path,[Drawing.Imaging.ImageFormat]::Png);$g.Dispose();$bmp.Dispose()
}

New-Preview $false (Join-Path $out 'editor-overview.png')
New-Preview $true (Join-Path $out 'camera-timeline.png')
Write-Output "Created illustrative previews in $out"
