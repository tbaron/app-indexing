using Newtonsoft.Json;

namespace AppIndexing.Models
{
    public class PageSchemaPotentialAction
    {
        [JsonProperty("@type")]
        public string Type { get; set; }

        [JsonProperty("target")]
        public string Target { get; set; }
    }

    public class PageSchemaModel
    {
        [JsonProperty("potentialAction")]
        public PageSchemaPotentialAction PotentialAction { get; set; }
    }
}
