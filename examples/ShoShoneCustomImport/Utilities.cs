using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using System.Linq;


namespace ShoShoneCustomImport
{
	/// <summary>
	/// Keeps track of uploaded files
	/// </summary>
	public class UploadedFile
	{
		/// <summary>
		/// originalPageName of the file
		/// </summary>
		public string id { get; set; }


		public string pageId { get; set; }

		/// <summary>
		/// The batch in which it was uploaded
		/// </summary>
		public string batchName { get; set; }

		public string error { get; set; }

		public string uploaderVersion { get; set; }
	}


	static class Utilities
    {

		public static IEnumerable<UploadedFile> readCacheFile(string filename)
		{
			Console.WriteLine($"loading status file {filename}");

			long errorCount = 0;

			using (var file = new System.IO.StreamReader(filename))
			{
				string line;
				while ((line = file.ReadLine()) != null)
				{
					UploadedFile val;
					try
					{
						val = JsonConvert.DeserializeObject<UploadedFile>(line);

					}
					catch (Exception ex)
					{
						var g = line.Split('\n');

						++errorCount;

						//Console.Error.WriteLine($"json error errorCount -> {++errorCount}, ex: {ex}");
						continue;

						//throw ex;
					}
					yield return val;
				}

				Console.Error.WriteLine($"json error errorCount -> {errorCount}");

				file.Close();
			}
		}

		
		
	}
}
