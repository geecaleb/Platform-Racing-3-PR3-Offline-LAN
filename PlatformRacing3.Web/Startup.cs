using Microsoft.AspNetCore.Authentication.Cookies;
using PlatformRacing3.Common.Campaign;
using PlatformRacing3.Common.Server;
using PlatformRacing3.Common.Utils;
using PlatformRacing3.Web.Middleware;

namespace PlatformRacing3.Web;

internal class Startup
{
	public IConfiguration Configuration { get; }

	public Startup(IConfiguration configuration)
	{
		this.Configuration = configuration;
	}
        
	public void ConfigureServices(IServiceCollection services)
	{
		services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(options =>
		{
			options.ExpireTimeSpan = TimeSpan.FromDays(30);
			options.SlidingExpiration = true;

			options.Cookie.Name = "PR3-Auth";
			options.Cookie.HttpOnly = true;
			options.Cookie.SameSite = SameSiteMode.None; //None due to Chrome 71 breaking changes
			options.Cookie.SecurePolicy = CookieSecurePolicy.None;
		});

		services.AddControllers()
			.AddXmlSerializerFormatters()  // Keep XML formatters
			.AddXmlDataContractSerializerFormatters();

		services.AddCors();

		services.AddSingleton<ServerManager>();
		services.AddSingleton<CampaignManager>();
	}
        
	public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
	{
		//Setup global stuff
		if (env.IsDevelopment())
		{
			app.UseDeveloperExceptionPage();
		}

		LoggerUtil.LoggerFactory = app.ApplicationServices.GetRequiredService<ILoggerFactory>();

		//TODO: Eh...
		app.ApplicationServices.GetRequiredService<ServerManager>().LoadServersAsync().Wait();
		app.ApplicationServices.GetRequiredService<CampaignManager>().LoadCampaignTimesAsync().Wait();
		app.ApplicationServices.GetRequiredService<CampaignManager>().LoadPrizesAsync().Wait();

		app.UseMiddleware<CloudflareMiddleware>();
		app.UseStaticFiles();
		app.UseRouting();
		app.UseCookiePolicy();
		app.UseAuthentication();
		app.UseCors(options =>
		{
			options.WithOrigins("http://pr3hub.com", "https://pr3hub.com", "http://127.0.0.1:8000", "http://localhost:8000")
				.AllowAnyMethod()
				.AllowAnyHeader()
				.AllowCredentials();
		});

		app.UseEndpoints(endpoints =>
		{
			endpoints.MapControllers();
		});
	}
}
