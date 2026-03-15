using System.Collections.Concurrent;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.FileProviders.Physical;
using Microsoft.Extensions.Primitives;
using PlatformRacing3.Common.Server;
using PlatformRacing3.Common.User;
using PlatformRacing3.Web.Config;
using PlatformRacing3.Web.Controllers.DataAccess2.Procedures;
using PlatformRacing3.Web.Controllers.DataAccess2.Procedures.Exceptions;
using PlatformRacing3.Web.Controllers.DataAccess2.Procedures.Stamps;
using PlatformRacing3.Web.Extensions;
using PlatformRacing3.Web.Responses;
using PlatformRacing3.Web.Utils;

namespace PlatformRacing3.Web.Controllers.DataAccess2;

[ApiController]
[Route("dataaccess2")]
[Produces("text/xml")]
public class DataAccess2 : ControllerBase
{
	private static byte[] DEFAULT_KEY;

	private static readonly IReadOnlyDictionary<string, ConcurrentDictionary<byte[], byte>> KEYS = new Dictionary<string, ConcurrentDictionary<byte[], byte>>()
	{
		{
			"Android",
			new ConcurrentDictionary<byte[], byte>()
		},
		{
			"iOS",
			new ConcurrentDictionary<byte[], byte>()
		},
		{
			"Browser",
			new ConcurrentDictionary<byte[], byte>()
		},
		{
			"Standalone",
			new ConcurrentDictionary<byte[], byte>()
		}
	};

	internal static void Init(WebConfig webConfig)
	{
		// Initialize the default encryption key from config
		if (!string.IsNullOrEmpty(webConfig.EncryptionKey))
		{
			DEFAULT_KEY = Encoding.UTF8.GetBytes(webConfig.EncryptionKey);
			Console.WriteLine($"Initialized encryption key from config: {webConfig.EncryptionKey}");
		}
		else
		{
			DEFAULT_KEY = Encoding.UTF8.GetBytes("012345318010A4CD");
			Console.WriteLine("Using default encryption key (not from config)");
		}
		
		// Add default key to all platforms
		byte[] defaultKeyBytes = DEFAULT_KEY;
		KEYS["Android"].TryAdd(defaultKeyBytes, 0);
		KEYS["Browser"].TryAdd(defaultKeyBytes, 0);
		KEYS["iOS"].TryAdd(defaultKeyBytes, 0);
		KEYS["Standalone"].TryAdd(defaultKeyBytes, 0);
		Console.WriteLine("Added default encryption key to all platforms");

		if (string.IsNullOrWhiteSpace(webConfig.GamePath))
		{
			Console.WriteLine("Game path is not set in config, skipping SWF loading");
			return;
		}

		try
		{
			Console.WriteLine($"Initializing game files from path: {webConfig.GamePath}");
			
			PhysicalFileProvider val = new(webConfig.GamePath, ExclusionFilters.Sensitive)
			{
				UsePollingFileWatcher = true,
				UseActivePolling = true
			};
				
			RefreshSwfs();
			RegisterWatch();

			void RefreshSwfs()
			{
				Console.WriteLine("Refreshing SWFs");
				
				DirectoryInfo dir = new DirectoryInfo(webConfig.GamePath);
				if (!dir.Exists)
				{
					Console.WriteLine($"WARNING: Game directory does not exist: {webConfig.GamePath}");
					return;
				}
				
				var files = dir.GetFiles("*.swf");
				Console.WriteLine($"Found {files.Length} SWF files");

				Parallel.ForEach(files.OrderBy(f => f.LastWriteTime), async swf =>
				{
					try
					{
						Console.WriteLine($"Processing SWF file: {swf.Name}");
						byte[] bytes = await System.IO.File.ReadAllBytesAsync(swf.FullName);
						DataAccess2.CalcHashAndAdd(bytes);
					}
					catch (Exception ex)
					{
						Console.WriteLine($"Error processing SWF file {swf.Name}: {ex.Message}");
					}
				});
				
				// Output all encryption keys for debugging
				Console.WriteLine("Generated encryption keys:");
				foreach (var platformKeys in KEYS)
				{
					Console.WriteLine($"Platform: {platformKeys.Key}, Key count: {platformKeys.Value.Count}");
					foreach (var key in platformKeys.Value.Keys)
					{
						Console.WriteLine($"  Key: {Encoding.UTF8.GetString(key)}");
					}
				}
			}

			void RegisterWatch()
			{
				IChangeToken token = val.Watch("*.swf");
				token.RegisterChangeCallback(state =>
				{
					RefreshSwfs();
					RegisterWatch();
				}, null);
			}
		}
		catch (Exception ex)
		{
			Console.WriteLine($"Error initializing game files: {ex.Message}");
		}
	}

