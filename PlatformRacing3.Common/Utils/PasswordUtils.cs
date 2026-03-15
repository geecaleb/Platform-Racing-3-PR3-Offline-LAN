using System.Security.Cryptography;
using System.Text;
using BCrypt.Net;

namespace PlatformRacing3.Common.Utils;

internal static class PasswordUtils
{
	internal static string HashPassword(string password)
	{
		// Use BCrypt with work factor of 12 (good balance between security and performance)
		return BCrypt.Net.BCrypt.HashPassword(password, workFactor: 12);
	}

	internal static bool VerifyPassword(string password, string hash)
	{
		try
		{
			// First try BCrypt verification
			if (BCrypt.Net.BCrypt.Verify(password, hash))
			{
				return true;
			}

			// If BCrypt verification fails, try legacy verification
			return VerifyPasswordLegacy(password, hash);
		}
		catch
		{
			return false;
		}
	}

	[Obsolete("This is legacy code")]
	internal static bool VerifyPasswordLegacy(string password, string hash)
	{
		//TEST PHP BCRYPT
		try
		{
			if (BCrypt.Net.BCrypt.Verify(password, hash))
			{
				return true;
			}
		}
		catch
		{

		}

		byte[] bytes = Encoding.UTF8.GetBytes(password);

		//MD5 (Don't worry, this was only for few months, was accidentally left after localhost testing)
		using (MD5 md5 = MD5.Create())
		{
			byte[] md5Hash = md5.ComputeHash(bytes);

			StringBuilder md5String = new();
			foreach(byte byte_ in md5Hash)
			{
				md5String.Append(byte_.ToString("x2"));
			}

			if (md5String.ToString() == hash)
			{
				return true;
			}
		}

		return false;
	}

	private static bool VerifyPasswordVersion0(string password, byte[] bytes)
	{
		// This method is no longer needed as we're using BCrypt
		return false;
	}
}