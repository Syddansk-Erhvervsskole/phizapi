using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Core.Configuration;
using System.Net;

namespace phizapi.Services
{
    public class MongoDBService
    {

        private IMongoDatabase database;
        private IConfiguration _config;

        public MongoDBService(IConfiguration config)
        {
            _config = config;
            var settings = MongoClientSettings.FromConnectionString(_config["MongoDB:ConnectionString"]);
            settings.ServerApi = new ServerApi(ServerApiVersion.V1);

            var client = new MongoClient(settings);
            database = client.GetDatabase("PhizRecon");
        }

        public List<T> GetList<T>(string collection)
        {

            // Access a collection (like a table)
            var collectionValue = database.GetCollection<T>(collection);
            
            return collectionValue.Find(Builders<T>.Filter.Empty).ToList();
            
        }
        public IMongoCollection<T> GetCollection<T>(string collection)
        {

           return database.GetCollection<T>(collection);

        }


        public void Remove<T>(T elem, string collectionName)
        {

            var collection = database.GetCollection<T>(collectionName);

 
            var idProp = typeof(T).GetProperty("id") ?? typeof(T).GetProperty("Id");
            if (idProp == null)
                throw new Exception("Type T must have an 'id' property");

            var idValue = idProp.GetValue(elem);

            var filter = Builders<T>.Filter.Eq("id", BsonValue.Create(idValue));
            collection.DeleteOne(filter);
        }


        public void Upload<T>(T elem, string collection)
        {

            var collectionValue = database.GetCollection<T>(collection);
           
            collectionValue.InsertOne(elem);

        }

    }
}
