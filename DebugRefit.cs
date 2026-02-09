using System;
using System.Net.Http;
using System.Threading.Tasks;
using Refit;
using BookStore.Client;
using System.Collections.Generic;

namespace DebugRefit
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var httpClient = new HttpClient { BaseAddress = new Uri("http://localhost:5000") };
            var client = RestService.For<IAuthorsClient>(httpClient);

            var id = Guid.NewGuid();
            var etag = "\"1\"";

            try 
            {
                await client.UpdateAuthorAsync(id, new UpdateAuthorRequest(), etag);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }
    }
}
