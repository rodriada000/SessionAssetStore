using System;
using System.IO;
using System.Collections.Generic;
using Newtonsoft.Json;
using System.Reflection;
using System.Diagnostics;
using System.Threading.Tasks;
using Amazon;
using Amazon.S3;
using Amazon.Runtime;
using Amazon.S3.Model;
using Amazon.S3.Util;
using System.Linq;
using Amazon.S3.Transfer;
using System.Threading;

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
        public IAmazonS3 client;

        /// <summary>
        /// Transfer helper for aws
        /// </summary>
        public TransferUtility transfer;

        /// <summary>
        /// Authenticates to the Amazon S3 API. Provide custom credentials to authenticate with modders rights.
        /// </summary>
        /// <param name="credentialsFile">The credentials to use to authenticate. Leave null for default read-only.</param>
        public void Authenticate(string credentialsFile = null)
        {
            credentialsFile = credentialsFile == null ? "modmanager.csv" : credentialsFile;
            var creds = ReadCSV(credentialsFile);
            var endpoint = "https://s3.wasabisys.com"; //US-East-1 endpoint
            var config = new AmazonS3Config { ServiceURL = endpoint };

            var s3Credentials = new BasicAWSCredentials(creds["Access Key Id"], creds["Secret Access Key"]);
            client = new AmazonS3Client(s3Credentials, config);     
            transfer = new TransferUtility(client);
        }

        Dictionary<string, string> ReadCSV(string filePath)
        {
            if (!File.Exists(filePath)) throw new FileNotFoundException();
            var lines = File.ReadAllLines(filePath);
            if (lines.Length < 2) throw new Exception("Bad credentials file format.");
            var header = lines[0].Split(',');
            var data = lines[1].Split(',');
            return header.Zip(data, (h, d) => new { h, d }).ToDictionary(item => item.h, item => item.d);
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
        public async Task GetAssetManifestsAsync(AssetCategory assetCategory, EventHandler<WriteObjectProgressArgs> progress = null)
        {
            if (client == null) throw new Exception("You must authenticate first.");
            string manifestPath = Path.Combine(MANIFESTS_TEMP, assetCategory.Value);
            // Delete the existing manifests if they exist, prevents old assets from being redownloaded.
            if (Directory.Exists(manifestPath))
            {
                Directory.Delete(manifestPath, true);
            }
            Directory.CreateDirectory(manifestPath);
            var buckets = await client.ListBucketsAsync().ConfigureAwait(false);

            foreach (var bucket in buckets.Buckets)
            {
                var files = await client.ListObjectsAsync(bucket.BucketName).ConfigureAwait(false);
                foreach (var file in files.S3Objects)
                {
                    if (file.Key.EndsWith(".json"))
                    {
                        GetObjectMetadataResponse metadataResponse = await client.GetObjectMetadataAsync(bucket.BucketName, file.Key).ConfigureAwait(false);
                        var metadata = metadataResponse.Metadata;

                        if (metadata != null)
                        {
                            if (metadata["category"] == assetCategory.Value)
                            {
                                using (var response = await client.GetObjectAsync(bucket.BucketName, file.Key).ConfigureAwait(false))
                                {
                                    response.WriteObjectProgressEvent += progress;
                                    await response.WriteResponseStreamToFileAsync(Path.Combine(manifestPath, file.Key), false, CancellationToken.None).ConfigureAwait(false);
                                    response.WriteObjectProgressEvent -= progress;
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
        public async Task GetAllAssetManifestsAsync()
        {     
            foreach (AssetCategory cat in GetAllCategories())
            {
                await GetAssetManifestsAsync(cat).ConfigureAwait(false);     
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
                using (FileStream stream = File.Open(file, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    using (StreamReader reader = new StreamReader(stream))
                    {
                        buffer.Add(JsonConvert.DeserializeObject<Asset>(reader.ReadToEnd()));
                    }
                }
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
        public async Task DownloadAssetAsync(Asset asset, string destination, EventHandler<WriteObjectProgressArgs> progress = null, bool update = false)
        {
            if (client == null) throw new Exception("You must authenticate first.");
            if (File.Exists(destination) && !update) { return; }

            var downloadRequest = new TransferUtilityDownloadRequest()
            {
                BucketName = await GetBucketNameAsync(asset.AssetName).ConfigureAwait(false),
                Key = asset.AssetName,
                FilePath = destination
            };

            downloadRequest.WriteObjectProgressEvent += progress;
            await transfer.DownloadAsync(downloadRequest).ConfigureAwait(false);
            downloadRequest.WriteObjectProgressEvent -= progress;
        }

        /// <summary>
        /// Download the thumbnail of an asset.
        /// </summary>
        /// <param name="asset">The asset to download.</param>
        /// <param name="destination">Where to save the thumbnail.</param>
        /// <param name="progress">An IProgress object to report download activities.</param>
        /// <param name="update">Redownload and overwrite an existing thumbnail of the same name.</param>
        public async Task DownloadAssetThumbnailAsync(Asset asset, string destination, EventHandler<WriteObjectProgressArgs> progress = null, bool update = false)
        {
            if (client == null) throw new Exception("You must authenticate first.");
            if (File.Exists(destination) && !update) { return; }

            var downloadRequest = new TransferUtilityDownloadRequest()
            {
                BucketName = await GetBucketNameAsync(asset.AssetName).ConfigureAwait(false),
                Key = asset.Thumbnail,
                FilePath = destination
            };
            downloadRequest.WriteObjectProgressEvent += progress;
            await transfer.DownloadAsync(downloadRequest).ConfigureAwait(false);
            downloadRequest.WriteObjectProgressEvent -= progress;
        }

        async Task<string> GetBucketNameAsync(string fileName)
        {
            var bucketResponse = await client.ListBucketsAsync().ConfigureAwait(false);

            foreach (var bucket in bucketResponse.Buckets)
            {
                var files = await client.ListObjectsAsync(bucket.BucketName).ConfigureAwait(false);
                foreach(var file in files.S3Objects)
                {
                    if (file.Key== fileName)
                    {
                        return bucket.BucketName;
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
        /// <param name="bucketName"> Name of storage bucket to upload to </param>
        /// <param name="progress"> EventHandler delegate to report progress of upload </param>
        public async Task UploadAssetAsync(string assetManifest, string assetThumbnail, string asset, string bucketName, EventHandler<UploadProgressArgs> progress = null)
        {
            if (client == null) throw new Exception("You must authenticate first.");
            Asset assetToUpload = ValidateManifest(assetManifest);
            if (!string.IsNullOrEmpty(assetToUpload.ConvertError))
            {
                throw new Exception($"Invalid asset manifest: {assetToUpload.ConvertError}");
            }

            var manifestObject = new TransferUtilityUploadRequest()
            {
                BucketName = bucketName,
                Key= Path.GetFileName(assetManifest)
            };
            var thumbnailObject = new TransferUtilityUploadRequest()
            {
                BucketName = bucketName,
                Key = assetToUpload.Thumbnail
            };
            var assetObject = new TransferUtilityUploadRequest()
            {
                BucketName = bucketName,
                Key= assetToUpload.AssetName                
            };

            using (var stream = File.OpenRead(assetManifest))
            {
                manifestObject.InputStream = stream;
                manifestObject.Metadata.Add("category", assetToUpload.assetCategory.Value);
                manifestObject.UploadProgressEvent += progress;
                await transfer.UploadAsync(manifestObject).ConfigureAwait(false);
                manifestObject.UploadProgressEvent -= progress;
            }
            using (var stream = File.OpenRead(assetThumbnail))
            {
                thumbnailObject.InputStream = stream;
                thumbnailObject.Metadata.Add("category", assetToUpload.assetCategory.Value);
                thumbnailObject.UploadProgressEvent += progress;
                await transfer.UploadAsync(thumbnailObject).ConfigureAwait(false);
                thumbnailObject.UploadProgressEvent -= progress;
            }
            using (var stream = File.OpenRead(asset))
            {
                assetObject.InputStream = stream;
                assetObject.Metadata.Add("category", assetToUpload.assetCategory.Value);
                assetObject.UploadProgressEvent += progress;
                await transfer.UploadAsync(assetObject).ConfigureAwait(false);
                assetObject.UploadProgressEvent -= progress;
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
            DeleteAsset(bucketName, manifestName, assetToDelete);        
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
        public async Task<List<string>> ListBucketsAsync()
        {
            var result = new List<string>();
            var bucketResponse = await client.ListBucketsAsync().ConfigureAwait(false); // use .ConfigureAwait(false) to avoid deadlocks ... 
            
            foreach(var bucket in bucketResponse.Buckets)
            {
                if(!bucket.BucketName.StartsWith("session-"))
                {
                    result.Add(bucket.BucketName);
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
