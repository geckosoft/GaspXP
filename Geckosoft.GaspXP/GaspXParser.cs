using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Xml;
using Geckosoft.GaspXP.Vendor.HtmlAgilityPack;

namespace Geckosoft.GaspXP
{
	public class GaspXParser : IDisposable
	{
		public readonly Regex AspTagsRegex = new Regex(@"<%(@|=|\$)?([^\0]+?)%>");
		public readonly Regex AspTagsStripRegex = new Regex(@"<%[@|=|\$]?((?!<%).)*%>"); //<%[@|=|\$]?[^>]*%>"); // <%[@|=|\$]?[^\0]+?%>"); // <%[@|=|\$]?[^\0]*%>
		public readonly Regex GaspXPContentRegex = new Regex(@"<!-- GaspXP\[\[(\d+)\]\] -->");
		public readonly Regex GaspXPConditionRegex = new Regex(@"(<gasp:condition\b[^>]*>(.*?)</gasp:condition>)");
		public readonly Regex GaspXPForeachRegex = new Regex(@"(<gasp:foreach\b[^>]*>(.*?)</gasp:foreach>)");
		public readonly Regex AttributesRegex = new Regex(@"(\S+)=[""']?((?:.(?![""']?\s+(?:\S+)=|[>""']))+.)[""']?");
		public Dictionary<int, string> AspContent { get; protected set; }
		public readonly XmlNamespaceManager GaspNamespace = new XmlNamespaceManager(new NameTable());

		public MatchCollection AspTags { get; protected set; }
		public MatchCollection GaspConditions { get; protected set; }
		public MatchCollection GaspForeaches { get; protected set; }

		public string Raw { get; protected set; }
		public string Processed { get; protected set; }

		protected GaspXParser(string data)
		{
			Raw = data;
			GaspNamespace.AddNamespace("gasp", "urn:gaspxp.codeplex.com");
		}

		private int contentHolderId;

		public void Parse()
		{
			contentHolderId = -1;
			AspContent = new Dictionary<int, string>();

			// Extract tags
			AspTags = AspTagsStripRegex.Matches(Raw);
			GaspConditions = GaspXPConditionRegex.Matches(Raw);
			GaspForeaches = GaspXPForeachRegex.Matches(Raw);

			// Preprocess the HTML

			// Strip the <% asp code %> (replace with a placeholder)
			Processed = AspTagsStripRegex.Replace(Raw,
									  me =>
									  {
										  AspContent.Add(++contentHolderId, me.Value);
										  return "<!-- GaspXP[[" + contentHolderId + "]] -->";
									  });

			// Strip the <condition></condition> tags
			Processed = GaspXPConditionRegex.Replace(Processed,
									  me => "");

			// Strip the <foreach></foreach> tags
			Processed = GaspXPForeachRegex.Replace(Processed,
									  me => "");

			var doc = new HtmlDocument();
			doc.OptionWriteEmptyNodes = true;
			doc.OptionOutputOriginalCase = true;
			doc.OptionAutoCloseOnEnd = true;

			// todo OptionOutputOriginalCase => doesnt seem to work for attributes! (not all?)
			doc.LoadHtml(Processed);
			string debug = "";

			// Loop through all conditions
			foreach (Match condition in GaspConditions)
			{
				foreach (Match tag in AttributesRegex.Matches(condition.Groups[0].Value))
				{
					if (tag.Groups[1].Value == "for")
					{
						var elementId = tag.Groups[2].Value;

						// find the element
						foreach (var n in doc.DocumentNode.SelectNodes("//*", GaspNamespace))
						{
							bool found = false;
							foreach (var a in n.Attributes)
							{
								if (a.OriginalName != "gasp:id" || a.Value != elementId)
									continue;

								found = true;
								break;
							}
							if (!found)
								continue;

							n.ParentNode.InsertBefore(HtmlNode.CreateNode("<% if(" + condition.Groups[2].Value + "){%>"), n);
							n.ParentNode.InsertAfter(HtmlNode.CreateNode("<% } %>"), n);
						}

						foreach (var n in doc.DocumentNode.SelectNodes("id('" + elementId + "')"))
						{
							n.ParentNode.InsertBefore(HtmlNode.CreateNode("<% if(" + condition.Groups[2].Value + "){%>"), n);
							n.ParentNode.InsertAfter(HtmlNode.CreateNode("<% } %>"), n);
						}
					}
				}
			}

			// Loop through all foreaches
			foreach (Match condition in GaspForeaches)
			{
				foreach (Match tag in AttributesRegex.Matches(condition.Groups[0].Value))
				{
					if (tag.Groups[1].Value == "for")
					{
						var elementId = tag.Groups[2].Value;
						string key = "item";

						foreach (Match keyTag in AttributesRegex.Matches(condition.Groups[0].Value))
						{
							if (keyTag.Groups[1].Value == "key")
							{
								key = keyTag.Groups[2].Value;
								break;
							}
						}

						// find the element (first search on 'gaspid')
						// allows to be applied to multiple elements at once!
						foreach (var n in doc.DocumentNode.SelectNodes("//*", GaspNamespace))
						{
							bool found = false;
							foreach (var a in n.Attributes)
							{
								if (a.OriginalName != "gasp:id" || a.Value != elementId)
									continue;

								found = true;
								break;
							}
							if (!found)
								continue;

							n.ParentNode.InsertBefore(HtmlNode.CreateNode("<% if(" + condition.Groups[2].Value + "){%>"), n);
							n.ParentNode.InsertAfter(HtmlNode.CreateNode("<% } %>"), n);
						}

						foreach (var n in doc.DocumentNode.SelectNodes("id('" + elementId + "')"))
						{
							n.InsertBefore(HtmlNode.CreateNode("<% foreach( var " + key + " in (" + condition.Groups[2].Value + ")){%>"), n.FirstChild);
							n.InsertAfter(HtmlNode.CreateNode("<% } %>"), n.LastChild);
							break;
						}
						break;
					}
				}
			}

			// cleanup gaspid's
			foreach (var n in new List<HtmlNode>(doc.DocumentNode.SelectNodes("//*", GaspNamespace)))
			{
				n.Attributes.Remove("gasp:id");
			}
			
			/* return the asp code back into the doc */
			Processed = GaspXPContentRegex.Replace(doc.DocumentNode.OuterHtml,m => AspContent[int.Parse(m.Groups[1].Value)]);
		}

		public static string Parse(string raw)
		{
			try
			{
				using (var parser = new GaspXParser(raw))
				{
					parser.Parse();
					return parser.Processed;
				}
			}
			catch(Exception ex)
			{
				throw new Exception("Error while preprocessing.", ex);
			}
		}

		#region Implementation of IDisposable

		public void Dispose()
		{
			Raw = null;
			Processed = null;
		}

		#endregion
	}
}
