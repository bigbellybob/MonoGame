using System;

using Microsoft.Xna.Framework.Graphics;

namespace Microsoft.Xna.Framework.Content
{
    internal class TextureCubeReader : ContentTypeReader<TextureCube>
	{
		
		protected internal override TextureCube Read(ContentReader reader, TextureCube existingInstance)
		{
			SurfaceFormat surfaceFormat = (SurfaceFormat)reader.ReadInt32 ();
			int size = reader.ReadInt32 ();
			int levels = reader.ReadInt32 ();

            SurfaceFormat convertedFormat = surfaceFormat;
            switch (surfaceFormat)
            {
#if IPHONE
                // At the moment. If a DXT Texture comes in on iOS, it's really a PVR compressed
                // texture. We need to use this hack until the content pipeline is implemented.
                // For now DXT5 means we're using 4bpp PVRCompression and DXT3 means 2bpp. Look at
                // PvrtcBitmapContent.cs for more information.:
                case SurfaceFormat.Dxt3:
                    convertedFormat = SurfaceFormat.RgbaPvrtc2Bpp;
                    break;
                case SurfaceFormat.Dxt5:
                    convertedFormat = SurfaceFormat.RgbaPvrtc4Bpp;
                    break;
#elif ANDROID
                case SurfaceFormat.Dxt1:
                case SurfaceFormat.Dxt3:
                case SurfaceFormat.Dxt5:
                    convertedFormat = SurfaceFormat.Color;
                    break;
#endif
                case SurfaceFormat.NormalizedByte4:
                    convertedFormat = SurfaceFormat.Color;
                    break;
            }

            TextureCube textureCube = new TextureCube(reader.GraphicsDevice, size, levels > 1, convertedFormat);
			for (int face = 0; face < 6; face++) {
				for (int i=0; i<levels; i++) {
					int faceSize = reader.ReadInt32();
					byte[] faceData = reader.ReadBytes(faceSize);
					textureCube.SetData<byte>((CubeMapFace)face, i, null, faceData, 0, faceSize);
				}
			}
			return textureCube;
		}
	}
}
