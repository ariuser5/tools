using System.Net.Http.Headers;

using var client = new HttpClient();
client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("DotNetApp", "1.0"));

Console.Write("Enter GitHub username: ");
var username = Console.ReadLine();
var url = $"https://api.github.com/users/{username}";
var response = await client.GetAsync(url);
if (response.IsSuccessStatusCode)
{
    var content = await response.Content.ReadAsStringAsync();
    Console.WriteLine($"GitHub User Profile for {username}:");
    Console.WriteLine(content);
}
else
{
    Console.WriteLine($"Error: {response.StatusCode}");
    var error = await response.Content.ReadAsStringAsync();
    Console.WriteLine(error);
}
