using Google.Api.Gax;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Storage.v1;
using Google.Cloud.Storage.V1;
using System;
using System.IO;
using System.Net.Http;
using System.Collections.Generic;
using Newtonsoft.Json;
using System.Threading.Tasks;

namespace SessionAssetStore
{
    public class StorageManager
    {
        public static string ASSET_TEMP = "assets_tmp";
        public StorageClient client;

        public async void Authenticate()
        {
            var credentials = GoogleCredential.FromJson(File.ReadAllText("modmanager.json"));
            client = await StorageClient.CreateAsync(credentials);
            Directory.CreateDirectory("assets_tmp");
        }

        public void GetAssetManifests(AssetCategory assetCategory, IProgress<Google.Apis.Download.IDownloadProgress> progress = null)
        {
            Directory.CreateDirectory(Path.Combine(ASSET_TEMP, assetCategory.Value));
            var files = client.ListObjects(assetCategory.Value);
            foreach (var file in files)
            {
                if (file.Name.EndsWith(".json"))
                {
                    using (var stream = File.OpenWrite(Path.Combine(ASSET_TEMP, assetCategory.Value, file.Name)))
                    {
                        client.DownloadObjectAsync(file, stream, progress: progress).Wait();
                    }
                }
            }
        }

        public List<Asset> GenerateAssets(AssetCategory assetCategory)
        {
            List<Asset> buffer = new List<Asset>();
            foreach(string file in Directory.GetFiles(Path.Combine(ASSET_TEMP, assetCategory.Value), "*.json"))
            {
                buffer.Add(JsonConvert.DeserializeObject<Asset>(File.ReadAllText(file)));
            }
            return buffer;
        }

        public void DownloadAsset(Asset asset, AssetCategory assetCategory, string destination, IProgress<Google.Apis.Download.IDownloadProgress> progress = null)
        {
            using (var stream = File.OpenWrite(destination))
            {
                client.DownloadObjectAsync(assetCategory.Value, asset.AssetName, stream, progress: progress).Wait();
            }
        }

        public void DownloadAssetThumbnail(Asset asset, AssetCategory assetCategory, string destination, IProgress<Google.Apis.Download.IDownloadProgress> progress = null)
        {
            using (var stream = File.OpenWrite(destination))
            {
                client.DownloadObjectAsync(assetCategory.Value, asset.Thumbnail, stream, progress: progress).Wait();
            }
        }

    }
}
