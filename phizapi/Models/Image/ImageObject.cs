using Microsoft.ML.OnnxRuntime.Tensors;
using System.Drawing;

namespace phizapi.Models.Image
{

    // ImageObject is the main image object used in the database
    // It contains all the information about an image including its embedding

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
