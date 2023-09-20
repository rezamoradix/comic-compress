using System;
using System.Linq;
using System.Drawing;
using SharpCompress.Archives;
using SharpCompress.Archives.Rar;
using SharpCompress.Archives.Zip;
using System.IO;
using SharpCompress.Common;
using System.Threading.Tasks;
using System.Collections.Generic;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.PixelFormats;
using Image = SixLabors.ImageSharp.Image;
using Rectangle = System.Drawing.Rectangle;

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
			else
			{
				Logger.Log("Not a comic: " + filename, LogLevel.Verbose);
				return;
			}

			ProcessArchive(archive, outputFile);
			Logger.Log("Finished: " + filename, LogLevel.Verbose);
		}

		private void ProcessArchive(IArchive archive, string outputPath)
		{
			var fileEntries = archive.Entries.Where(e => !e.IsDirectory);

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
				}, file => ProcessEntry(file.Item1, file.Item2, output));

				archive.Dispose();

				Directory.CreateDirectory(Path.GetDirectoryName(outputPath));

				output.SaveTo(outputPath, CompressionType.Deflate);

				fileStreams.ForEach(i => i.Item1.Dispose());
			}
		}

		private void ProcessEntry(Stream stream, string entryKey, ZipArchive output)
		{
			if (entryKey.StartsWith("__MACOSX"))
			{
				return;
			}

			if (!entryKey.EndsWith(".jpg") && !entryKey.EndsWith(".png") && !entryKey.EndsWith(".jpeg"))
			{
				output.AddEntry(entryKey, stream);
				return;
			}

			try
			{
				using (var ms = new MemoryStream())
				{
					stream.Position = 0;
					using var image = Image.Load(stream).CloneAs<Rgb24>();
					ms.Position = 0;
					image.Save(ms, new WebpEncoder() { Quality = Quality });
					//encoder.Encode(bits, ms, Quality);
					stream.Dispose();
					stream = new MemoryStream();
					ms.WriteTo(stream);
					lock(output)
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
				//bits.Dispose();
			}
		}
	}
}