namespace phizapi.Models.Device
{
    // Device is the return element when a user request a device or list of devices
    public class Device
    {
        public string id { get; set; } = Guid.NewGuid().ToString();
        public string name { get; set; }
        public string description { get; set; }
        public string ip { get; set; }
        public string mac { get; set; }
    }
}
