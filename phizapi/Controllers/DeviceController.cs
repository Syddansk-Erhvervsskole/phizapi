using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.ML.OnnxRuntime;
using MongoDB.Driver;
using phizapi.Services;
using System.Net;
using XAct.Messages;
using static phizapi.Controllers.DeviceController;

namespace phizapi.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class DeviceController : ControllerBase
    {
        private readonly EncryptionService _es;
        private readonly JwtTokenService _jwt;
        private readonly MongoDBService _dbService;

        public DeviceController(JwtTokenService jwt, EncryptionService es, MongoDBService dbService)
        {
            _es = es;
            _jwt = jwt;
            _dbService = dbService;
        }


        [HttpPost("")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Create([FromBody] CreateDevice createDevice)
        {
            try
            {
                var collection = _dbService.GetCollection<Device>("Devices");
                var existingCamera = collection.Find(x => x.mac == createDevice.mac);

                if(existingCamera != null)
                {
                    return BadRequest(new {message = "Device with same mac-address already exists", value = existingCamera });
                }

                var device = createDevice.Getdevice(_es);
                collection.InsertOne(device);



                return Ok(device);
            }
            catch (WebException ex)
            {
                return NotFound($"File not found on FTP server. {ex.Message}");
            }
        }

        [HttpPost("Login")]
        public async Task<IActionResult> Login([FromBody]DeviceLoginRequest deviceLoginRequest)
        {
            try
            {
                var sha256Password = _es.Sha256(deviceLoginRequest.mac_address);
                var collection = _dbService.GetCollection<Device>("Devices");
                var existingCamera = collection.Find(x => x.mac == deviceLoginRequest.mac_address).FirstOrDefault();
                if(existingCamera != null)
                {
                    var token = _jwt.GenerateToken(existingCamera.name, "Device", 24);

                    return Ok(new { message = token });
                }
                else
                {
                    return NotFound(new { message = "Device with given mac_adress not found" });
                }

            }
            catch (WebException ex)
            {
                return NotFound($"File not found on FTP server. {ex.Message}");
            }
        }


        public class DeviceLoginRequest()
        {
            public string mac_address { get; set; }
        }

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

        public class Device
        {

            public string id { get; set; } = Guid.NewGuid().ToString();
            public string name { get; set; }
            public string description { get; set; }
            public string ip { get; set; }
            public string mac { get; set; }
        }
    }
}
