using PlatformRacing3.Common.Campaign;
using PlatformRacing3.Common.Redis;
using PlatformRacing3.Common.User;
using PlatformRacing3.Common.Utils;
using PlatformRacing3.Web.Controllers.DataAccess2.Procedures.Exceptions;
using PlatformRacing3.Web.Extensions;
using PlatformRacing3.Web.Responses;
using PlatformRacing3.Web.Responses.Procedures;
using StackExchange.Redis;
using System.Text.Json;
using System.Xml.Linq;

namespace PlatformRacing3.Web.Controllers.DataAccess2.Procedures;

public class SaveCampaignRun3Procedure : IProcedure
{
	private const int GOLD_MEDAL_MULTIPLIER = 9;
	private const int SILVER_MEDAL_MULTIPLIER = 6;
	private const int BRONZE_MEDAL_MULTIPLIER = 3;
	private const int BASE_XP = 5;
	
	// Define medal enum if not available
	private enum Medal
	{
		None = 0,
		Bronze = 1,
		Silver = 2,
		Gold = 3
	}

	public async Task<IDataAccessDataResponse> GetResponseAsync(HttpContext httpContext, XDocument xml)
	{
		uint userId = httpContext.IsAuthenicatedPr3User();
		if (userId > 0)
		{
			XElement data = xml.Element("Params");
			if (data != null)
			{
				string category = (string)data.Element("p_category") ?? "normal";
				uint levelId = (uint?)data.Element("p_level_id") ?? throw new DataAccessProcedureMissingData();
				uint levelVersion = (uint?)data.Element("p_level_version") ?? throw new DataAccessProcedureMissingData();
				string recordRun = (string)data.Element("p_recorded_run") ?? throw new DataAccessProcedureMissingData();
				int finishTime = (int?)data.Element("p_finish_time") ?? throw new DataAccessProcedureMissingData();

				CampaignRun campaignRun = CampaignRun.FromCompressed(recordRun);

				if (campaignRun != null)
				{
					PlayerUserData playerUserData = await UserManager.TryGetUserDataByIdAsync(userId);
					if (campaignRun.Username != playerUserData.Username)
					{
						return new DataAccessErrorResponse("Invalid username");
					}

					// Save the campaign run data
					await CampaignManager.SaveCampaignRunAsync(userId, category, levelId, levelVersion, recordRun, finishTime);

					// Determine the medal based on finish time
					Medal medal = DetermineMedal(finishTime);
					
					// Calculate XP based on medal
					int medalMultiplier = medal switch
					{
						Medal.Gold => GOLD_MEDAL_MULTIPLIER,
						Medal.Silver => SILVER_MEDAL_MULTIPLIER,
						Medal.Bronze => BRONZE_MEDAL_MULTIPLIER,
						_ => 1
					};
					
					ulong xpGain = (ulong)(BASE_XP * medalMultiplier);
					
					// Add the XP to the user's account
					playerUserData.AddExp(xpGain);
					
					// Get updated values after adding XP
					var (newRank, newExp) = ExpUtils.GetRankAndExpFromTotalExp(playerUserData.TotalExp);
					
					// Notify the game server about the XP update
					await NotifyUserVarsUpdateAsync(userId, newRank, newExp, xpGain, medal);
					
					// Send the updated XP AND rank to the client
					return new DataAccessSaveCampaignRun3Response(newExp, newRank);
				}
				else
				{
					throw new DataAccessProcedureMissingData();
				}
			}
			else
			{
				throw new DataAccessProcedureMissingData();
			}
		}
		else
		{
			return new DataAccessErrorResponse("You are not logged in!");
		}
	}
	
	private Medal DetermineMedal(int finishTime)
	{
		// Simple medal determination based on time
		// This would normally use level data from the database
		if (finishTime < 40000) // Less than 40 seconds
		{
			return Medal.Gold;
		}
		else if (finishTime < 60000) // Less than 60 seconds
		{
			return Medal.Silver;
		}
		else
		{
			return Medal.Bronze;
		}
	}
	
	private async Task NotifyUserVarsUpdateAsync(uint userId, uint newRank, ulong newExp, ulong xpGain, Medal medal)
	{
		try
		{
			string medalName = medal.ToString();
			ulong baseXpAmount = BASE_XP;
			ulong bonusXpAmount = xpGain - baseXpAmount;
			
			// Prepare the data to send to the game server (XP only, no campaign data)
			var userVarsData = new Dictionary<string, object>
			{
				["UserId"] = userId,
				["Rank"] = newRank,
				["Exp"] = newExp,
				["TotalExpGain"] = xpGain,
				["ExpArray"] = new object[]
				{
					new object[] { "Base XP", baseXpAmount },
					new object[] { $"{medalName} medal bonus", bonusXpAmount }
				}
			};
			
			// Campaign data is no longer included in the Redis message
			// It will be queried directly when needed instead
			
			// Convert to JSON
			string jsonData = JsonSerializer.Serialize(userVarsData);
			
			// Publish the message to Redis for the game servers to pick up
			await RedisConnection.GetDatabase().PublishAsync(RedisChannel.Literal("UserVarsUpdate"), jsonData, CommandFlags.FireAndForget);
		}
		catch (Exception ex)
		{
			// Log the error but don't fail the request
			Console.WriteLine($"Error notifying about user vars update: {ex.Message}");
		}
	}
}