using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using phizapi.Services;

namespace phizapi.Controllers
{
    [ApiController]
    [Route("[controller]")]
    [Authorize]
    public class PersonController : ControllerBase
    {
        private static MongoDBService _dbService;

        public PersonController(MongoDBService dBService)
        {
            _dbService = dBService;
        }

        [HttpGet("List")]
        [Authorize(Roles = "Admin")]
        public IActionResult GetAll()
        {
             return Ok(_dbService.GetList<Person>("Persons"));
        }

        [HttpPost()]
        [Authorize(Roles = "Admin")]
        public IActionResult Create([FromBody] CreatePerson person)
        {
            var personValue = new Person()
            {
                name = person.name,
            };

            try
            {
                _dbService.Upload(personValue, "Persons");
            }
            catch (Exception ex)
            {
                return Problem($"Error occured {ex.Message}");
            }

            return Ok(personValue);
        }

        [HttpPatch("{id}/Custom_details")]
        [Authorize(Roles = "Admin")]
        public IActionResult AddCustomDetails([FromBody] CustomDetails data, string id)
        {
            Person person = default;

            try
            {
                var collections = _dbService.GetCollection<Person>("Persons");
                person = collections.Find(x => x.id == id).FirstOrDefault();

                if(person != null)
                {
                    if(person.custom_details == null)
                        person.custom_details = new List<CustomDetails>();

                    var exitingData = person.custom_details.FirstOrDefault(x => x.field_name == data.field_name);

                    if(exitingData != null)
                    {
                        exitingData.value = data.value;
                    }
                    else
                    {
                        person.custom_details.Add(data);
                     
                    }

                    collections.UpdateOne(X => X.id == id, Builders<Person>.Update.Set(u => u.custom_details, person.custom_details));
                }
                else
                {
                    return NotFound();
                }
            
            }
            catch (Exception ex)
            {
                return Problem($"Error occured {ex.Message}");
            }

            return Ok(person);
        }

        [HttpDelete("{id}/Custom_details")]
        [Authorize(Roles = "Admin")]
        public IActionResult DeleteCustomData([FromBody] CustomDetailsRemove data, string id)
        {
            Person person = default;

            try
            {
                var collections = _dbService.GetCollection<Person>("Persons");
                person = collections.Find(x => x.id == id).FirstOrDefault();

                if (person != null )
                {
                    var detail = person.custom_details.FirstOrDefault(x => x.field_name == data.field_name);

                    if(detail != null)
                    {
                        person.custom_details.Remove(detail);
                    }
                    else
                    {
                        return NotFound();
                    }

                    collections.UpdateOne(X => X.id == id, Builders<Person>.Update.Set(u => u.custom_details, person.custom_details));
                }
                else
                {
                    return NotFound();
                }

            }
            catch (Exception ex)
            {
                return Problem($"Error occured {ex.Message}");
            }

            return Ok(person);
        }


        [HttpDelete("")]
        [Authorize(Roles = "Admin")]
        public IActionResult Remove([FromBody]RemoveImage body)
        {

            try
            {
                _dbService.Remove(new Person() { id = body.id }, "Persons");

                var imageCollection = _dbService.GetCollection<ImageObject>("Images");

                if (body.RemoveAllImages)
                {
                    _dbService.GetCollection<ImageObject>("Images").DeleteMany(x => x.person == body.id);
                }
                else
                {
                    imageCollection.UpdateMany(x => x.person == body.id, Builders<ImageObject>.Update.Set(u => u.person, null));
                }

            }
            catch (Exception ex)
            {
                return Problem($"Error occured {ex.Message}");
            }

            return Ok(new {message= "Person has been removed"});
        }

    }


    public class RemoveImage
    {
        public string id { get; set; }

        public bool RemoveAllImages { get; set; }
    }

    public class Person
    {
        public string id { get; set; } = Guid.NewGuid().ToString();
        public string name { get; set; } = null!;
        public List<CustomDetails> custom_details { get; set; } = new List<CustomDetails>();
    }
    public class CreatePerson
    {
        public string name { get; set; } = "";
    }
    public class CustomDetails
    {
        public string field_name { get; set; }
        public string value { get; set; }
    }
    public class CustomDetailsRemove
    {
        public string field_name { get; set; }
    }
}
