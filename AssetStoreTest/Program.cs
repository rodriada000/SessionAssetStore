using System;
using System.IO;
using SessionAssetStore;
using Amazon.S3.Transfer;

namespace AssetStoreTest
{
    class Program
    {
        static void Main(string[] args)
        {
            //Upload();
            Download();    
            Console.ReadLine();
        }

        static void Upload()
        {

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
            Console.WriteLine("done");
        }
    }
}
