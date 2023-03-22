using Newtonsoft.Json;

namespace Rick_Player.Main.Data;

public class TrackJson
{
    // Root myDeserializedClass = JsonConvert.DeserializeObject<Root>(myJsonResponse);
    [JsonProperty("kind")] public string kind { get; set; }

    [JsonProperty("etag")] public string etag { get; set; }

    [JsonProperty("items")] public List<Item> items { get; set; }

    public class Item
    {
        [JsonProperty("kind")] public string kind { get; set; }

        [JsonProperty("etag")] public string etag { get; set; }

        [JsonProperty("id")] public string id { get; set; }

        [JsonProperty("snippet")] public Snippet snippet { get; set; }
    }

    public class Snippet
    {
        [JsonProperty("publishedAt")] public DateTime publishedAt { get; set; }

        [JsonProperty("channelId")] public string channelId { get; set; }

        [JsonProperty("title")] public string title { get; set; }

        [JsonProperty("description")] public string description { get; set; }

        [JsonProperty("thumbnails")] public Thumbnails thumbnails { get; set; }

        [JsonProperty("channelTitle")] public string channelTitle { get; set; }

        [JsonProperty("tags")] public List<string> tags { get; set; }

        [JsonProperty("categoryId")] public string categoryId { get; set; }

        [JsonProperty("liveBroadcastContent")] public string liveBroadcastContent { get; set; }

        [JsonProperty("localized")] public Localized localized { get; set; }
    }

    public class Thumbnails
    {
        [JsonProperty("default")] public Default @default { get; set; }

        [JsonProperty("medium")] public Medium medium { get; set; }

        [JsonProperty("high")] public High high { get; set; }

        [JsonProperty("standard")] public Standard standard { get; set; }

        [JsonProperty("maxres")] public Maxres maxres { get; set; }
    }

    public class Default
    {
        [JsonProperty("url")] public string url { get; set; }

        [JsonProperty("width")] public int width { get; set; }

        [JsonProperty("height")] public int height { get; set; }
    }

    public class Medium
    {
        [JsonProperty("url")] public string url { get; set; }

        [JsonProperty("width")] public int width { get; set; }

        [JsonProperty("height")] public int height { get; set; }
    }
    
    public class High
    {
        [JsonProperty("url")] public string url { get; set; }

        [JsonProperty("width")] public int width { get; set; }

        [JsonProperty("height")] public int height { get; set; }
    }
    
    public class Standard
    {
        [JsonProperty("url")] public string url { get; set; }

        [JsonProperty("width")] public int width { get; set; }

        [JsonProperty("height")] public int height { get; set; }
    }
    
    public class Maxres
    {
        [JsonProperty("url")] public string url { get; set; }

        [JsonProperty("width")] public int width { get; set; }

        [JsonProperty("height")] public int height { get; set; }
    }

    public class Localized
    {
        [JsonProperty("title")] public string title { get; set; }

        [JsonProperty("description")] public string description { get; set; }
    }
}