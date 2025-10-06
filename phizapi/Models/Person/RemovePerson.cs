namespace phizapi.Models.Person
{
    // RemovePerson is used to remove a person from the database
    public class RemovePerson
    {
        public string id { get; set; }
        public bool DeleteAllImages { get; set; }
    }

}
