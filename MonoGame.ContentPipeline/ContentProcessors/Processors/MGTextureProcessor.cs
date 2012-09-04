using System;
using System.ComponentModel;
using System.IO;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content.Pipeline;
using Microsoft.Xna.Framework.Content.Pipeline.Graphics;
using Microsoft.Xna.Framework.Content.Pipeline.Processors;
using System.Runtime.InteropServices;
using System.Collections.ObjectModel;
using MonoGameContentProcessors.Content;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using Microsoft.Xna.Framework.Graphics;

namespace MonoGameContentProcessors.Processors
{
    [ContentProcessor(DisplayName = "MonoGame Texture")]
    public class MGTextureProcessor : TextureProcessor
    {
        public const int MAX_TEXTURE_SIZE = 1024;       

        [DllImport("PVRTexLibC.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr CompressTexture(byte[] data, int height, int width, int mipLevels, bool preMultiplied, bool pvrtc4bppCompression, ref IntPtr dataSizes);

        private MGCompressionMode compressionMode = MGCompressionMode.PVRTCFourBitsPerPixel;

        [DisplayName("Compression Mode")]
        [Description("Specifies the type of compression to use, if any.")]
        [DefaultValue(MGCompressionMode.PVRTCFourBitsPerPixel)]
        public MGCompressionMode CompressionMode
        {
            get { return this.compressionMode; }
            set { this.compressionMode = value; }
        }

        public override TextureContent Process(TextureContent input, ContentProcessorContext context)
        {
            // Fallback if we aren't buiding for iOS.
            var platform = ContentHelper.GetMonoGamePlatform();
            if (platform != MonoGamePlatform.iOS)
                return base.Process(input, context);


            // Only go this path if we are compressing the texture
            if (TextureFormat != TextureProcessorOutputFormat.DxtCompressed)
                return base.Process(input, context);

            var height = input.Faces[0][0].Height;
            var width = input.Faces[0][0].Width;
            var mipLevels = 1;

            var invalidBounds = height != width || !(isPowerOfTwo(height) && isPowerOfTwo(width));

            // TODO: Reflector ResizeToPowerOfTwo(TextureContent tex)
            // Resize the first face and let mips get generated from the dll.
            if (invalidBounds)
            {
                byte[] originalBytes = input.Faces[0][0].GetPixelData();

                //Here create the Bitmap to the know height, width and format
                Bitmap bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb);

                BitmapData bmpData = bmp.LockBits(
                       new System.Drawing.Rectangle(0, 0, bmp.Width, bmp.Height),
                       ImageLockMode.WriteOnly, bmp.PixelFormat);

                //Copy the data from the byte array into BitmapData.Scan0
                Marshal.Copy(originalBytes, 0, bmpData.Scan0, originalBytes.Length);

                //Unlock the pixels
                bmp.UnlockBits(bmpData);

                int newSize = Math.Min(Pow2roundup(Math.Max(width, height)), MAX_TEXTURE_SIZE);

                Bitmap newBitmap = new Bitmap(newSize, newSize, PixelFormat.Format32bppArgb);

                using (Graphics gfx = Graphics.FromImage(newBitmap))
                {
                    gfx.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    gfx.DrawImage(bmp, new System.Drawing.Rectangle(0, 0, newSize, newSize),
                        new System.Drawing.Rectangle(0, 0, bmp.Width, bmp.Height), GraphicsUnit.Pixel);
                }

                BitmapData newbmpData = newBitmap.LockBits(
                       new System.Drawing.Rectangle(0, 0, newBitmap.Width, newBitmap.Height),
                       ImageLockMode.ReadOnly, newBitmap.PixelFormat);

                var length = newbmpData.Stride * newbmpData.Height;

                byte[] newBytes = new byte[length];

                //Copy the data from the byte array into BitmapData.Scan0
                Marshal.Copy(newbmpData.Scan0, newBytes, 0, length);

                //Unlock the pixels
                newBitmap.UnlockBits(newbmpData);

                PixelBitmapContent<Microsoft.Xna.Framework.Color> pbc = new PixelBitmapContent<Microsoft.Xna.Framework.Color>(newSize, newSize);

                pbc.SetPixelData(newBytes);

                input.Faces[0][0] = pbc;

                context.Logger.LogWarning("", input.Identity, "Texture resized to: " + input.Faces[0][0].Width + "x" + input.Faces[0][0].Height, this);


                // don't forget to reset width height vars
                height = newSize;
                width = newSize;
                invalidBounds = false;
            }

            // Only PVR compress square, power of two textures.
            if (invalidBounds || compressionMode == MGCompressionMode.NoCompression)
            {
                if (compressionMode != MGCompressionMode.NoCompression)
                {
                    context.Logger.LogImportantMessage("WARNING: PVR Texture {0} must be a square, power of two texture. Skipping Compression.",
                                                        Path.GetFileName(context.OutputFilename));
                }

                // Skip compressing this texture and process it normally.
                this.TextureFormat = TextureProcessorOutputFormat.Color;

                return base.Process(input, context);
            }

            // Calculate how many mip levels will be created, and pass that to our DLL.
            if (GenerateMipmaps)
            {
                while (height != 1 || width != 1)
                {
                    height = Math.Max(height / 2, 1);
                    width = Math.Max(width / 2, 1);
                    mipLevels++;
                }
            }

            if (PremultiplyAlpha)
            {
                var colorTex = input.Faces[0][0] as PixelBitmapContent<Microsoft.Xna.Framework.Color>;
                if (colorTex != null)
                {
                    for (int x = 0; x < colorTex.Height; x++)
                    {
                        var row = colorTex.GetRow(x);
                        for (int y = 0; y < row.Length; y++)
                        {
                            if (row[y].A < 0xff)
                                row[y] = Microsoft.Xna.Framework.Color.FromNonPremultiplied(row[y].R, row[y].G, row[y].B, row[y].A);
                        }
                    }
                }
                else
                {
                    var vec4Tex = input.Faces[0][0] as PixelBitmapContent<Vector4>;
                    if (vec4Tex == null)
                        throw new NotSupportedException();

                    for (int x = 0; x < vec4Tex.Height; x++)
                    {
                        var row = vec4Tex.GetRow(x);
                        for (int y = 0; y < row.Length; y++)
                        {
                            if (row[y].W < 1.0f)
                            {
                                row[y].X *= row[y].W;
                                row[y].Y *= row[y].W;
                                row[y].Z *= row[y].W;
                            }
                        }
                    }
                }
            }

            ConvertToPVRTC(input, mipLevels, PremultiplyAlpha, compressionMode);

            return input;
        }

        public static void ConvertToPVRTC(TextureContent sourceContent, int mipLevels, bool premultipliedAlpha, MGCompressionMode bpp)
        {
            foreach (MipmapChain face in sourceContent.Faces)
            {
                IntPtr dataSizesPtr = IntPtr.Zero;

                var texDataPtr = CompressTexture(face[0].GetPixelData(),
                                                face[0].Height,
                                                face[0].Width,
                                                mipLevels,
                                                premultipliedAlpha,
                                                bpp == MGCompressionMode.PVRTCFourBitsPerPixel,
                                                ref dataSizesPtr);

                // Store the size of each mipLevel
                var dataSizesArray = new int[mipLevels];
                Marshal.Copy(dataSizesPtr, dataSizesArray, 0, dataSizesArray.Length);

                var levelSize = 0;
                byte[] levelData;
                var sourceWidth = face[0].Width;
                var sourceHeight = face[0].Height;

                // Set the pixel data for each mip level.
                face.Clear();

                for (int x = 0; x < mipLevels; x++)
                {
                    levelSize = dataSizesArray[x];
                    levelData = new byte[levelSize];

                    Marshal.Copy(texDataPtr, levelData, 0, levelSize);

                    var levelWidth = Math.Max(sourceWidth >> x, 1);
                    var levelHeight = Math.Max(sourceHeight >> x, 1);

                    face.Add(new MGBitmapContent(levelData, levelWidth, levelHeight, bpp));

                    texDataPtr = IntPtr.Add(texDataPtr, levelSize);
                }
            }
        }

        private bool isPowerOfTwo(int x)
        {
            return (x != 0) && ((x & (x - 1)) == 0);
        }

        private int Pow2roundup (int x)
        {
            if (x < 0)
                return 0;
            --x;
            x |= x >> 1;
            x |= x >> 2;
            x |= x >> 4;
            x |= x >> 8;
            x |= x >> 16;
            return x+1;
        }
    }


}