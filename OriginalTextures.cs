using System.IO;
using System.IO.Compression;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ToonTwisterNative;
public partial class MainWindow
{
    private static readonly Dictionary<string,BitmapSource> OriginalTextureCache=new(StringComparer.OrdinalIgnoreCase);
    private static BitmapSource LoadOriginalTexture(string path)
    {
        if(OriginalTextureCache.TryGetValue(path,out var cached))return cached;
        var bytes=File.ReadAllBytes(path);
        using var source=new MemoryStream(bytes);
        var decoder=BitmapDecoder.Create(source,BitmapCreateOptions.PreservePixelFormat,BitmapCacheOption.OnLoad);
        BitmapSource bitmap=decoder.Frames[0];
        // Legacy IFX stores JPEG RGB followed by width/height/size and a zlib
        // grayscale alpha plane. A normal JPEG decoder silently drops that tail.
        for(var i=2;i+14<bytes.Length;i++)
        {
            if(bytes[i-2]!=255||bytes[i-1]!=217)continue;
            var width=BitConverter.ToInt32(bytes,i);var height=BitConverter.ToInt32(bytes,i+4);var length=BitConverter.ToInt32(bytes,i+8);
            if(width!=bitmap.PixelWidth||height!=bitmap.PixelHeight||length<=0||length>bytes.Length-i-12||width<=0||height<=0)continue;
            using var packed=new MemoryStream(bytes,i+12,length);
            using var zlib=new ZLibStream(packed,CompressionMode.Decompress);
            var alpha=new byte[checked(width*height)];zlib.ReadExactly(alpha);
            if(zlib.ReadByte()!=-1)throw new InvalidDataException("Onverwachte lengte van W3D-transparantiekanaal.");
            var rgba=new FormatConvertedBitmap(bitmap,PixelFormats.Bgra32,null,0);var pixels=new byte[checked(width*height*4)];rgba.CopyPixels(pixels,width*4,0);
            // The mask is a bottom-up IFX/DIB plane; JPEG scanlines are top-down.
            for(var y=0;y<height;y++)for(var x=0;x<width;x++)pixels[(y*width+x)*4+3]=alpha[(height-1-y)*width+x];
            bitmap=BitmapSource.Create(width,height,96,96,PixelFormats.Bgra32,null,pixels,width*4);break;
        }
        bitmap.Freeze();OriginalTextureCache[path]=bitmap;return bitmap;
    }
}
