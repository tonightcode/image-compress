using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace LetSmall
{
    internal static class ImageCodec
    {
        internal static string FormatFromPath(string path)
        {
            switch (Path.GetExtension(path).ToLowerInvariant())
            {
                case ".jpg":
                case ".jpeg": return "JPEG";
                case ".png": return "PNG";
                case ".webp": return "WebP";
                default: return null;
            }
        }

        internal static string ExtensionFor(string format)
        {
            switch (format)
            {
                case "JPEG": return ".jpg";
                case "PNG": return ".png";
                case "WebP": return ".webp";
                default: throw new ArgumentException("Unsupported image format.", "format");
            }
        }

        internal static BitmapSource Decode(string path)
        {
            if (FormatFromPath(path) == "WebP")
            {
                return DecodeWebP(File.ReadAllBytes(path));
            }

            using (var stream = File.OpenRead(path))
            {
                var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat,
                    BitmapCacheOption.OnLoad);
                if (decoder.Frames.Count == 0)
                {
                    throw new InvalidDataException("No image frame found.");
                }
                var frame = decoder.Frames[0];
                var oriented = ApplyOrientation(frame);
                oriented.Freeze();
                return oriented;
            }
        }

        private static BitmapSource ApplyOrientation(BitmapFrame frame)
        {
            var metadata = frame.Metadata as BitmapMetadata;
            const string query = "/app1/ifd/{ushort=274}";
            if (metadata == null || !metadata.ContainsQuery(query))
                return frame;

            var value = metadata.GetQuery(query);
            if (!(value is ushort))
                return frame;

            var transform = new TransformGroup();
            switch ((ushort)value)
            {
                case 2: transform.Children.Add(new ScaleTransform(-1, 1)); break;
                case 3: transform.Children.Add(new RotateTransform(180)); break;
                case 4: transform.Children.Add(new ScaleTransform(1, -1)); break;
                case 5:
                    transform.Children.Add(new ScaleTransform(-1, 1));
                    transform.Children.Add(new RotateTransform(270));
                    break;
                case 6: transform.Children.Add(new RotateTransform(90)); break;
                case 7:
                    transform.Children.Add(new ScaleTransform(-1, 1));
                    transform.Children.Add(new RotateTransform(90));
                    break;
                case 8: transform.Children.Add(new RotateTransform(270)); break;
                default: return frame;
            }
            return new TransformedBitmap(frame, transform);
        }

        internal static void Encode(BitmapSource source, string format, string path)
        {
            if (format == "WebP")
            {
                EncodeWebP(source, path);
                return;
            }

            BitmapEncoder encoder;
            if (format == "JPEG")
            {
                encoder = new JpegBitmapEncoder { QualityLevel = 82 };
                source = FlattenOnWhite(source);
            }
            else if (format == "PNG")
            {
                encoder = new PngBitmapEncoder();
            }
            else
            {
                throw new ArgumentException("Unsupported image format.", "format");
            }

            encoder.Frames.Add(BitmapFrame.Create(source));
            using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write))
            {
                encoder.Save(stream);
            }
        }

        private static BitmapSource DecodeWebP(byte[] encoded)
        {
            int width;
            int height;
            var pointer = NativeWebP.WebPDecodeBGRA(encoded, (UIntPtr)encoded.LongLength,
                out width, out height);
            if (pointer == IntPtr.Zero)
            {
                throw new InvalidDataException("Cannot decode WebP image.");
            }

            try
            {
                var stride = checked(width * 4);
                var pixels = new byte[checked(stride * height)];
                Marshal.Copy(pointer, pixels, 0, pixels.Length);
                var bitmap = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32,
                    null, pixels, stride);
                bitmap.Freeze();
                return bitmap;
            }
            finally
            {
                NativeWebP.WebPFree(pointer);
            }
        }

        private static void EncodeWebP(BitmapSource source, string path)
        {
            var bitmap = ToBgra32(source);
            var stride = checked(bitmap.PixelWidth * 4);
            var pixels = new byte[checked(stride * bitmap.PixelHeight)];
            bitmap.CopyPixels(pixels, stride, 0);

            IntPtr output;
            var size = NativeWebP.WebPEncodeBGRA(pixels, bitmap.PixelWidth, bitmap.PixelHeight,
                stride, 78, out output);
            if (size == UIntPtr.Zero || output == IntPtr.Zero)
            {
                throw new InvalidDataException("Cannot encode WebP image.");
            }

            try
            {
                var encoded = new byte[checked((int)size.ToUInt64())];
                Marshal.Copy(output, encoded, 0, encoded.Length);
                using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write))
                {
                    stream.Write(encoded, 0, encoded.Length);
                }
            }
            finally
            {
                NativeWebP.WebPFree(output);
            }
        }

        private static BitmapSource ToBgra32(BitmapSource source)
        {
            if (source.Format == PixelFormats.Bgra32)
            {
                return source;
            }
            var converted = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
            converted.Freeze();
            return converted;
        }

        private static BitmapSource FlattenOnWhite(BitmapSource source)
        {
            var bitmap = ToBgra32(source);
            var stride = checked(bitmap.PixelWidth * 4);
            var pixels = new byte[checked(stride * bitmap.PixelHeight)];
            bitmap.CopyPixels(pixels, stride, 0);
            for (var i = 0; i < pixels.Length; i += 4)
            {
                var alpha = pixels[i + 3];
                for (var channel = 0; channel < 3; channel++)
                {
                    pixels[i + channel] = (byte)((pixels[i + channel] * alpha +
                        255 * (255 - alpha)) / 255);
                }
                pixels[i + 3] = 255;
            }
            var flattened = BitmapSource.Create(bitmap.PixelWidth, bitmap.PixelHeight,
                bitmap.DpiX, bitmap.DpiY, PixelFormats.Bgra32, null, pixels, stride);
            flattened.Freeze();
            return flattened;
        }

        private static class NativeWebP
        {
            [DllImport("libwebp.dll", CallingConvention = CallingConvention.Cdecl)]
            internal static extern IntPtr WebPDecodeBGRA(byte[] data, UIntPtr size,
                out int width, out int height);

            [DllImport("libwebp.dll", CallingConvention = CallingConvention.Cdecl)]
            internal static extern UIntPtr WebPEncodeBGRA(byte[] pixels, int width, int height,
                int stride, float quality, out IntPtr output);

            [DllImport("libwebp.dll", CallingConvention = CallingConvention.Cdecl)]
            internal static extern void WebPFree(IntPtr pointer);
        }
    }
}
