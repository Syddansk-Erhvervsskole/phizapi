namespace phizapi.Models.User
{
    public class UserUpdate<T>
    {
        public string user_id { get; set; }

        public T value { get; set; }

        public DateTime updated_at = DateTime.Now;
    }
}
