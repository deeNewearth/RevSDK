using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.FileExtensions;
using Microsoft.Extensions.Configuration.Json;

namespace ImportFromFolder
{
    class Program
    {
        IEnumerable<FileInfo> fileToImport_imageRoot()
        {
            var dir = new DirectoryInfo(_imageRoot);
            return dir.EnumerateFiles("*.*", System.IO.SearchOption.AllDirectories).Where(fi=>fi.Name != _doneFilename);
        }

        readonly string _imageRoot;
        readonly string _doneFilename;
        readonly revCore.Config _appconfig = new revCore.Config();
        readonly Dictionary<string, bool> _doneFileMap = new Dictionary<string, bool>();

        readonly string _projectname;

        public Program()
        {
            var configuration = new ConfigurationBuilder()

#if DEBUG
              .AddJsonFile("appsettings.development.json", true, true)
#else
              .AddJsonFile("appsettings.json", true, true)
#endif
              .AddEnvironmentVariables()
              .Build();

            
            configuration.GetSection("rev").Bind(_appconfig);

            _imageRoot = configuration["folder"];
            if (string.IsNullOrWhiteSpace(_imageRoot))
                throw new Exception("empty input folder");

            _projectname = configuration["reponame"];
            if (string.IsNullOrWhiteSpace(_projectname))
                throw new Exception("empty rev reponame");


            if (!Directory.Exists(_imageRoot))
                throw new Exception($"Folder {_imageRoot} does not exist");


            

            _doneFilename = Path.Combine(_imageRoot, "revImportDoneStatus.txt");

            if (File.Exists(_doneFilename))
            {
                using (var sr = new StreamReader(_doneFilename))
                {
                    while (sr.Peek() >= 0)
                    {
                        _doneFileMap[sr.ReadLine()] = true;
                    }
                }

            }


        }

        long _donecount = 0;

        public async Task ImportDataAsync()
        {
            Console.WriteLine("Starting import");
            var rev = new revCore.Rev(_appconfig);
            
            var skipcount = 0;
            using (StreamWriter sw = new StreamWriter(_doneFilename,true))
            {
                foreach (var fi in fileToImport_imageRoot())
                {

                    if (_doneFileMap.ContainsKey(fi.FullName))
                    {
                        if((skipcount++) % 10 == 0 )
                        {
                            Console.WriteLine($"Skip count -> {skipcount}");
                        }
                        _donecount++;
                        continue;
                    }

                    await rev.CreateDocument(_projectname,
                        new Dictionary<string, string> { { "filename", fi.Name } },
                        new[] { fi }
                        );


                    sw.WriteLine(fi.FullName);
                    sw.Flush();

                    if (0 == (_donecount++) % 10)
                    {
                        Console.WriteLine($"done count -> {_donecount}");
                    }

                }
            }


        }


        static int Main(string[] args)
        {
            try
            {
                var program = new Program();

                program.ImportDataAsync().Wait();

                Console.WriteLine($"All done. {program._donecount} files");

                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("Failed With Exception");
                Console.Error.Write(ex.ToString());

                return -1;
            }
        }
    }
}
