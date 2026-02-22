using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Soenneker.Quark.Gen.Lucide.Demo;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

BuildTimeServices.Configure(builder.Services, builder.HostEnvironment.BaseAddress);

await builder.Build().RunAsync();