	private static void CalcHashAndAdd(byte[] bytes)
	{
		try
		{
			int crc = DataAccess2.GetCrc32(bytes.AsSpan(3));
			string s = DataAccess2.GetHash(crc);

			byte[] bytes2 = Encoding.UTF8.GetBytes(s);
			if (bytes2.Length == 16)
			{
				Console.WriteLine($"Generated key from SWF: {s}");
				DataAccess2.KEYS["Android"].TryAdd(bytes2, 0);
				DataAccess2.KEYS["Browser"].TryAdd(bytes2, 0);
				DataAccess2.KEYS["Standalone"].TryAdd(bytes2, 0);
			}
			else
			{
				Console.WriteLine($"Key has wrong length, expected 16 but got {bytes2.Length}: {s}");
			}
		}
		catch (Exception ex)
		{
			Console.WriteLine($"Error calculating hash: {ex.Message}");
		}
	}

	private static int GetCrc32(ReadOnlySpan<byte> span)
	{
		using (MemoryStream memoryStream = new(span[5..].ToArray()))
		{
			using (ZLibStream val = new(memoryStream, CompressionMode.Decompress))
			{
				using (MemoryStream memoryStream2 = new())
				{
					memoryStream2.Write(Encoding.UTF8.GetBytes("FWS"));
					memoryStream2.Write(span[..5]);

					val.CopyTo(memoryStream2);

					return DataAccess2.CalculateCrc32(memoryStream2.ToArray());
				}
			}
		}
	}

	private static int CalculateCrc32(byte[] bytes)
	{
		int[] array = DataAccess2.Crc32Table();

		uint num = uint.MaxValue;
		for (int i = 0; i < bytes.Length; i++)
		{
			num = (uint) (array[(num ^ bytes[i]) & 0xFF] ^ (int) (num >> 8));
		}

		return (int) (~num);
	}

	private static int[] Crc32Table()
	{
		int[] array = new int[256];
		for (uint num = 0u; num < 256; num++)
		{
			uint num2 = num;
			for (int i = 0; i< 8; i++)
			{
				num2 = (uint) (((num2 & 1) != 0) ? (-306674912 ^ (int) (num2 >> 1)) : ((int)(num2 >> 1)));
			}

			array[num] = (int) num2;
		}

		return array;
	}

	private static string GetHash(int crc32)
	{
		using (MD5 mD = MD5.Create())
		{
			byte[] array = mD.ComputeHash(Encoding.UTF8.GetBytes($"L{crc32}L"));

			StringBuilder stringBuilder = new();
			for (int i = 0; i < array.Length; i++)
			{
				stringBuilder.Append(array[i].ToString("x2"));
			}

			return $"{IntOrZero(stringBuilder[9])}12345{IntOrZero(stringBuilder[18]) + 56 - 32}8{IntOrZero(stringBuilder[31]) + 11 - 11}10A{stringBuilder[16]}CD";
		}

		static int IntOrZero(char c) => char.IsNumber(c) ? int.Parse(c.ToString()) : 0;
	}

	private readonly ILogger<DataAccess2> logger;

	private readonly IReadOnlyDictionary<string, IProcedure> Procedures;

