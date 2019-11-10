using System;
using System.IO;
using SessionAssetStore;
using Google.Apis.Download;
using Google.Apis.Upload;

namespace AssetStoreTest
{
    class Program
    {
        static void Main(string[] args)
        {
            Upload();
            //Download();
            Console.ReadLine();
        }

        static void Upload()
        {
            StorageManager manager = new StorageManager();
            Console.WriteLine("authenticating");
            manager.Authenticate("test/test-uploader.json");
            Console.WriteLine("Uploading");
            manager.UploadAsset(
                "test/814.json", "test/814.png", "test/814 Park.rar", new IProgress<IUploadProgress>[] {
                new Progress<IUploadProgress>(p => Console.WriteLine($"Manifest status: {p.Status}")),
                new Progress<IUploadProgress>(p => Console.WriteLine($"Thumbnail status: {p.Status}")),
                new Progress<IUploadProgress>(p => Console.WriteLine($"Asset status: {p.Status}"))
                }
                );
            Console.WriteLine("done!");
        }

        static void Download()
        {
            StorageManager manager = new StorageManager();
            Console.WriteLine("authenticating");
            manager.Authenticate();
            Console.WriteLine("Getting manifests");
            manager.GetAssetManifests(AssetCategory.Maps);
            Console.WriteLine("Generating assets");
            var assets = manager.GenerateAssets(AssetCategory.Maps);
            Directory.CreateDirectory("test");
            foreach (var a in assets)
            {
                Console.WriteLine(a.ToString());
                Console.WriteLine("downloading thumbnail...");
                manager.DownloadAssetThumbnail(a, Path.Combine("test", a.Thumbnail));
                Console.WriteLine("downloading asset...");
                manager.DownloadAsset(a, Path.Combine("test", a.AssetName), new Progress<IDownloadProgress>(p => Console.WriteLine($"{a.Name}: status: {p.Status}")));
            }
            Console.WriteLine("done");
        }
    }
}
