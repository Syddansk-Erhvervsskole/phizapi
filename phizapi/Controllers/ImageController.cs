using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using phizapi.Services;
using System.Drawing;
using System.Net;
using System.Net.Mime;
using XAct;

namespace phizapi.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class ImageController : ControllerBase
    {

        private readonly FtpService _ftpService;
        private readonly InferenceSession _session;
        public ImageController(FtpService ftpService)
        {
            _ftpService = ftpService;

            _session = new InferenceSession("facenet.onnx");
        }

        [HttpGet("Checksim/{ImageID1}/{ImageID2}")]
        [Authorize]
        public async Task<IActionResult> CheckSim(string ImageID1, string ImageID2)
        {
            try
            {
                var images = MongoDBService.GetList<ImageObject>("Images");


                var image = images.FirstOrDefault(x => x.id == ImageID1);
                var image2 = images.FirstOrDefault(x => x.id == ImageID2);

                return Ok(ImageObject.CosineSimilarity(image.embedding, image2.embedding));
            }
            catch (WebException ex)
            {
                return NotFound($"File not found on FTP server. {ex.Message}");
            }
        }

        [HttpGet("List")]
        [Authorize]
        public async Task<IActionResult> List()
        {
            try
            {
                var images = MongoDBService.GetList<ImageObject>("Images");


                return Ok(images);
            }
            catch (WebException ex)
            {
                return NotFound($"File not found on FTP server. {ex.Message}");
            }
        }

        [HttpGet("List/Embeddings")]
        [Authorize]
        public async Task<IActionResult> ListEmbeddings()
        {
            try
            {
                var images = MongoDBService.GetList<EmbeddingsImage>("Images");

                return Ok(images);
            }
            catch (WebException ex)
            {
                return NotFound($"File not found on FTP server. {ex.Message}");
            }
        }


        [HttpGet("View/{fileName}")]
        [Authorize]
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
        [Authorize]
        public async Task<IActionResult> Get(string id)
        {
            try
            {
                var image = MongoDBService.GetCollection<ImageObject>("Images").Find(x => x.id == id).FirstOrDefault();
                
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
        [Authorize(Roles = "Admin" )]
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


            var matchingEmbbedings = MongoDBService.GetCollection<EmbeddingsImage>("Images").Find(x => ImageObject.CosineSimilarity(x.embedding, embedding) > 0.8).ToList().OrderByDescending(x => ImageObject.CosineSimilarity(x.embedding, embedding));

            var person = matchingEmbbedings.FirstOrDefault(x => !string.IsNullOrEmpty(x.person))?.person;

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
                MongoDBService.Upload(imageObject, "Images");

                await _ftpService.CreateFileAsync(file, $"{id}{extension}");

                return Ok(new { file.FileName, imageObject.url, message = "Uploaded to FTP successfully" });
            }
            catch (Exception ex)
            {
                MongoDBService.Remove(imageObject, "Images");
              
                return StatusCode(500, $"FTP upload failed: {ex.Message}");
            }
            finally
            {
                GC.Collect();
            }
            

        }



  

        [HttpDelete("{id}")]
        [Authorize]
        public async Task<IActionResult> DeleteFile(string id)
        {
            try
            {
                var imageObject = MongoDBService.GetList<ImageObject>("Images").FirstOrDefault(x => x.id == id);
                
                if (imageObject != null)
                {
                    MongoDBService.Remove(imageObject, "Images");
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
                    MongoDBService.Upload(imageObject, "Images");
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
