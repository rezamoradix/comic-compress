using System;
using System.Linq;
using System.Drawing;
using Imazen.WebP;
using SharpCompress.Archives;
using SharpCompress.Archives.Rar;
using SharpCompress.Archives.Zip;
using System.IO;
using SharpCompress.Common;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace ComicCompressor
{
    public class Compressor
    {
        private Logger Logger { get; set; }
        private int MultiProcessing { get; set; }

        public int Quality { get; set; } = 75;

        public Compressor(Logger logger, int multi_processing)
        {
            Logger = logger;
            MultiProcessing = multi_processing;
        }

        public void Compress(string filename, string outputFile = null)
        {
            outputFile = Path.ChangeExtension(outputFile ?? filename, "cbz");

            Logger.Log("Processing: " + filename, LogLevel.Verbose);

            IArchive archive = null;

            if (filename.EndsWith(".cbr"))
            {
                archive = RarArchive.Open(filename);
            }
            else if (filename.EndsWith(".cbz"))
            {
                archive = ZipArchive.Open(filename);
            }

            ProcessArchive(archive, outputFile);
            Logger.Log("Finished: " + filename, LogLevel.Verbose);
        }

        private void ProcessArchive(IArchive archive, string outputPath)
        {
            var fileEntries = archive.Entries.Where(e => !e.IsDirectory);

            var encoder = new SimpleEncoder();

            using (var output = ZipArchive.Create())
            {
                var fileStreams = new List<Tuple<Stream, string>>();
                foreach (var entry in fileEntries)
                {
                    MemoryStream ms = new MemoryStream();
                    using (var entryStream = entry.OpenEntryStream())
                    {
                        entryStream.CopyTo(ms);
                        fileStreams.Add(new Tuple<Stream, string>(ms, entry.Key));
                    }
                }

                Parallel.ForEach(fileStreams, new ParallelOptions()
                {
                    MaxDegreeOfParallelism = MultiProcessing
                }, file => ProcessEntry(file.Item1, file.Item2, output, encoder));

                archive.Dispose();

                Directory.CreateDirectory(Path.GetDirectoryName(outputPath));

                output.SaveTo(outputPath, CompressionType.Deflate);

                fileStreams.ForEach(i => i.Item1.Dispose());
            }
        }

        private void ProcessEntry(Stream stream, string entryKey, ZipArchive output, SimpleEncoder encoder)
        {
            if (entryKey.StartsWith("__MACOSX"))
            {
                return;
            }

            if (!entryKey.EndsWith(".jpg") && !entryKey.EndsWith(".png"))
            {
                output.AddEntry(entryKey, stream);
                return;
            }

            Bitmap bits;

            try
            {
                bits = new Bitmap(stream);
            }
            catch (Exception e)
            {
                Logger.LogError("Error parsing bitmap: " + entryKey);
                Logger.LogDebug(e, LogLevel.Error);
                return;
            }

            if (bits.PixelFormat != System.Drawing.Imaging.PixelFormat.Format24bppRgb)
            {
                var newBits = ChangePixelFormat(bits);
                bits.Dispose();
                bits = newBits;
            }

            try
            {
                using (var ms = new MemoryStream())
                {
                    encoder.Encode(bits, ms, Quality);
                    stream.Dispose();
                    stream = new MemoryStream();
                    ms.WriteTo(stream);
                    output.AddEntry(Path.ChangeExtension(entryKey, "webp"), stream);
                }
            }
            catch (Exception e)
            {
                Logger.LogError("Error encoding entry: " + entryKey);
                Logger.LogDebug(e, LogLevel.Error);
            }
            finally
            {
                bits.Dispose();
            }
        }

        private Bitmap ChangePixelFormat(Bitmap orig, System.Drawing.Imaging.PixelFormat format = System.Drawing.Imaging.PixelFormat.Format24bppRgb)
        {
            Bitmap clone = new Bitmap(orig.Width, orig.Height, format);

            using (Graphics gr = Graphics.FromImage(clone))
            {
                gr.DrawImage(orig, new Rectangle(0, 0, clone.Width, clone.Height));
            }

            return clone;
        }
    }
}