	public DataAccess2(ServerManager serverManager, ILogger<DataAccess2> logger)
	{
		this.logger = logger;

		this.Procedures = new Dictionary<string, IProcedure>()
		{
			{ "GetServers2", new GetServers2Procedure(serverManager) },
			{ "GetLoginToken2", new GetLoginToken2Procedure() },
			{ "SaveLevel4",  new SaveLevel4Procedure() },
			{ "CountMyLevels2", new CountMyLevels2Procedure() },
			{ "GetMyLevels2", new GetMyLevels2Procedure() },
			{ "GetLevel2", new GetLevel2Procedure() },
			{ "DeleteLevel2", new DeleteLevel2Procedure() },
			{ "SaveBlock4", new SaveBlock4Procedure() },
			{ "GetMyBlockCategorys", new GetMyBlockCategorysProcedure() },
			{ "CountMyBlocks2", new CountMyBlocks2Procedure() },
			{ "GetMyBlocks2", new GetMyBlocks2Procedure() },
			{ "GetBlock2", new GetBlock2Procedure() },
			{ "GetManyBlocks", new GetManyBlocksProcedure() },
			{ "DeleteBlock2", new DeleteBlock2Procedure() },
			{ "CountMyFriends", new CountMyFriendsProcedure() },
			{ "GetMyFriends", new GetMyFriendsProcedure() },
			{ "CountMyIgnored", new CountMyIgnoredProcedure() },
			{ "GetMyIgnored", new GetMyIgnoredProcedure() },
			{ "SearchUsers2", new SearchUsers2Procedure() },
			{ "SearchLevels3", new SearchLevels3Procedure() },
			{ "GetLockedLevel", new GetLockedLevelProcedure() },
			{ "SaveCampaignRun3", new SaveCampaignRun3Procedure() },
			{ "GetMyFriendsFastestRuns", new GetMyFriendsFastestRunsProcedure() },
			{ "GetCampaignRun3", new GetCampaignRun3Procedure() },
			{ "GetMyStampCategorys", new GetMyStampCategoriesProcedure() },
			{ "CountMyStamps", new CountMyStampsProcedure() },
			{ "GetMyStamps", new GetMyStampsProcedure() },
			{ "SaveStamp", new SaveStampProcedure() },
			{ "GetManyStamps", new GetManyStampsProcedure() },
			{ "DeleteStamp", new DeleteStampProcedure() },
			{ "GetUserLevelData", new GetUserLevelDataProcedure() },
			{ "SaveUserLevelData", new SaveUserLevelDataProcedure() },
			{ "PurchaseItem", new PurchaseItemProcedure() },
			{ "GetPurchasedItems", new GetPurchasedItemsProcedure() }
		};
	}

	[HttpPost]
	public async Task<object> DataAccessAsync([FromQuery] uint id, [FromForm] uint dataRequestId, [FromForm(Name = "gameId")] string gameIdEncoded, [FromForm(Name = "storedProcID")] byte[] storedProcId, [FromForm(Name = "storedProcedureName")] string storedProcedureNameEncoded, [FromForm(Name = "parametersXML")] string parametersXmlEncoded, [FromForm(Name = "platform")] string platform, [FromForm(Name = "playerType")] string playerType)
	{
		if (id != dataRequestId || gameIdEncoded is null || storedProcId is null || storedProcedureNameEncoded is null || parametersXmlEncoded is null)
		{
			return this.BadRequest();
		}
            
		byte[] key = await this.FindEncryptionKeyAsync(storedProcId, gameIdEncoded, platform, playerType);
		if (key != null)
		{
			try 
			{
				string storedProcedureName = this.DecryptDataAsString(storedProcedureNameEncoded, storedProcId, key);
				Console.WriteLine($"Procedure requested: {storedProcedureName}");
				
				if (this.Procedures.TryGetValue(storedProcedureName, out IProcedure procedure))
				{
					XDocument xml = this.DecryptDataAsXml(parametersXmlEncoded, storedProcId, key);

					try
					{
						IDataAccessDataResponse response = await procedure.GetResponseAsync(this.HttpContext, xml);
						response.DataRequestId = dataRequestId;
						return response;
					}
					catch (DataAccessProcedureMissingData)
					{
						return new DataAccessErrorResponse(dataRequestId, "Invalid request, procedure was missing required data");
					}
					catch (Exception ex)
					{
						this.logger.LogError(EventIds.DataAccess2Failed, ex, "Failed to execute procedure");

						return new DataAccessErrorResponse(dataRequestId, "Critical error while executing procedure");
					}
				}
				else
				{
					return new DataAccessErrorResponse(dataRequestId, "No procedure found by the name");
				}
			}
			catch (Exception ex)
			{
				this.logger.LogError(EventIds.DataAccess2Failed, ex, "Failed to decrypt data");

				return new DataAccessErrorResponse(dataRequestId, "Critical error while decrypting data");
			}
		}
		else
		{
			return new DataAccessEmptyResponse(dataRequestId);
		}
	}

