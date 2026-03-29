using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Vocalith.Localization
{
    [Serializable]
    public class LocalizationPack
    {
        [JsonProperty("language")]
        public string Language;

        [JsonProperty("entries")]
        public Dictionary<string, string> Entries = new();
    }
}
