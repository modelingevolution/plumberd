using System;
using System.Collections.Generic;
using System.IO;

namespace ModelingEvolution.Plumberd.BlobStore
{
    public class BlobStore : IBlobStore
    {
        class FileBlob : IBlob
        {
            public FileBlob(string fileName)
            {
                Name = Path.GetFileName(fileName);
                FileName = fileName;
            }
            public string Name { get; private set; }
            public string FileName { get; private set; }

            public System.IO.Stream Open()
            {
                return File.OpenRead(FileName);
            }
        }
        class Partition : IBlobPartition
        {
            private readonly string _blobDir;
            private readonly string _category;
            private readonly Guid _id;
            private readonly string _streamName;
            public Partition(string blobDir, string category, Guid id)
            {
                _category = category;
                _blobDir = blobDir;
                _id = id;
                _streamName = $"{_category}-{id}";
            }
            public string GetBlobUrl(string name)
            {
                return Path.Combine(_blobDir, _streamName, name);
            }
            public IEnumerable<IBlob> GetBlobs()
            {
                var DirData = Path.Combine(_blobDir, _streamName);
                if (Directory.Exists(DirData))
                {
                    foreach (var i in Directory.EnumerateFiles(DirData))
                    {
                        if (!i.EndsWith(".json", StringComparison.InvariantCultureIgnoreCase))
                        {
                            yield return new FileBlob(i);
                        }
                    }
                }
            }

            public string Category => _category;
            public Guid Id => _id;

            public bool BlobExists(string name)
            {
                return File.Exists(GetBlobUrl(name));
            }
            public void SaveBlob(string name, byte[] data)
            {
                var dir = Path.Combine(_blobDir, _streamName);
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
                var fn = Path.Combine(dir, name);
                File.WriteAllBytes(fn, data);
            }

            public void SaveBlob(string name, System.IO.Stream rSteam)
            {
                var dir = Path.Combine(_blobDir, _streamName);
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                var fn = Path.Combine(dir, name);

                using (rSteam)
                using (var wStream = File.OpenWrite(fn))
                {
                    rSteam.CopyTo(wStream);
                }

            }
        }
        private readonly string _blobDir;
        public BlobStore(string blobDir)
        {
            _blobDir = blobDir;
        }
        public IBlobPartition GetPartition(string category, Guid id)
        {
            return new Partition(_blobDir, category, id);
        }

        public IEnumerable<IBlobPartition> GetPartitions()
        {
            foreach (var i in Directory.EnumerateDirectories(_blobDir))
            {
                var name = Path.GetFileName(i);
                int separator = name.IndexOf('-');
                if (separator > 0)
                {
                    var category = name.Remove(separator);
                    var guid = name.Substring(separator + 1);
                    if(Guid.TryParse(guid, out var id))
                        yield return new Partition(_blobDir, category, id);
                }
                
            }
        }
    }
}