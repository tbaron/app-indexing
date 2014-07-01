using Newtonsoft.Json;

namespace AppIndexing.Models
{
    public class DeeplinkRequest
    {
        [JsonProperty("urls")]
        public string[] Urls { get; set; }
    }
}
