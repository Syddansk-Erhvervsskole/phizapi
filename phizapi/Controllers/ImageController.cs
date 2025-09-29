using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using phizapi.Services;
using System;
using System.Drawing;
using System.Net;
using System.Net.Mime;
using XAct;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace phizapi.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class ImageController : ControllerBase
    {

        private readonly FtpService _ftpService;
        private readonly InferenceSession _session;
        private readonly MongoDBService _dbService;
        public ImageController(FtpService ftpService, MongoDBService dbService)
        {
            _ftpService = ftpService;
            _dbService = dbService;
            _session = new InferenceSession("facenet.onnx");
        }

        [HttpGet("List")]
        [Authorize(Roles = "Admin")]
        public IActionResult List()
        {
            try
            {
                var images = _dbService.GetList<ImageObjectSimple>("Images");


                return Ok(images);
            }
            catch (WebException ex)
            {
                return NotFound($"File not found on FTP server. {ex.Message}");
            }
        }

        [HttpGet("List/Full")]
        [Authorize(Roles = "Admin")]
        public IActionResult ListFull()
        {
            try
            {
                var images = _dbService.GetList<ImageObject>("Images");


                return Ok(images);
            }
            catch (WebException ex)
            {
                return NotFound($"File not found on FTP server. {ex.Message}");
            }
        }

        [HttpGet("List/Embeddings")]
        [Authorize(Roles = "Admin")]
        public IActionResult ListEmbeddings()
        {
            try
            {
                var images = _dbService.GetList<EmbeddingsImage>("Images");

                return Ok(images);
            }
            catch (WebException ex)
            {
                return NotFound($"File not found on FTP server. {ex.Message}");
            }
        }


        [HttpGet("View/{fileName}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> ViewFile(string fileName)
        {
            try
            {
                var bytes = await _ftpService.GetBytes(fileName);

                if (bytes.Length == 0)
                    return NotFound(new { message = $"File {fileName} not found on FTP server" });

                var provider = new FileExtensionContentTypeProvider();
                string contentType;
                if (!provider.TryGetContentType(fileName, out contentType))
                {
                    contentType = "application/octet-stream";
                }

                return File(bytes, contentType);
            }
            catch (WebException ex)
            {
                return Problem($"An error occured. Message: {ex.Message}");
            }
        }

        [HttpGet("{id}")]
        [Authorize(Roles = "Admin")]
        public IActionResult Get(string id)
        {
            try
            {
                var image = _dbService.GetCollection<ImageObject>("Images").Find(x => x.id == id).FirstOrDefault();
                
                if(image != null)
                {
                    return Ok(new { image });
                }
                return NotFound();
           
            }
            catch (WebException ex)
            {
                return Problem($"An error occured. Message: {ex.Message}");
            }
        }


        [HttpPost()]
        [Authorize(Roles = "Admin,Device" )]
        public async Task<IActionResult> Upload(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest("No file uploaded.");

            // Validate file type by content type
            if (!file.ContentType.StartsWith("image/"))
                return BadRequest("Only image files are allowed.");

            var extension = Path.GetExtension(file.FileName);
            //Image ID
            Guid id = Guid.NewGuid();
            var (width, height) = ImageObject.GetImageDimensions(file);


            Tensor<float> input = ImageObject.ImageToTensor(file.OpenReadStream().ReadAllBytes());
            var inputs = new List<NamedOnnxValue> { NamedOnnxValue.CreateFromTensor("image_input", input) };
            float[] embedding;
            using (var results = _session.Run(inputs))
            {
                embedding = results.First().AsEnumerable<float>().ToArray();
            }


            var matchingEmbbedings = _dbService
                .GetList<EmbeddingsImage>("Images")
                .Where(x => !string.IsNullOrEmpty(x.person) && ImageObject.CosineSimilarity(x.embedding, embedding) > 0.8)
                .OrderByDescending(x => ImageObject.CosineSimilarity(x.embedding, embedding));

            var person = matchingEmbbedings.FirstOrDefault()?.person;

            var imageObject = new ImageObject()
            {
                id = id.ToString(),
                server_name = $"{ id }{ extension }",
                original_name = file.FileName,
                url = $"{Request.Scheme}://{Request.Host}/image/view/{id}{extension}",
                size = file.Length,
                filetype = file.ContentType,
                createdTime = DateTime.Now,
                width = width,
                height = height,
                person = person,
                embedding = embedding
            };


            try
            {
                _dbService.Upload(imageObject, "Images");

                await _ftpService.CreateFileAsync(file, $"{id}{extension}");

                return Ok(new { image = imageObject });
            }
            catch (Exception ex)
            {
                _dbService.Remove(imageObject, "Images");
              
                return StatusCode(500, $"FTP upload failed: {ex.Message}");
            }
            finally
            {
                GC.Collect();
            }
            

        }


        [HttpPatch("{id}/Update/Person")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UpdatePerson([FromBody]ImagePersonUpdate personUpdate, string id)
        {
            try
            {
                var collection = _dbService.GetCollection<ImageObject>("Images");
                var image = collection.Find(x => x.id == id).FirstOrDefault();
                var person = _dbService.GetCollection<Person>("Persons").Find(x => x.id == personUpdate.person_id).FirstOrDefault();

                if (image != null && person != null)
                {

                    if (person != null)
                    {
                        collection.UpdateOne(X => X.id == id, Builders<ImageObject>.Update
                        .Set(u => u.person, person.id));
                    }
                    else
                    {
                        return NotFound("Image or person not found");
                    }

                    image.person = person.id;
                    return Ok(new { image });
                }
                return NotFound();

            }
            catch (WebException ex)
            {
                return Problem($"An error occured. Message: {ex.Message}");
            }
        }


        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteFile(string id)
        {
            try
            {
                var imageObject = _dbService.GetList<ImageObject>("Images").FirstOrDefault(x => x.id == id);
                
                if (imageObject != null)
                {
                    _dbService.Remove(imageObject, "Images");
                }
                else
                {
                    return BadRequest(new { message = "Image with given id could not be found" });
                }

                try
                {

                    await _ftpService.DeleteFileAsync(Path.GetFileName(imageObject.url));

                    return Ok(new { message = "File removed" });
                }
                catch (WebException ex)
                {
                    _dbService.Upload(imageObject, "Images");
                    return Problem($"Failed to remove file from ftp");
                }
            }
            catch
            {
                return Problem($"Failed to remove entry from mongoDB");
            }

          
        }


    

    }

    [BsonIgnoreExtraElements]
    public class EmbeddingsImage
    {
        public string id { get; set; }
        public string person { get; set; }
        public float[] embedding { get; set; }
    }

    public class ImagePersonUpdate
    {
        public string person_id { get; set; }

    }

    [BsonIgnoreExtraElements]
    public class ImageObjectSimple
    {
        public string id { get; set; }
        public string original_name { get; set; }
        public string url { get; set; }
        public string filetype { get; set; }
        public DateTime createdTime { get; set; }
        public string? person { get; set; }
    }
    public class ImageObject
    {
        public string id { get; set; }
        public string server_name { get; set; }
        public string original_name { get; set; }
        public string url { get; set; }
        public float size { get; set; }
        public string filetype { get; set; }
        public DateTime createdTime { get; set; }
        public int width { get; set; }
        public int height { get; set; }
        public string? person { get; set; }
        public float[]? embedding { get; set; }

        public static (int width, int height) GetImageDimensions(IFormFile file)
        {
            using var stream = file.OpenReadStream();
            using var image = System.Drawing.Image.FromStream(stream);
            return (image.Width, image.Height);
        }

        public static float CosineSimilarity(float[] a, float[] b)
        {
            float dot = 0, magA = 0, magB = 0;
            for (int i = 0; i < a.Length; i++)
            {
                dot += a[i] * b[i];
                magA += a[i] * a[i];
                magB += b[i] * b[i];
            }
            return dot / (float)(Math.Sqrt(magA) * Math.Sqrt(magB));
        }

        public static Tensor<float> ImageToTensor(byte[] bytes)
        {
            using var ms = new MemoryStream(bytes);
            using var bitmap = new Bitmap(ms);
            using var resized = new Bitmap(bitmap, new Size(160, 160));

            var tensor = new DenseTensor<float>(new[] { 1, 160, 160, 3 });

            for (int y = 0; y < 160; y++)
            {
                for (int x = 0; x < 160; x++)
                {
                    var color = resized.GetPixel(x, y);

                    tensor[0, y, x, 0] = (color.R / 127.5f) - 1f; // R
                    tensor[0, y, x, 1] = (color.G / 127.5f) - 1f; // G
                    tensor[0, y, x, 2] = (color.B / 127.5f) - 1f; // B
                }
            }

            return tensor;
        }


    }

}
