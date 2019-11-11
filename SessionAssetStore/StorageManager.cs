using Google.Api.Gax;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Storage.v1;
using Google.Cloud.Storage.V1;
using Google.Apis.Download;
using Google.Apis.Upload;
using System;
using System.IO;
using System.Collections.Generic;
using Newtonsoft.Json;
using System.Reflection;
using System.Diagnostics;

namespace SessionAssetStore
{
    /// <summary>
    /// Main class for managing assets
    /// </summary>
    public class StorageManager
    {
        /// <summary>
        /// Manifests temporary directory
        /// </summary>
        public static readonly string MANIFESTS_TEMP = "manifests_tmp";

        /// <summary>
        /// Shared client object
        /// </summary>
        public StorageClient client;

        /// <summary>
        /// Authenticates to the Google Cloud Storage API. Provide custom credentials to authenticate with modders rights.
        /// </summary>
        /// <param name="credentialsFile">The credentials to use to authenticate. Leave null for default read-only.</param>
        public async void Authenticate(string credentialsFile = null)
        {
            credentialsFile = credentialsFile == null ? "modmanager.json" : credentialsFile;
            var credentials = GoogleCredential.FromJson(File.ReadAllText(credentialsFile));
            client = await StorageClient.CreateAsync(credentials);
        }

        internal List<AssetCategory> GetAllCategories()
        {
            List<AssetCategory> categories = new List<AssetCategory>();
            PropertyInfo[] properties = typeof(AssetCategory).GetProperties(BindingFlags.Public | BindingFlags.Static);
            foreach (PropertyInfo property in properties)
            {
                categories.Add(property.GetValue(null) as AssetCategory);
            }
            return categories;
        }

        /// <summary>
        /// Fetch all manifests for a single category.
        /// </summary>
        /// <param name="assetCategory">The category of asset manifest to fetch</param>
        /// <param name="progress">An IProgress object to report download activities.</param>
        public void GetAssetManifests(AssetCategory assetCategory, IProgress<IDownloadProgress> progress = null)
        {
            if (client == null) throw new Exception("You must authenticate first.");
            string manifestPath = Path.Combine(MANIFESTS_TEMP, assetCategory.Value);
            // Delete the existing manifests if they exist, prevents old assets from being redownloaded.
            if (Directory.Exists(manifestPath))
            {
                Directory.Delete(manifestPath, true);
            }
            Directory.CreateDirectory(manifestPath);
            var files = client.ListObjects(assetCategory.Value);
            foreach (var file in files)
            {
                if (file.Name.EndsWith(".json"))
                {
                    using (var stream = File.OpenWrite(Path.Combine(manifestPath, file.Name)))
                    {
                        client.DownloadObjectAsync(file, stream, progress: progress).Wait();
                    }
                }
            }
        }

        /// <summary>
        /// Gets the manifests for all categories.
        /// </summary>
        public void GetAllAssetManifests()
        {     
            foreach (AssetCategory cat in GetAllCategories())
            {
                GetAssetManifests(cat);     
            }
        }

        /// <summary>
        /// Generate asset objects from the file manifests for a category.
        /// </summary>
        /// <param name="assetCategory">An asset category</param>
        /// <returns>A list of Asset</returns>
        public List<Asset> GenerateAssets(AssetCategory assetCategory)
        {
            List<Asset> buffer = new List<Asset>();
            foreach (string file in Directory.GetFiles(Path.Combine(MANIFESTS_TEMP, assetCategory.Value), "*.json"))
            {
                buffer.Add(JsonConvert.DeserializeObject<Asset>(File.ReadAllText(file)));
            }
            return buffer;
        }

        /// <summary>
        /// Loads all Asset object from fetched manifests in memory.
        /// </summary>
        /// <returns>Complete list of asset objects</returns>
        public List<Asset> GenerateAllAssets()
        {
            List<Asset> fullList = new List<Asset>();
            foreach (AssetCategory cat in GetAllCategories())
            {
                fullList.AddRange(GenerateAssets(cat));
            }
            return fullList;
        }

