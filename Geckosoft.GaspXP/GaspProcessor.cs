using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using Geckosoft.GaspXP.Properties;
using Geckosoft.GaspXP.Vendor.VsMultipleFileGenerator;

namespace Geckosoft.GaspXP
{
	[Guid("30F3789F-7FF9-4c66-ADA6-A071513E7FDA")]
	public class GaspProcessor : VsMultipleFileGenerator<string>
	{
		public const string GASP_PRE_SUFFIX = ".gasp.";

		public static string[] GaspSuffixes = new[] { GASP_PRE_SUFFIX + "aspx", GASP_PRE_SUFFIX + "ascx", GASP_PRE_SUFFIX + "master" };

        public override IEnumerator<string> GetEnumerator()
        {
			if (ActiveSuffix == null)
				yield break;

        	yield return InputFilePath;
        }

		protected override string GetFileName(string element)
		{
			element = element.Replace(GASP_PRE_SUFFIX, ".");

			return element.Substring(element.LastIndexOf('/') + 1);
        }

        public override byte[] GenerateContent(string filename)
        {
        	return Encoding.GetBytes(GaspXParser.Parse(InputFileContents));
        }

		public static readonly Encoding Encoding = new UTF8Encoding();

		public string ActiveSuffix
		{
			get
			{
				foreach (var suf in GaspSuffixes)
				{
					if (suf.Length >= InputFilePath.Length)
						continue;

					if (InputFilePath.Substring(InputFilePath.Length - suf.Length, suf.Length) == suf)
					{
						return suf;
					}
				}

				return null;
			}
		}
		
        public override byte[] GenerateSummaryContent()
        {
        	var sb = new StringBuilder(Resources.Report);
			sb.Replace("$Version$", Version);
			sb.Replace("$InputFilePath$", InputFilePath);
        	sb.Replace("$DateTime$", DateTime.Now.ToString());

			if (ActiveSuffix == null)
			{
				UnhandledContent(sb);
			}else
			{
				sb.Replace("$Output$", "Preprocessing complete.<br /><br />" + ((LastException == null) ? "" : "Exception:  " +LastException.ToString()));
			}

        	return Encoding.GetBytes(sb.ToString());
        }

		protected virtual string Version
		{
			get { return typeof (GaspProcessor).Assembly.GetName().Version.ToString(); }
		}

		protected virtual void UnhandledContent(StringBuilder sb)
		{
			sb.Replace("$Output$", "Unhandled file extension. Valid extensions: " + string.Join(", ", GaspSuffixes));
		}

		public override string GetDefaultExtension()
        {
            return ".html";
        }
    }
}
