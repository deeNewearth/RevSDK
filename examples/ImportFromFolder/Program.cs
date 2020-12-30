using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.FileExtensions;
using Microsoft.Extensions.Configuration.Json;

namespace ImportFromFolder
{
    public class MyConfig
    {
        /// <summary>
        /// The root forlder where we start looking for files to import
        /// </summary>
        public string imageRoot { get; set; }

        /// <summary>
        /// The name of the repository to import into
        /// If repo name is found in the Indexing regex then we ignore this value
        /// </summary>
        public string repoName { get; set; }

        /// <summary>
        /// if TRUE delete the image after import
        /// </summary>
        public bool removeAfterImport { get; set; }

        /// <summary>
        /// for rev index fileds that have spaces or special characters, this maps the keyword to the index field
        /// </summary>
        public Dictionary<string, string> indexOverride { get; set; }

        /// <summary>
        /// dot net group capturing regex
        /// if the regex contains repoName it overrides the repoName config
        /// </summary>
        public string indexRegex { get; set; }

        /*
         * indexRegex Details
         * 
         * lets assume the files root is "C:\\codework\\orderEasy\\sample images"
         * and the folder structure is
         * "C:\\codework\\orderEasy\\sample images\\clientFiles\\jay\\00001.pdf"
         * 
         * Then using the regex "C:\\codework\\orderEasy\\sample images\\(?<repoName>.*)\\(?<clientName>.*)\\(?<doc Number>.*\.)"
         * Will lead to repoName = clientFiles, clientName = jay, [doc Number] = 00001
         * 
        */

        

        public static readonly string REPONAME_KEYWORD = @"repoName";
    }

    class Program
    {
        readonly revCore.Config _appconfig = new revCore.Config();

        readonly MyConfig _importConfig = new MyConfig();

        readonly Dictionary<string, bool> _doneFileMap = new Dictionary<string, bool>();

        readonly string _doneFilename;

        readonly IFileGetter _fileGetter;

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

            configuration.GetSection("config").Bind(_importConfig);


            
            if (string.IsNullOrWhiteSpace(_importConfig?.imageRoot))
                throw new Exception("empty input folder");

            
            if (string.IsNullOrWhiteSpace(_importConfig?.repoName))
                throw new Exception("empty rev reponame");

            _fileGetter = new FileGetter(_importConfig.imageRoot);

            _doneFilename = Path.Combine(_importConfig.imageRoot, "revImportDoneStatus.txt");

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

            var indexRegex = string.IsNullOrWhiteSpace(_importConfig.indexRegex) ? null :
                new Regex(_importConfig.indexRegex);
            
            var skipcount = 0;
            using (StreamWriter sw = new StreamWriter(_doneFilename,true))
            {
                foreach (var fi in _fileGetter.fileToImport_imageRoot())
                {
                    if(_doneFilename == fi.FullName)
                    {
                        continue;
                    }

                    if (_doneFileMap.ContainsKey(fi.FullName))
                    {
                        if((skipcount++) % 10 == 0 )
                        {
                            Console.WriteLine($"Skip count -> {skipcount}");
                        }
                        _donecount++;
                        continue;
                    }

                    try
                    {
                        var fields = new Dictionary<string, string> { { "filename", fi.Name } };
                        var repoName = _importConfig.repoName;

                        if (null != indexRegex)
                        {
                            var match = indexRegex.Match(fi.FullName);
                            if (!match.Success)
                            {
                                throw new Exception("regex did not match");
                            }

                            fields = match.Groups.ToDictionary(k => k.Name, v => v.Value);

                            //Remove the full match group
                            fields.Remove(key: "0");

                            if (fields.ContainsKey(MyConfig.REPONAME_KEYWORD))
                            {
                                repoName = fields[MyConfig.REPONAME_KEYWORD];
                                fields.Remove(MyConfig.REPONAME_KEYWORD);
                            }

                            if (null != _importConfig.indexOverride)
                            {
                                foreach (var kv in _importConfig.indexOverride)
                                {
                                    if (fields.ContainsKey(kv.Key))
                                    {
                                        fields[kv.Value] = fields[kv.Key];
                                        fields.Remove(kv.Key);
                                    }
                                }
                            }
                        }

                        await rev.CreateDocument(_importConfig.repoName,
                            fields,
                            new[] { fi }
                            );
                    }
                    catch(Exception ex)
                    {
                        Console.Error.WriteLine($"failed to import file {fi.FullName}");
                        Console.Error.Write(ex.ToString());

                        continue;
                    }


                    sw.WriteLine(fi.FullName);
                    sw.Flush();

                    if (0 == (_donecount++) % 10)
                    {
                        Console.WriteLine($"done count -> {_donecount}");
                    }

                    if (_importConfig.removeAfterImport)
                    {
                        _fileGetter.RemoveFile(fi.FullName);
                    }

                }
            }


        }


        static async Task Main(string[] args)
        {
            try
            {
                var program = new Program();

                await program.ImportDataAsync();

                Console.WriteLine($"All done. {program._donecount} files");

                
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("Failed With Exception");
                Console.Error.Write(ex.ToString());
            }
        }
    }
}
