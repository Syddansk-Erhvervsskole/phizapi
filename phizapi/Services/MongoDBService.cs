using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Core.Configuration;

namespace phizapi.Services
{
    public class MongoDBService
    {
        static string connectionString = "mongodb+srv://Admin:OlJHF17jm81paCWW@phizrecondb.1sfrmel.mongodb.net/";
        public static List<T> GetList<T>(string collection)
        {
            // Connection string (replace with your MongoDB URI)

            var client = new MongoClient(connectionString);

            // Access a specific database (it will create it if it doesn't exist)
            var database = client.GetDatabase("PhizRecon");

            // Access a collection (like a table)
            var collectionValue = database.GetCollection<T>(collection);
            
            return collectionValue.Find(Builders<T>.Filter.Empty).ToList();
            
        }
        public static IMongoCollection<T> GetCollection<T>(string collection)
        {

            var client = new MongoClient(connectionString);

            var database = client.GetDatabase("PhizRecon");

           return database.GetCollection<T>(collection);

        }


        public static void Remove<T>(T elem, string collectionName)
        {
            var client = new MongoClient(connectionString);
            var database = client.GetDatabase("PhizRecon");
            var collection = database.GetCollection<T>(collectionName);

 
            var idProp = typeof(T).GetProperty("id") ?? typeof(T).GetProperty("Id");
            if (idProp == null)
                throw new Exception("Type T must have an 'id' property");

            var idValue = idProp.GetValue(elem);

            var filter = Builders<T>.Filter.Eq("id", BsonValue.Create(idValue));
            collection.DeleteOne(filter);
        }


        public static void Upload<T>(T elem, string collection)
        {

            var client = new MongoClient(connectionString);

            var database = client.GetDatabase("PhizRecon");

            var collectionValue = database.GetCollection<T>(collection);
           
            collectionValue.InsertOne(elem);

        }

    }
}
