using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

namespace Redadeg.lmuDataPlugin
{
    internal class LMUPluginUpdateModule
    {



        internal static async  Task<string> UpdateLMUPlugin(string LMU_PluginPath)
        {
            string owner = "tembob64"; // Замените на владельца репозитория
            string repo = "LMU_SharedMemoryMapPlugin"; // Замените на название репозитория
            string releasesUrl = $"https://api.github.com/repos/{owner}/{repo}/releases/latest";

            using (HttpClient client = new HttpClient())
            {
                client.DefaultRequestHeaders.UserAgent.ParseAdd("LMU_Plugin_update_service");

                try
                {
                    // Получаем информацию о релизе
                    var response = await client.GetAsync(releasesUrl);
                    response.EnsureSuccessStatusCode();
                    var jsonString = await response.Content.ReadAsStringAsync();
                    var release = JObject.Parse(jsonString);

                    // Находим URL архива ZIP
                    var zipAsset = release["assets"]
                        .FirstOrDefault(a => a["name"].ToString().EndsWith(".zip"));

                    if (zipAsset == null)
                    {
                       
                        return "ZIP file cannot find.";
                    }

                    string zipUrl = zipAsset["browser_download_url"].ToString();

                    // Скачиваем ZIP-файл
                    string zipFilePath = Path.Combine(Path.GetTempPath(), zipAsset["name"].ToString());
                    using (var zipResponse = await client.GetAsync(zipUrl))
                    {
                        zipResponse.EnsureSuccessStatusCode();
                        using (var fs = new FileStream(zipFilePath, FileMode.Create))
                        {
                            await zipResponse.Content.CopyToAsync(fs);
                        }
                    }

                    // Распаковываем архив
                   // string extractPath = Path.Combine(Directory.GetCurrentDirectory(), "Extracted");
                    Directory.CreateDirectory(LMU_PluginPath);
                    ZipFile.ExtractToDirectory(zipFilePath, LMU_PluginPath);

                    return $"Zip file exctracted to: {LMU_PluginPath}";
                }
                catch (Exception ex)
                {
                    return  $"Error: {ex.Message}";
                }
            }

        }   
    }
}