        /// <summary>
        /// Download a single asset.
        /// </summary>
        /// <param name="asset">The asset to download.</param>
        /// <param name="destination">Where to save the asset.</param>
        /// <param name="progress">An IProgress object to report download activities.</param>
        /// <param name="update">Redownload and overwrite an existing asset of the same name.</param>
        public void DownloadAsset(Asset asset, string destination, IProgress<IDownloadProgress> progress = null, bool update = false)
        {
            if (client == null) throw new Exception("You must authenticate first.");
            if (File.Exists(destination) && !update) { return; }
            using (var stream = File.OpenWrite(destination))
            {
                client.DownloadObjectAsync(asset.assetCategory.Value, asset.AssetName, stream, progress: progress).Wait();
            }
        }

        /// <summary>
        /// Download the thumbnail of an asset.
        /// </summary>
        /// <param name="asset">The asset to download.</param>
        /// <param name="destination">Where to save the thumbnail.</param>
        /// <param name="progress">An IProgress object to report download activities.</param>
        /// <param name="update">Redownload and overwrite an existing thumbnail of the same name.</param>
        public void DownloadAssetThumbnail(Asset asset, string destination, IProgress<IDownloadProgress> progress = null, bool update = false)
        {
            if (client == null) throw new Exception("You must authenticate first.");
            if (File.Exists(destination) && !update) { return; }
            using (var stream = File.OpenWrite(destination))
            {
                client.DownloadObjectAsync(asset.assetCategory.Value, asset.Thumbnail, stream, progress: progress).Wait();
            }
        }

        /// <summary>
        /// Upload an asset to the storage server.
        /// </summary>
        /// <param name="assetManifest">The path of the JSON manifest</param>
        /// <param name="asset">The path of the asset</param>
        /// <param name="assetThumbnail">The path of the thumbnail of the asset</param>
        /// <param name="progress">An array of IProgress objects to report download activities in this specific order: Manifest, Thumbnail, Asset.</param>
        public void UploadAsset(string assetManifest, string assetThumbnail, string asset, IProgress<IUploadProgress>[] progress = null)
        {
            if (client == null) throw new Exception("You must authenticate first.");
            var options = new UploadObjectOptions();
            options.PredefinedAcl = PredefinedObjectAcl.ProjectPrivate;
            Asset assetToUpload = ValidateManifest(assetManifest);
            if(!string.IsNullOrEmpty(assetToUpload.ConvertError))
            {
                throw new Exception($"Invalid asset manifest: {assetToUpload.ConvertError}");
            }
            using (var stream = File.OpenRead(assetManifest))
            {
                client.UploadObjectAsync(assetToUpload.Category, Path.GetFileName(assetManifest), null, stream, options: options, progress: progress[0]).Wait();
            }
            using (var stream = File.OpenRead(assetThumbnail))
            {
                client.UploadObjectAsync(assetToUpload.Category, assetToUpload.Thumbnail, null, stream, options: options, progress: progress[1]).Wait();
            }
            using (var stream = File.OpenRead(asset))
            {
                client.UploadObjectAsync(assetToUpload.Category, assetToUpload.AssetName, null, stream, options: options, progress: progress[2]).Wait();
            }
        }

        /// <summary>
        /// Deletes an asset from the storage server.
        /// </summary>
        /// <param name="assetManifest">The manifest file of the asset.</param>
        public void DeleteAsset(string assetManifest)
        {
            if (client == null) throw new Exception("You must authenticate first.");
            Asset assetToDelete = ValidateManifest(assetManifest);
            client.DeleteObjectAsync(assetToDelete.Category, assetManifest).Wait();
            client.DeleteObjectAsync(assetToDelete.Category, assetToDelete.AssetName).Wait();
            client.DeleteObjectAsync(assetToDelete.Category, assetToDelete.Thumbnail).Wait();            
        }

        Asset ValidateManifest(string manifest)
        {
            Asset asset;
            try
            {
                asset = JsonConvert.DeserializeObject<Asset>(File.ReadAllText(manifest));
            }
            catch (Exception ex)
            {
                return new Asset(ex.Message);
            }
            return asset;
        }
    }
}
