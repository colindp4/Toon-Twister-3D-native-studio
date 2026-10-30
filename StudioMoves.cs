using System.Windows.Media.Media3D;
using System.IO;

namespace ToonTwisterNative;
public partial class MainWindow
{
    private SavedTransform? _actionPreviewBase;
    private void EndActionPreview()
    {
        if(_actionPreview is {} preview&&!string.IsNullOrEmpty(preview.Clip.preset)&&_actionPreviewBase is {} b&&_models.TryGetValue(preview.Model,out var model))
        {model.X=b.X;model.Y=b.Y;model.Z=b.Z;model.RotationY=b.RotationY;model.Scale=b.Scale;model.PresetTiltX=0;model.PresetTiltZ=0;ApplyTransform(model);}
        _actionPreview=null;_actionPreviewBase=null;
    }
    // Root movement presets retain the actor's original skeletal idle and mouth animation.
    private static IEnumerable<ActionClip> AvailableClips(string path)=>ClipsFor(path).Concat(new[]{
        new ActionClip {name="studio_bounce",desc="Stuiteren",preset="bounce",cat="Studio",end=1600,loop=1},
        new ActionClip {name="studio_turn",desc="Rondje draaien",preset="turn",cat="Studio",end=2400,loop=1},
        new ActionClip {name="studio_sway",desc="Heen en weer dansen",preset="sway",cat="Studio",end=2400,loop=1},
        new ActionClip {name="studio_greet",desc="Vrolijk buigen",preset="greet",cat="Studio",end=2000,loop=1}
        ,new ActionClip {name="studio_floss",desc="Floss-achtige dans",preset="floss",cat="Studio",end=1800,loop=1}
        ,new ActionClip {name="studio_orange",desc="Orange-Justice-achtige chaos",preset="orange",cat="Studio",end=2000,loop=1}
        ,new ActionClip {name="studio_robot",desc="Robotdans",preset="robot",cat="Studio",end=2400,loop=1}
        ,new ActionClip {name="studio_shuffle",desc="Electro shuffle",preset="shuffle",cat="Studio",end=2400,loop=1}
        ,new ActionClip {name="studio_l",desc="Overdreven L-dans",preset="l",cat="Studio",end=2100,loop=1}
        ,new ActionClip {name="studio_moon",desc="Moonwalk",preset="moon",cat="Studio",end=2400,loop=1}
        ,new ActionClip {name="studio_worm",desc="De worm",preset="worm",cat="Studio",end=2000,loop=1}
        ,new ActionClip {name="studio_disaster",desc="Dansramp",preset="disaster",cat="Studio",end=1700,loop=1}
        ,new ActionClip {name="studio_victory",desc="Victory hop",preset="victory",cat="Studio",end=1600,loop=1}
        ,new ActionClip {name="studio_spaghetti",desc="Spaghetti-benen",preset="spaghetti",cat="Studio",end=2100,loop=1}
        ,new ActionClip {name="studio_spin",desc="Tornado",preset="tornado",cat="Studio",end=1200,loop=1}
        ,new ActionClip {name="studio_marionette",desc="Marionet",preset="marionette",cat="Studio",end=2400,loop=1},new ActionClip {name="studio_sidestep",desc="Disco zijstap",preset="sidestep",cat="Studio",end=2000,loop=1},new ActionClip {name="studio_party",desc="Feestsprongen",preset="party",cat="Studio",end=1800,loop=1},new ActionClip {name="studio_sneak",desc="Stiekeme disco",preset="sneakdisco",cat="Studio",end=2300,loop=1}
    });
    private void ApplyPresetTransform(SceneModel model,double time,IEnumerable<RecordedAction> actions)
    {
        var presets=actions.Where(a=>a.Model==model.Name&&!string.IsNullOrEmpty(a.Preset)).OrderBy(a=>a.Start).ToArray();
        var action=presets.LastOrDefault(a=>a.Start<=time)??presets.FirstOrDefault();
        if(action?.PresetBase is not {} b)return;
        model.X=b.X;model.Y=b.Y;model.Z=b.Z;model.Scale=b.Scale;model.RotationY=b.RotationY;model.PresetTiltX=0;model.PresetTiltZ=0;
        if(time>=action.Start&&time<action.Start+action.Duration)
        {
            var source=action.SourceOffset+time-action.Start;var range=Math.Max(.1,action.SourceDuration);
            var phase=(action.Loop?PositiveModulo(source,range):Math.Clamp(source,0,range))/range;var wave=Math.Sin(phase*Math.PI*2);var beat=Math.Sin(phase*Math.PI*4);var step=Math.Sin(phase*Math.PI*8);var full=action.SourceOffset+time-action.Start;
            switch(action.Preset)
            {
                case "bounce":model.Y+=Math.Abs(wave)*.42*b.Scale;break;
                case "turn":model.RotationY+=phase*360;break;
                case "sway":model.X+=wave*.45*b.Scale;model.RotationY+=wave*25;break;
                case "greet":model.Y-=Math.Abs(wave)*.12*b.Scale;model.Scale*=1-.08*Math.Abs(wave);break;
                case "floss":model.X-=wave*.08*b.Scale;model.PresetTiltZ=-wave*3;break;
                case "orange":model.X+=beat*.27*b.Scale;model.Y+=Math.Abs(step)*.18*b.Scale;model.RotationY+=wave*43;model.PresetTiltZ=beat*17;break;
                case "robot":model.RotationY+=Math.Round(phase*4)*22;model.X+=Math.Round(wave*2)*.13*b.Scale;model.PresetTiltX=Math.Round(beat*2)*9;break;
                case "shuffle":model.X+=wave*.5*b.Scale;model.Z+=beat*.3*b.Scale;model.Y+=Math.Abs(step)*.15*b.Scale;model.RotationY+=beat*25;break;
                case "l":model.Y+=Math.Abs(step)*.22*b.Scale;model.X+=wave*.19*b.Scale;model.RotationY+=beat*28;model.PresetTiltZ=wave*18;break;
                case "moon":model.Z-=phase*.8*b.Scale;model.PresetTiltX=wave*7;model.Y+=Math.Abs(beat)*.055*b.Scale;break;
                case "worm":model.PresetTiltX=wave*28;model.Y-=Math.Abs(wave)*.28*b.Scale;model.Z+=beat*.2*b.Scale;break;
                case "disaster":model.RotationY+=wave*60;model.PresetTiltZ=beat*24;model.PresetTiltX=step*17;model.Y+=Math.Abs(step)*.14*b.Scale;break;
                case "victory":model.Y+=Math.Abs(wave)*.58*b.Scale;model.RotationY+=phase*180;model.PresetTiltZ=beat*12;break;
                case "spaghetti":model.X+=wave*.3*b.Scale;model.Z+=beat*.22*b.Scale;model.PresetTiltZ=step*20;model.PresetTiltX=beat*13;break;
                case "tornado":model.RotationY+=phase*720;model.Y+=Math.Abs(beat)*.17*b.Scale;model.PresetTiltZ=wave*8;break;
                case "marionette":model.PresetTiltX=beat*12;model.PresetTiltZ=wave*14;model.Y+=Math.Abs(step)*.10*b.Scale;break;
                case "sidestep":model.X+=wave*.32*b.Scale;model.RotationY= b.RotationY+beat*15;break;
                case "party":model.Y+=Math.Abs(wave)*.32*b.Scale;model.PresetTiltZ=beat*8;break;
                case "sneakdisco":model.Z+=wave*.43*b.Scale;model.X+=beat*.23*b.Scale;model.RotationY+=step*20;model.PresetTiltZ=wave*13;break;
            }
        }
        ApplyTransform(model);
    }
    private static string[] BodyChoices(string preset)=>preset switch
    {
        "party"=>["jump","dance_cycle"],"marionette"=>["greet_cycle","dance_cycle"],
        "moon"=>["walk_back_cycle","walk_cycle","dance_cycle"],
        "worm"=>["crawl_cycle","dance_cycle"],
        "victory"=>["jump","dance_cycle"],
        "sneakdisco"=>["sneak_cycle","sneak","dance_cycle"],
        "disaster"=>["tantrum_cycle","dance_cycle"],
        "robot"=>["point","dance_cycle"],
        _=>["dance_cycle","greet_cycle","walk_cycle"]
    };
    private static ActionClip? BodyFor(string path,string preset)
    {var clips=ClipsFor(path);if(preset=="floss"&&Path.GetFileName(path).Contains("spongebob",StringComparison.OrdinalIgnoreCase))return clips.FirstOrDefault(c=>c.name=="neutral_cycle")??clips.FirstOrDefault(c=>c.name=="idle");return BodyChoices(preset).Select(name=>clips.FirstOrDefault(c=>c.name==name)).FirstOrDefault(c=>c is not null);}
    private static double BodyTime(RecordedAction action,double timeline,NativeMotion motion)
    {
        if(action.Preset=="floss"&&motion.FlossArms)return motion.IdleStart+PositiveModulo((action.SourceOffset+timeline-action.Start)*action.BodySpeed,Math.Max(.1,motion.IdleEnd-motion.IdleStart));
        if(action.BodyDuration<=.01)return motion.IdleStart+PositiveModulo(timeline,Math.Max(.1,motion.IdleEnd-motion.IdleStart));
        return action.BodyStart+PositiveModulo((action.SourceOffset+timeline-action.Start)*action.BodySpeed,action.BodyDuration);
    }
    private static double PreviewBodyTime(string path,string preset,double elapsed,NativeMotion motion)
    {var body=BodyFor(path,preset);return body is null?motion.IdleStart+PositiveModulo(elapsed,Math.Max(.1,motion.IdleEnd-motion.IdleStart)):body.start/1000d+PositiveModulo(elapsed*BodySpeedFor(preset),Math.Max(.1,(body.end-body.start)/1000d));}
    private static double BodySpeedFor(string preset)=>preset switch {"robot"=>.7,"orange"=>1.4,"shuffle"=>1.3,"disaster"=>1.8,"tornado"=>1.7,"worm"=>.9,_=>1};
}

