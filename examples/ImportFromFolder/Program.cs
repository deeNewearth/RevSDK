using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ImportFromFolder
{
    public class MyConfig
    {
        /// <summary>
        /// The root forlder where we start looking for files to import
        /// </summary>
        public string imageRoot { get; set; }

        //todo:dev:
        /// <summary>
        /// The folder where we create log files. If this is empty we DONOT write logs to the file
        /// </summary>
        public string logFileLocation {get;set;}

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
        public IList<IndexOverride> indexOverride { get; set; }

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
    public class IndexOverride
    {
        public string key { get; set; }
        public string value { get; set; }

    }

    class Program
    {
        readonly revCore.Config _appconfig = new revCore.Config();

        readonly MyConfig _importConfig = new MyConfig();

        readonly Dictionary<string, bool> _doneFileMap = new Dictionary<string, bool>();

        readonly string _doneFilename;

        readonly IFileGetter _fileGetter;

        readonly ILogger _logger;

        public Program()
        {
            var configuration = new ConfigurationBuilder()

            .AddJsonFile("appsettings.json", true, false)
            .AddJsonFile("importFolderSettings.json", false, true)

#if DEBUG
              .AddJsonFile("appsettings.development.json", false, true)
#endif
              .AddEnvironmentVariables()
              .Build();

            
            configuration.GetSection("rev").Bind(_appconfig);

            configuration.GetSection("config").Bind(_importConfig);

            
            _logger = LoggerFactory
                .Create(logging =>
                {
                    logging.AddConsole();

                    if (!string.IsNullOrWhiteSpace(_importConfig?.logFileLocation))
                    {
                        logging.AddFile(_importConfig?.logFileLocation.TrimEnd('/','\\') + "/logs_importer_{Date}.txt");
                    }
                })
                .CreateLogger<Program>();


            _logger.LogInformation($"\n\nStarting Folder import at {DateTime.Now}");


            if (string.IsNullOrWhiteSpace(_importConfig?.imageRoot))
            {
                _logger.LogError("empty input folder");
                throw new Exception("empty input folder");
            }               
            
            if (string.IsNullOrWhiteSpace(_importConfig?.repoName))
            {
                _logger.LogError("empty rev reponame");
                throw new Exception("empty rev reponame");
            }

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

                    if(!string.IsNullOrWhiteSpace(_importConfig.logFileLocation) && fi.FullName.StartsWith(_importConfig.logFileLocation))
                    {
                        //we donot want to import log files
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
                                _logger.LogError("regex did not match");
                                throw new Exception("regex did not match");
                            }
                            
                            fields = match.Groups.Values.ToDictionary(k => k.Name, v => v.Value);

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
                                    if (fields.ContainsKey(kv.key))
                                    {
                                        fields[kv.value] = fields[kv.key];
                                        fields.Remove(kv.key);
                                    }
                                }
                                
                            }
                        }

                        await rev.CreateDocument(_importConfig.repoName,
                            fields,
                            new[] { fi }
                            );
                        //_logger.LogInformation($"Created documnent in {_importConfig.repoName} with page {fi.FullName}");
                    }
                    catch(Exception ex)
                    {
                        Console.Error.WriteLine($"failed to import file {fi.FullName}");
                        Console.Error.Write(ex.ToString());
                        _logger.LogError(ex.ToString());

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
                program._logger.LogInformation($"All done. Added {program._donecount} documents in {program._importConfig.repoName} at {DateTime.Now}");
                
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("Failed With Exception");
                Console.Error.Write(ex.ToString());
                
            }
        }
    }
}
