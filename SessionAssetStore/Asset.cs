using System;
using System.Diagnostics;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace SessionAssetStore
{
    /// <summary>
    /// Class containing an Asset object
    /// </summary>
    public class Asset
    {
        /// <summary>
        /// The display name of the asset
        /// </summary>
        public string Name { get; }
        /// <summary>
        /// A short description of the asset
        /// </summary>
        public string Description { get; }
        /// <summary>
        /// The author of the asset
        /// </summary>
        public string Author { get; }
        /// <summary>
        /// The version of the asset
        /// </summary>
        public string Version { get; }
        /// <summary>
        /// The name of the asset on the storage server
        /// </summary>
        public string AssetName { get; }
        /// <summary>
        /// The name of the thumbnail on the storage server
        /// </summary>
        public string Thumbnail { get; }
        /// <summary>
        /// The category of the asset as a string
        /// </summary>
        public string Category { get; }

        [JsonIgnore]
        internal AssetCategory assetCategory { get; }
        [JsonIgnore]
        internal string ConvertError{ get;  }

        /// <summary>
        /// Default constructor
        /// </summary>
        [JsonConstructor]
        public Asset(string Name, string Description, string Author, string Version, string AssetName, string Thumbnail, string Category)
        {
            this.Name = Name;
            this.Description = Description;
            this.Author = Author;
            this.Version = Version;
            this.AssetName = AssetName;
            this.Thumbnail = Thumbnail;
            this.Category = Category;
            assetCategory = AssetCategory.FromString(Category);
        }

        public Asset(string ConvertError)
        {
            this.ConvertError = ConvertError;
        }
        /// <summary>
        /// Dumps a json string of the asset
        /// </summary>
        /// <returns>JSON string</returns>
        public override string ToString()
        {
            return JsonConvert.SerializeObject(this);
        }

    }

    /// <summary>
    /// Class to replace an enum to get string values.
    /// </summary>
    public class AssetCategory
    {
        public string Value { get; set; }
        private AssetCategory(string value) { Value = value; }

        public static AssetCategory Maps { get { return new AssetCategory("session-maps"); } }
        public static AssetCategory Griptapes { get { return new AssetCategory("session-griptapes"); } }
        public static AssetCategory Clothes { get { return new AssetCategory("session-clothes"); } }
        public static AssetCategory Hats { get { return new AssetCategory("session-hats"); } }
        public static AssetCategory Shirts { get { return new AssetCategory("session-shirts"); } }
        public static AssetCategory Pants { get { return new AssetCategory("session-pants"); } }
        public static AssetCategory Shoes { get { return new AssetCategory("session-shoes"); } }
        public static AssetCategory Decks { get { return new AssetCategory("session-decks"); } }
        public static AssetCategory Trucks { get { return new AssetCategory("session-trucks"); } }
        public static AssetCategory Wheels { get { return new AssetCategory("session-wheels"); } }

        /// <summary>
        /// Converts a string to an AssetCategory
        /// </summary>
        /// <param name="category">Name of the category</param>
        /// <returns>AssetCategory object</returns>
        public static AssetCategory FromString(string category)
        {
            switch(category)
            {
                case ("session-maps"):
                    return Maps;
                case ("session-griptapes"):
                    return Griptapes;
                case ("session-clothes"):
                    return Clothes;
                case ("session-hats"):
                    return Hats;
                case ("session-shirts"):
                    return Shirts;
                case ("session-pants"):
                    return Pants;
                case ("session-shoes"):
                    return Shoes;
                case ("session-decks"):
                    return Decks;
                case ("session-trucks"):
                    return Trucks;
                case ("session-wheels"):
                    return Wheels;
                default:
                    throw new Exception($"Invalid category provided: {category}");
            }
        }
    }
}
