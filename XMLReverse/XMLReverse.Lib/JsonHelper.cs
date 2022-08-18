using System.IO;
using System.Text;
using Newtonsoft.Json;

namespace XMLReverse.Lib
{
    public static class JsonHelper
    {
        public static void WriteToFile(string path, object obj)
        {
            var cfg = new JsonSerializerSettings
            {
                Formatting = Formatting.Indented
            };
            var json = JsonConvert.SerializeObject(obj, cfg);
            File.WriteAllText($"{path}.json", json, Encoding.UTF8);
        }
    }
}