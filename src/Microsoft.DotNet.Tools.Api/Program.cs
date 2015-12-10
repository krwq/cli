using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using System.Threading.Tasks;

namespace Microsoft.DotNet.Tools.Api
{
    public class Program
    {
        public static int Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine("Usage: dotnet api <search string>");
                Console.WriteLine("Example: dotnet api xmlReader");
                Console.WriteLine("Example: dotnet api xmlRe*r");
                return 0;
            }

            UrlEncoder urlEncoder = UrlEncoder.Create(UnicodeRanges.BasicLatin);
            string searchTerm = urlEncoder.Encode(string.Join(" ", args));
            string requestUri = $"http://packagesearch.azurewebsites.net/Search/?searchTerm={searchTerm}";
            // Verbose Con

            HttpWebRequest request;
            WebResponse response;

            try
            {
                request = WebRequest.CreateHttp(requestUri);
                response = request.GetResponseAsync().Result;
            }
            catch (Exception e)
            {
                Console.WriteLine("Could not connect with the server.");
                Console.WriteLine(e);
                return 1;
            }

            if (request.HaveResponse)
            {
                using (var respStream = response.GetResponseStream())
                {
                    using (var reader = new StreamReader(respStream))
                    {
                        string jsonResponse = reader.ReadToEnd();
                        var results = JsonConvert.DeserializeObject<List<SearchResult>>(jsonResponse);
                        bool hasResults = false;
                        foreach (var result in results)
                        {
                            if (result.PackageDetails == null)
                            {
                                continue;
                            }
                            hasResults = true;

                            if (result.Signature != null)
                            {
                                Console.Write($"Type: {result.FullTypeName}: {result.Signature}");
                            }
                            else
                            {
                                Console.Write($"Type: {result.FullTypeName}");
                            }

                            Console.WriteLine($" in {result.PackageDetails.Name} {result.PackageDetails.Version}");
                        }

                        if (hasResults)
                        {
                            return 0;
                        }
                    }
                }
            }

            return 1;
        }
    }
}
