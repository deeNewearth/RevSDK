using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace ImportFromFolder
{
    public interface IFileGetter
    {
        IEnumerable<FileInfo> fileToImport_imageRoot();
        void RemoveFile(string fullPath);
    }

    public class FileGetter : IFileGetter
    {
        readonly string _imageRoot;
        
        public FileGetter(string imageRoot)
        {
            _imageRoot = imageRoot;

            if (!Directory.Exists(_imageRoot))
                throw new Exception($"Folder {_imageRoot} does not exist");

        }

        public IEnumerable<FileInfo> fileToImport_imageRoot()
        {
            var dir = new DirectoryInfo(_imageRoot);
            return dir.EnumerateFiles("*.*", System.IO.SearchOption.AllDirectories);
        }

        public void RemoveFile(string fullPath)
        {
            try
            {
                if (File.Exists(fullPath))
                    File.Delete(fullPath);
            }
            catch(Exception ex)
            {
                Console.Error.WriteLine($"failed to remove file {fullPath}");
                Console.Error.Write(ex.ToString());
            }
        }
    }
}
