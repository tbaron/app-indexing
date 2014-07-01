using System.Collections.Generic;
using Newtonsoft.Json;

namespace AppIndexing.Models
{
    public class DeeplinkResult
    {
        [JsonProperty("links")]
        public string[][] Links { get; set; }

        [JsonProperty("errors")]
        public ICollection<object> Errors { get; set; }
    }
}