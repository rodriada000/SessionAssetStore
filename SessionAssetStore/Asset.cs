using System;
using Newtonsoft.Json;

namespace SessionAssetStore
{
    public class Asset
    {
        public string Name { get; }
        public string Description { get; }
        public string Author { get; }
        public string Version { get; }
        public string AssetName { get; }
        public string Thumbnail { get; }

        public Asset(string Name, string Description, string Author, string Version, string AssetName, string Thumbnail)
        {
            this.Name = Name;
            this.Description = Description;
            this.Author = Author;
            this.Version = Version;
            this.AssetName = AssetName;
            this.Thumbnail = Thumbnail;
        }
        public override string ToString()
        {
            return JsonConvert.SerializeObject(this);
        }

    }

    public class AssetCategory
    {
        public string Value { get; set; }
        private AssetCategory(string value) { Value = value; }

        public static AssetCategory Map { get { return new AssetCategory("session-maps"); } }
        public static AssetCategory Griptape { get { return new AssetCategory("session-maps"); } }
        public static AssetCategory Clothes { get { return new AssetCategory("session-maps"); } }
        public static AssetCategory Hats { get { return new AssetCategory("session-maps"); } }
        public static AssetCategory Shirts { get { return new AssetCategory("session-maps"); } }
        public static AssetCategory Pants { get { return new AssetCategory("session-maps"); } }
        public static AssetCategory Shoes { get { return new AssetCategory("session-maps"); } }
        public static AssetCategory Decks { get { return new AssetCategory("session-maps"); } }
        public static AssetCategory Trucks { get { return new AssetCategory("session-maps"); } }
        public static AssetCategory Wheels { get { return new AssetCategory("session-maps"); } }
    }
}
