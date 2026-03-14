using System.Xml.Linq;
using PlatformRacing3.Common.Server;
using PlatformRacing3.Web.Responses;
using PlatformRacing3.Web.Responses.Procedures;

namespace PlatformRacing3.Web.Controllers.DataAccess2.Procedures;

public class GetServers2Procedure : IProcedure
{
	private readonly ServerManager serverManager;

	public GetServers2Procedure(ServerManager serverManager)
	{
		this.serverManager = serverManager;
	}

	public Task<IDataAccessDataResponse> GetResponseAsync(HttpContext httpContext, XDocument xml)
	{
		Console.WriteLine("GetServers2Procedure: Retrieving server list");
		
		var servers = this.serverManager.GetServers().ToList();
		
		Console.WriteLine($"GetServers2Procedure: Retrieved {servers.Count} servers from ServerManager");
		
		// Print all servers for debugging
		Console.WriteLine("BEGIN SERVER LIST FROM SERVERMANAGER");
		foreach (var server in servers)
		{
			Console.WriteLine($"Server in manager: ID={server.Id}, Name={server.Name}, IP={server.IP}, Port={server.Port}, Status={server.Status}");
		}
		Console.WriteLine("END SERVER LIST FROM SERVERMANAGER");
		
		// If no servers are found, add a debug entry
		if (servers.Count == 0)
		{
			Console.WriteLine("WARNING: No servers found in ServerManager! This will prevent login!");
		}
		
		var response = new DataAccessGetServers2Response(servers);
		Console.WriteLine($"GetServers2Procedure: Created response with {servers.Count} servers");
		
		return Task.FromResult<IDataAccessDataResponse>(response);
	}
}