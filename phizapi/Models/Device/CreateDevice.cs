using phizapi.Services;


namespace phizapi.Models.Device
{

    // CreateDevice is used to create a new device in the database
    public class CreateDevice
    {
        public string name { get; set; }
        public string description { get; set; }
        public string ip { get; set; }
        public string mac { get; set; }

        public Device Getdevice(EncryptionService es)
        {
            return new Device()
            {
                name = name,
                description = description,
                ip = ip,
                mac = es.Sha256(mac)
            };
        }
    }
}
