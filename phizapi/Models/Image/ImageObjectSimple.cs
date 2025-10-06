using MongoDB.Bson.Serialization.Attributes;

namespace phizapi.Models.Image
{
    //ImageObjectSimple is a simplified version of ImageObject for listing images without embeddings

    [BsonIgnoreExtraElements]
    public class ImageObjectSimple
    {
        public string id { get; set; }
        public string original_name { get; set; }
        public string url { get; set; }
        public string filetype { get; set; }
        public DateTime createdTime { get; set; }
        public string? person { get; set; }
    }
}
