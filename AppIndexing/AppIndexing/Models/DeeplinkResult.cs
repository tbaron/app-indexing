using Newtonsoft.Json;

namespace AppIndexing.Models
{
    public class DeeplinkResult
    {
        [JsonProperty("links")]
        public string[][] Links { get; set; }
    }
}