	private async Task<byte[]> FindEncryptionKeyAsync(byte[] storedProcId, string gameIdEncoded, string platform, string playerType)
	{
		try
		{
			Console.WriteLine($"Attempting to find encryption key for platform: {platform}, player type: {playerType}");
			
			if (string.IsNullOrWhiteSpace(platform))
			{
				platform = "Browser";
			}

			if (DataAccess2.KEYS.ContainsKey(platform))
			{
				// If we have keys for this platform, try finding them all
				foreach (byte[] key in DataAccess2.KEYS[platform].Keys)
				{
					try
					{
						string gameId = this.DecryptDataAsString(gameIdEncoded, storedProcId, key);
						if (!string.IsNullOrWhiteSpace(gameId) && gameId.StartsWith("PlatformRacing3"))
						{
							Console.WriteLine($"Found valid key: {Encoding.UTF8.GetString(key)}");
							return key;
						}
					}
					catch
					{
						// Ignore
					}
				}
			}

			// Fallback to DEFAULT_KEY
			Console.WriteLine($"No platform key found, falling back to default key: {Encoding.UTF8.GetString(DEFAULT_KEY)}");
			return DataAccess2.DEFAULT_KEY;
		}
		catch (Exception ex)
		{
			Console.WriteLine($"Error finding encryption key: {ex.Message}");
			return DataAccess2.DEFAULT_KEY;
		}
	}

	private MemoryStream DecryptDataAsStream(string data, byte[] iv, byte[] key)
	{
		Span<byte> bytes = stackalloc byte[0];
		if (data.Length <= 1024)
		{
			bytes = stackalloc byte[768];

			Convert.TryFromBase64String(data, bytes, out int bytesWritten);

			bytes = bytes[..bytesWritten];
		}
		else
		{
			bytes = Convert.FromBase64String(data);
		}
            
		using (Aes crypt = Aes.Create())
		{
			crypt.Mode = CipherMode.CBC;
			crypt.KeySize = 128;
			crypt.Padding = PaddingMode.Zeros;

			crypt.IV = iv;
			crypt.Key = key;

			MemoryStream memoryStream = new();

			using (Stream transcoding = Encoding.CreateTranscodingStream(memoryStream, Encoding.Unicode, Encoding.UTF8, leaveOpen: true))
			{
				using (ICryptoTransform decryptor = crypt.CreateDecryptor())
				{
					using (CryptoStream stream = new(transcoding, decryptor, CryptoStreamMode.Write))
					{
						stream.Write(bytes);
					}
				}
			}
                
			return memoryStream;
		}
	}

	private string DecryptDataAsString(string data, byte[] iv, byte[] key)
	{
		using (MemoryStream stream = this.DecryptDataAsStream(data, iv, key))
		{
			byte[] buffer = stream.GetBuffer();

			Span<byte> bytes = buffer.AsSpan(..(int)stream.Length);
			Span<char> chars = MemoryMarshal.Cast<byte, char>(bytes).TrimEnd('\0'); //Trim to remove extra null bytes
                
			return new string(chars);
		}
	}

	private XDocument DecryptDataAsXml(string data, byte[] iv, byte[] key)
	{
		using (MemoryStream stream = this.DecryptDataAsStream(data, iv, key))
		{
			byte[] buffer = stream.GetBuffer();
                
			Span<byte> bytes = buffer.AsSpan(..(int)stream.Length);
			Span<char> chars = MemoryMarshal.Cast<byte, char>(bytes).TrimEnd('\0'); //Trim to remove extra null bytes
                
			using (MemoryStream trimmedStream = new(buffer, 0, chars.Length * sizeof(char)))
			{
				return XDocument.Load(trimmedStream);
			}
		}
	}
}