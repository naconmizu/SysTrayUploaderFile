using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace ever
{
    internal class Uploader
    {
        public static async Task UploadFileAsync(string filePath)
        {
            if (!File.Exists(filePath))
            {
                Console.WriteLine("Arquivo não encontrado.");
                return;
            }

            

            HttpClient httpClient = new HttpClient();
            HttpClient client = httpClient;

            var form = new MultipartFormDataContent();

            var fileStream = File.OpenRead(filePath);
            var fileContent = new StreamContent(fileStream);

            fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

            form.Add(fileContent, "file", Path.GetFileName(filePath));

            try
            {
                var response = await client.PostAsync("http://localhost:8080/upload", form);
                var responseText = await response.Content.ReadAsStringAsync();

                Console.WriteLine($"Status: {response.StatusCode}");
                Console.WriteLine($"Response: {responseText}");
            }
            catch
            {
                Console.WriteLine("Erro ao enviar o arquivo. Verifique se o servidor está rodando.");
            }
            finally
            {
                fileStream.Close();
                client.Dispose();
            }
        }
    }
}
