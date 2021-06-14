using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using System.Linq;

namespace importCommon
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

        public long fileSize { get; set; }
    }


    public static class Utilities
    {

        public static string NormalizeDirSeperator(this string data)
        {
            //Console.WriteLine($"NormalizeDirSeperator called with {data}");

            var ret =  data.Replace("\\", "/")
                                            .Replace('\\', '/')
                                            .Replace("//", "/");

            while(-1 != ret.IndexOf("//"))
            {
                ret = ret.Replace("//", "/");
            }

            //Console.WriteLine($"NormalizeDirSeperator normalized -> {ret}");

            //Console.ReadKey();

            return ret;


        }

        public static SqlDataReader ExecuteReaderDiag(this SqlCommand cmd)
        {
            try
            {
                return cmd.ExecuteReader();
            }
            catch (Exception ex)
            {
                throw new Exception($"error in sql {cmd.CommandText}", ex);
            }
        }

        public static IEnumerable<String[]> executeReader(this SqlConnection conn, String cmd)
        {

            if (null == conn)
                yield break;

            using (var command = conn.CreateCommand())
            {
                command.CommandText = cmd;
                command.CommandTimeout = 300;
                using (var reader = ExecuteReaderDiag(command))
                {
                    while (reader.Read())
                    {
                        var ret = new string[reader.FieldCount];
                        for (int i = 0; i < reader.FieldCount; i++)
                        {
                            ret[i] = (reader.GetValue(i) ?? "").ToString().Trim();
                        }

                        yield return ret;
                    }
                }
            }

        }


        public static IEnumerable<UploadedFile> readCacheFile(string filename)
        {
            return readCacheFile<UploadedFile>(filename);
        }

        public static IEnumerable<T> readCacheFile<T>(string filename)
        {
            Console.WriteLine($"loading status file {filename}");

            long errorCount = 0;

            using (var file = new System.IO.StreamReader(filename))
            {
                string line;
                while ((line = file.ReadLine()) != null)
                {
                    T val;
                    try
                    {
                        val = JsonConvert.DeserializeObject<T>(line);
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

        public static ConcurrentDictionary<string, string> UploadedFileMap(string statusFile, bool ensureExists = true, bool normlizeDirPaths = false)
        {
            if (!File.Exists(statusFile) )
            {
                if (ensureExists)
                    throw new Exception($"Status file {statusFile} does not exist");
                else
                    return new ConcurrentDictionary<string, string>();
            }
            else
            {
                //it seesm the status file can have duplicated entries so
                //                return new ConcurrentDictionary<string, string>(readCacheFile(statusFile).ToDictionary(k => k.id, v => v.pageId));


                var ret = new Dictionary<string, string>();
                foreach(var kv in readCacheFile(statusFile))
                {
                    if (null == kv)
                        continue;

                    var key = normlizeDirPaths ? kv.id.NormalizeDirSeperator() : kv.id;

                    if (!ret.ContainsKey(kv.id))
                    {
                        ret[key] = kv.pageId;
                    }
                }

                return new ConcurrentDictionary<string, string>(ret);
            }
        }


    }
}
