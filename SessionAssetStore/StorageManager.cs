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
using System.Threading.Tasks;

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

        public static readonly string PROJECTID = "sessionassetstore";

        /// <summary>
        /// Authenticates to the Google Cloud Storage API. Provide custom credentials to authenticate with modders rights.
        /// </summary>
        /// <param name="credentialsFile">The credentials to use to authenticate. Leave null for default read-only.</param>
        public async Task Authenticate(string credentialsFile = null)
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
            var buckets = client.ListBuckets(PROJECTID);
            foreach (var bucket in buckets)
            {
                var files = client.ListObjects(bucket.Name);
                foreach (var file in files)
                {
                    if (file.Name.EndsWith(".json"))
                    {
                        Debug.WriteLine(bucket.Name);
                        if (file.Metadata != null && file.Metadata.ContainsKey("category"))
                        {
                            if (file.Metadata["category"] == assetCategory.Value)
                            {
                                using (var stream = File.OpenWrite(Path.Combine(manifestPath, file.Name)))
                                {
                                    client.DownloadObjectAsync(file, stream, progress: progress).Wait();
                                }
                            }
                        }
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
            string pathToFolder = Path.Combine(MANIFESTS_TEMP, assetCategory.Value);
            Directory.CreateDirectory(pathToFolder);

            foreach (string file in Directory.GetFiles(pathToFolder, "*.json"))
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
                client.DownloadObjectAsync(GetBucketName(asset.AssetName), asset.AssetName, stream, progress: progress).Wait();
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
                client.DownloadObjectAsync(GetBucketName(asset.AssetName), asset.Thumbnail, stream, progress: progress).Wait();
            }
        }

        string GetBucketName(string fileName)
        {
            var buckets = client.ListBuckets(PROJECTID);
            foreach(var bucket in buckets)
            {
                var files = client.ListObjects(bucket.Name);
                foreach(var file in files)
                {
                    if (file.Name == fileName)
                    {
                        return bucket.Name;
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// Upload an asset to the storage server.
        /// </summary>
        /// <param name="assetManifest"> absolute path to the json manifest file </param>
        /// <param name="assetThumbnail"> absolute path to the thumbnail image file </param>
        /// <param name="asset"> absolute path to the asset file (e.g. a .zip file) </param>
        /// <param name="bucketName"> Name of google cloud storage bucket to upload to </param>
        /// <param name="progress"> array of IUpload representing progress for [Manifest, Thumbnail, File] in that order. </param>
        public void UploadAsset(string assetManifest, string assetThumbnail, string asset, string bucketName, IProgress<IUploadProgress>[] progress = null)
        {
            if (client == null) throw new Exception("You must authenticate first.");
            Asset assetToUpload = ValidateManifest(assetManifest);
            if (!string.IsNullOrEmpty(assetToUpload.ConvertError))
            {
                throw new Exception($"Invalid asset manifest: {assetToUpload.ConvertError}");
            }

            var manifestObject = new Google.Apis.Storage.v1.Data.Object()
            {
                Bucket = bucketName,
                Name = Path.GetFileName(assetManifest),
                Metadata = new Dictionary<string, string>
                {
                    { "category", assetToUpload.assetCategory.Value}
                }
            };
            var thumnailObject = new Google.Apis.Storage.v1.Data.Object()
            {
                Bucket = bucketName,
                Name = assetToUpload.Thumbnail,
                Metadata = new Dictionary<string, string>
                {
                    { "category", assetToUpload.assetCategory.Value}
                }
            };
            var assetObject = new Google.Apis.Storage.v1.Data.Object()
            {
                Bucket = bucketName,
                Name = assetToUpload.AssetName,
                Metadata = new Dictionary<string, string>
                {
                    { "category", assetToUpload.assetCategory.Value}
                }
            };

            using (var stream = File.OpenRead(assetManifest))
            {
                client.UploadObjectAsync(manifestObject, stream, progress: progress[0]).Wait();
            }
            using (var stream = File.OpenRead(assetThumbnail))
            {
                client.UploadObjectAsync(thumnailObject, stream, progress: progress[1]).Wait();
            }
            using (var stream = File.OpenRead(asset))
            {
                client.UploadObjectAsync(assetObject, stream, progress: progress[2]).Wait();
            }
        }

        /// <summary>
        /// Deletes an asset from the storage server.
        /// </summary>
        /// <param name="manifestName">The manifest file of the asset.</param>
        public void DeleteAsset(string bucketName, string manifestName, string absolutePathToManifest)
        {
            if (client == null) throw new Exception("You must authenticate first.");
            Asset assetToDelete = ValidateManifest(absolutePathToManifest);
            client.DeleteObjectAsync(bucketName, manifestName).Wait();
            client.DeleteObjectAsync(bucketName, assetToDelete.AssetName).Wait();
            client.DeleteObjectAsync(bucketName, assetToDelete.Thumbnail).Wait();            
        }

        /// <summary>
        /// Deletes an asset from the storage server.
        /// </summary>
        /// <param name="manifestName">name of the manifest file on the server.</param>
        /// <param name="assetToDelete"> Asset to delete </param>
        public void DeleteAsset(string bucketName, string manifestName, Asset assetToDelete)
        {
            if (client == null) throw new Exception("You must authenticate first.");
            client.DeleteObjectAsync(bucketName, manifestName).Wait();
            client.DeleteObjectAsync(bucketName, assetToDelete.AssetName).Wait();
            client.DeleteObjectAsync(bucketName, assetToDelete.Thumbnail).Wait();
        }

        /// <summary>
        /// List all custom buckets
        /// </summary>
        /// <returns></returns>
        public List<string> ListBuckets()
        {
            var result = new List<string>();
            var buckets = client.ListBuckets(PROJECTID);
            foreach(var bucket in buckets)
            {
                if(!bucket.Name.StartsWith("session-"))
                {
                    result.Add(bucket.Name);
                }
            }
            return result;
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
