using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ShoShoneCustomImport
{
    class DataReader
    {
		

		readonly revCore.Rev _rev;


		readonly string _imageList;

		readonly ConcurrentDictionary<string, bool> _doneMap = new ConcurrentDictionary<string, bool>();

		readonly StreamWriter _statusWriter;

		public DataReader(
			revCore.Config revConfig,
			string imageList
			)
		{
			_imageList = imageList;

			var statusDir = Path.GetDirectoryName(imageList);
			var justFileName = Path.GetFileNameWithoutExtension(imageList);

			var statusFile = Path.Combine(statusDir, $"{justFileName}.doneStatus.txt");

			Console.WriteLine($"using status file {statusFile}");

			if (File.Exists(statusFile))
			{
				using (var file = new System.IO.StreamReader(statusFile))
				{
					string line;
					while ((line = file.ReadLine()) != null)
					{
						_doneMap[line] = true;
					}
				}
			}

			_statusWriter = File.AppendText(statusFile);

			_rev = new revCore.Rev(revConfig);

			

			Console.WriteLine("All initialized");

		}

		//Assessment Date : date  HRN Number : text  Patient First Name : text  Patient Last Name : text

		//"/infolder/fdtc 2014 assessments/18146y miller, junior - asm 7.21.2014.pdf"
		//readonly Regex _pattern = new Regex(@"/infolder/(?<project>.+)/(?<hrn>.+)\s+(?<last>.+),\s+(?<first>.+)\s*-\s*asm\s*(?<mm>.+)\.(?<dd>.+)\.(?<yy>.+)\..*");

		readonly Regex _pattern = new Regex(@"/infolder/(?<project>.+)/(?<hrn>.+)\s+(?<last>.+)[,\s]+(?<first>.+)\s*-\s*asm\s*(?<mm>.+)\.(?<dd>.+)\.(?<yy>.+)\..*");

		//readonly Regex _pattern = new Regex(@"/infolder/(?<project>.+)/(?<hrn>.+)\s+(?<last>.+)[,\s]+(?<first>.+)\s*-\s*(?<mm>.+)\.(?<dd>.+)\.(?<yy>.+)\..*");

		public async Task RunAsync()
		{
			var doneCount = 0;
			var erroCount = 0;
			
			foreach(var image in Utilities.readCacheFile(_imageList))
			{
				if (_doneMap.ContainsKey(image.id))
				{
					Console.WriteLine($"already done {image.id}");
					doneCount++;
					continue;
				}

				var match = _pattern.Match(image.id);

				if (!match.Success)
				{
					Console.WriteLine($"image {image.id} didnt match the pattern. erroCount-> {++erroCount}");
					continue;
				}

				var indexes = new Dictionary<string, string>
				{
					{ "HRN Number", match.Groups["hrn"].Value},
					{ "Patient First Name", match.Groups["first"].Value},
					{ "Patient Last Name", match.Groups["last"].Value},
					{ "Assessment Date", $"{match.Groups["mm"].Value}/{match.Groups["dd"].Value}/{match.Groups["yy"].Value}" },
				};

				var pages = new[]{new revCore.ExistingPage
				{
					id = image.pageId,
					orderNumber = 0,
					originalPageName = image.id,
					pageType = "unprocessed"

				}
				};

				try
				{
					await _rev.CreateDocumentAsync(match.Groups["project"].Value, indexes, pages);

					_statusWriter.WriteLine(image.id);
					_statusWriter.Flush();
				}
				catch(Exception ex)
				{
					Console.WriteLine($"image {image.id} didnt add . erroCount-> {++erroCount}, err -> {ex}");
				}
				

				if (++doneCount % 100 == 0)
				{
					Console.WriteLine($"doneCount -> {doneCount}");
				}

			}

			Console.WriteLine($"total erroCount ->{erroCount}, doneCount ->{doneCount}");

		}


	}
}
