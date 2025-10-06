namespace phizapi.Models.Person
{
    // Person is the main person object used in the database
    public class Person
    {
        public string id { get; set; } = Guid.NewGuid().ToString();
        public string name { get; set; } = null!;
        public List<CustomDetails> custom_details { get; set; } = new List<CustomDetails>();
    }
   
}
