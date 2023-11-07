using Newtonsoft.Json;
using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace ISP.Problem
{
    public class CompanyService : IRegisterService
    {
        public static HttpClient client = new HttpClient();

        public object Get(int Id)
        {
            return new
            {
                Id = Id,
                Name = "COMPANY NAME",
                TradeName = "COMPANY TRADENAME",
                IE = "123456789",
                TaxId = "10651/0001-001",
                Email = "test@test.com"
            };
        }

        public async Task<object> SearchTaxId(string taxId)
        {
            string token = "user";

            string Url = String.Format("http://www.endpoint.com", token, taxId);

            var result = await client.GetAsync(Url);

            var content = await result.Content.ReadAsStringAsync();

            return JsonConvert.DeserializeObject(content);
        }
    }
}
