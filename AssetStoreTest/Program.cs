using System;
using System.IO;
using SessionAssetStore;

namespace AssetStoreTest
{
    class Program
    {
        static void Main(string[] args)
        {
            StorageManager manager = new StorageManager();
            Console.WriteLine("authenticating");
            Console.WriteLine("Getting manifests");
            manager.GetAssetManifests(AssetCategory.Map);
            Console.WriteLine("Generating assets");
            var ass = manager.GenerateAssets(AssetCategory.Map);
            Directory.CreateDirectory("test");
            foreach(var a in ass)
            {
                Console.WriteLine(a.ToString());
                Console.WriteLine("downloading thumbnail...");
                manager.DownloadAssetThumbnail(a, AssetCategory.Map, Path.Combine("test", a.AssetName));
                Console.WriteLine("downloading asset...");
                manager.DownloadAsset(a, AssetCategory.Map, Path.Combine("test", a.Thumbnail));
            }
            Console.WriteLine("done!");
            Console.ReadLine();


        }
    }
}
