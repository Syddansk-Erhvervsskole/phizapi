using MongoDB.Bson.Serialization.Attributes;

namespace phizapi.Models.Image
{

    //Embeddings image is for getting an image and its embedding from the database to compare with other embeddings
    //Embeddings are usually only used within the api when an image is uploaded

    [BsonIgnoreExtraElements]
    public class EmbeddingsImage
    {
        public string id { get; set; }
        public string person { get; set; }
        public float[] embedding { get; set; }
    }

}
