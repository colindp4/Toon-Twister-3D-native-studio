using System.IO;
using System.Numerics;
using System.Windows.Media.Media3D;

namespace ToonTwisterNative;
public partial class MainWindow
{
    private sealed class NativeFace
    {
        private readonly byte[] _data;
        private readonly int _bones,_frames,_frameOffset;
        private readonly float _fps;
        private readonly int[] _parents;
        private readonly float[] _lengths;
        private readonly Matrix4x4[] _inverseBind;
        private readonly Dictionary<int,Matrix4x4>[] _shapes;
        private readonly FaceVertex[] _vertices;
        private sealed record FaceVertex(int Index,Vector3 Position,Matrix4x4 World,int[] Joints,float[] Weights);
        private static Matrix4x4 ReadMatrix(BinaryReader r)=>new(r.ReadSingle(),r.ReadSingle(),r.ReadSingle(),r.ReadSingle(),r.ReadSingle(),r.ReadSingle(),r.ReadSingle(),r.ReadSingle(),r.ReadSingle(),r.ReadSingle(),r.ReadSingle(),r.ReadSingle(),r.ReadSingle(),r.ReadSingle(),r.ReadSingle(),r.ReadSingle());
        public NativeFace(string path,int vertexCount)
        {
            _data=File.ReadAllBytes(path);using var r=new BinaryReader(new MemoryStream(_data));
            if(new string(r.ReadChars(4))!="TTF1"||r.ReadInt32()!=vertexCount)throw new InvalidDataException("Mondrig en model horen niet bij elkaar.");
            _bones=r.ReadInt32();_frames=r.ReadInt32();_fps=r.ReadSingle();var affected=r.ReadInt32();
            if(_bones<1||_bones>1024||_frames<1||affected<0||affected>vertexCount)throw new InvalidDataException("Ongeldige mondrig.");
            _parents=new int[_bones];_lengths=new float[_bones];_inverseBind=new Matrix4x4[_bones];
            for(var i=0;i<_bones;i++){_parents[i]=r.ReadInt32();_lengths[i]=r.ReadSingle();_inverseBind[i]=ReadMatrix(r);if(_parents[i]>=i)throw new InvalidDataException("Ongeldige mondbotvolgorde.");}
            var count=r.ReadInt32();_shapes=new Dictionary<int,Matrix4x4>[count];
            for(var s=0;s<count;s++){_shapes[s]=[];var n=r.ReadInt32();for(var i=0;i<n;i++){var bone=r.ReadInt32();_shapes[s].Add(bone,ReadMatrix(r));}}
            _vertices=new FaceVertex[affected];
            for(var i=0;i<affected;i++)
            {
                var index=r.ReadInt32();var position=new Vector3(r.ReadSingle(),r.ReadSingle(),r.ReadSingle());var world=ReadMatrix(r);var n=r.ReadInt32();
                var joints=new int[n];var weights=new float[n];for(var j=0;j<n;j++){joints[j]=r.ReadInt32();weights[j]=r.ReadSingle();}
                _vertices[i]=new FaceVertex(index,position,world,joints,weights);
            }
            _frameOffset=(int)r.BaseStream.Position;
            if((long)_frameOffset+(long)_frames*_bones*64!=_data.LongLength)throw new InvalidDataException("Onvolledige mondanimatie.");
        }
        public Dictionary<int,Point3D> Pose(double time,int viseme,double? flossPhase=null)
        {
            var output=new Dictionary<int,Point3D>();if(flossPhase is null&&(viseme<1||viseme>_shapes.Length))return output;
            var position=Math.Clamp(time*_fps,0,_frames-1);var frame=(int)position;var next=Math.Min(frame+1,_frames-1);var fraction=(float)(position-frame);
            using var r=new BinaryReader(new MemoryStream(_data));var normal=new Matrix4x4[_bones];var posed=new Matrix4x4[_bones];var changed=new bool[_bones];
            for(var i=0;i<_bones;i++){r.BaseStream.Position=_frameOffset+((long)frame*_bones+i)*64;var a=ReadMatrix(r);r.BaseStream.Position=_frameOffset+((long)next*_bones+i)*64;normal[i]=Matrix4x4.Lerp(a,ReadMatrix(r),fraction);}
            for(var i=0;i<_bones;i++)
            {
                var p=_parents[i];
                if(flossPhase.HasValue&&(i==26||i==39||i==51||i==57))
                {var swing=(float)Math.Sin(flossPhase.Value*Math.PI*2);var angle=i==26||i==39?swing*.95f:(i==51?Math.Max(0,-swing):Math.Min(0,-swing))*.33f;var pivot=normal[i].Translation;posed[i]=normal[i]*Matrix4x4.CreateTranslation(-pivot)*Matrix4x4.CreateRotationY(angle)*Matrix4x4.CreateTranslation(pivot);changed[i]=true;}
                else if(viseme>=1&&viseme<=_shapes.Length&&_shapes[viseme-1].TryGetValue(i,out var local))
                {posed[i]=p<0?local:local*Matrix4x4.CreateTranslation(_lengths[p],0,0)*posed[p];changed[i]=true;}
                else if(p>=0&&changed[p])
                {if(!Matrix4x4.Invert(normal[p],out var inverse))throw new InvalidDataException("Singuliere mondbottransformatie.");posed[i]=normal[i]*inverse*posed[p];changed[i]=true;}
                else posed[i]=normal[i];
            }
            var skin=new Matrix4x4[_bones];for(var i=0;i<_bones;i++)skin[i]=_inverseBind[i]*posed[i];
            foreach(var v in _vertices)
            {
                var sum=Vector3.Zero;var total=0f;
                for(var i=0;i<v.Joints.Length;i++){var bone=v.Joints[i];if(bone<0||bone>=_bones)continue;sum+=Vector3.Transform(v.Position,skin[bone])*v.Weights[i];total+=v.Weights[i];}
                var point=Vector3.Transform(total>.0001?sum/total:v.Position,v.World);
                output[v.Index]=new Point3D(point.X*.01,point.Y*.01,point.Z*.01);
            }
            return output;
        }
    }
}
