using System.Xml.Serialization;

namespace PlatformRacing3.Web.Responses.Procedures;

public class DataAccessSaveCampaignRun3Response : DataAccessDataResponse<DataAccessSaveCampaignRun3Response.Row>
{
	public DataAccessSaveCampaignRun3Response()
	{
		this.Rows = new List<Row>();
	}
	
	public DataAccessSaveCampaignRun3Response(ulong exp)
	{
		this.Rows = new List<Row> { new Row { Exp = exp } };
	}
	
	public DataAccessSaveCampaignRun3Response(ulong exp, uint rank)
	{
		this.Rows = new List<Row> { new Row { Exp = exp, Rank = rank } };
	}
	
	public class Row
	{
		[XmlElement("xp_earned")]
		public ulong Exp { get; set; }
		
		[XmlElement("rank")]
		public uint Rank { get; set; }
	}
}