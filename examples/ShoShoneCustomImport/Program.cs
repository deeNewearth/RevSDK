using System;

namespace ShoShoneCustomImport
{
    class Program
	{
		static int Main(string[] args)
		{
			try
			{
				var klick = new DataReader(
					revConfig: new revCore.Config
					{
						jwt = @"eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiJpLnBlcmN5IiwianRpIjoiZWVkZmZhZmEtN2MwNS00OTI1LWFiMmQtYzlhYzUxMmRlZjgzIiwiUmV2QXBwQWNjZXNzIjoiQWxsIiwiRXh0ZXJuYWxQcm9ncmFtIjoiMSIsImV4cCI6MTU3NTI4ODk1OSwiaXNzIjoicmV2MiIsImF1ZCI6InJldjIifQ.ucvvF3L4-fYD01Aspqk5zY4UA12E5FaBKkS4qa_xTMg",
						revUrl = "http://localhost:6021",
						workspace = "shoshone"
					},
					
					imageList: @"C:\tmp\transfer\shoshone\archivefileimportStatus.txt"
					);

				klick.RunAsync().Wait();

				Console.WriteLine("All done. Any key to exit");
				Console.ReadKey();

				return 0;

			}
			catch (Exception ex)
			{
				Console.WriteLine($"Failed with exception :{ex}");

				Console.Error.WriteLine($"Exiting with error at {DateTime.Now.ToString("MM/dd HH:mm:ss")} Any key to exit");

				Console.ReadKey();

				return -1;
			}
		}
	}
}
