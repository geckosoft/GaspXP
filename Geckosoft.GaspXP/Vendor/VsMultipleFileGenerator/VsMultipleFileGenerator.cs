using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.TextTemplating.VSHost;
using Microsoft.VisualStudio.Shell;
using System.Runtime.InteropServices;
using System.IO;
using System.ComponentModel;

namespace Geckosoft.GaspXP.Vendor.VsMultipleFileGenerator
{
    public abstract class VsMultipleFileGenerator<T> : IEnumerable<T>, IVsSingleFileGenerator, IObjectWithSite
    {
        #region Visual Studio Specific Fields
        private object site;
        private ServiceProvider serviceProvider;
        #endregion

        #region Our Fields
        private string bstrInputFileContents;
        private string wszInputFilePath;
        private readonly EnvDTE.Project project;
        private readonly List<string> newFileNames;
        #endregion

        protected EnvDTE.Project Project
        {
            get
            {
                return project;
            }
        }

        protected string InputFileContents
        {
            get
            {
                return bstrInputFileContents;
            }
        }

        protected string InputFilePath
        {
            get
            {
                return wszInputFilePath;
            }
        }

        private ServiceProvider SiteServiceProvider
        {
            get
            {
                if (serviceProvider == null)
                {
                    var oleServiceProvider = site as Microsoft.VisualStudio.OLE.Interop.IServiceProvider;
                    serviceProvider = new ServiceProvider(oleServiceProvider);
                }
                return serviceProvider;
            }
        }

        public VsMultipleFileGenerator()
        {
            var dte = (EnvDTE.DTE)Package.GetGlobalService(typeof(EnvDTE.DTE));
            var ary = (Array)dte.ActiveSolutionProjects;

            if (ary.Length > 0)
            {
                project = (EnvDTE.Project)ary.GetValue(0);

            }
            newFileNames = new List<string>();
        }

        public abstract IEnumerator<T> GetEnumerator();

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        protected abstract string GetFileName(T element);
        public abstract byte[] GenerateContent(T element);
        public abstract string GetDefaultExtension();


        public abstract byte[] GenerateSummaryContent();
    	protected Exception LastException;

        public void Generate(string wszInputFilePath, string bstrInputFileContents, string wszDefaultNamespace, out IntPtr rgbOutputFileContents, out int pcbOutput, IVsGeneratorProgress pGenerateProgress)
        {
            this.bstrInputFileContents = bstrInputFileContents;
            this.wszInputFilePath = wszInputFilePath;
            newFileNames.Clear();

            int iFound;
            uint itemId;
            EnvDTE.ProjectItem item;
            var pdwPriority = new Microsoft.VisualStudio.Shell.Interop.VSDOCUMENTPRIORITY[1];

            // obtain a reference to the current project as an IVsProject type
            var vsProject = VsHelper.ToVsProject(project);
            // this locates, and returns a handle to our source file, as a ProjectItem
            vsProject.IsDocumentInProject(InputFilePath, out iFound, pdwPriority, out itemId);


            // if our source file was found in the project (which it should have been)
            if (iFound != 0 && itemId != 0)
            {
                Microsoft.VisualStudio.OLE.Interop.IServiceProvider oleSp;
                vsProject.GetItemContext(itemId, out oleSp);
                if (oleSp != null)
                {
                    var sp = new ServiceProvider(oleSp);
                    // convert our handle to a ProjectItem
                    item = sp.GetService(typeof(EnvDTE.ProjectItem)) as EnvDTE.ProjectItem;
                }
                else
                    throw new ApplicationException("Unable to retrieve Visual Studio ProjectItem");
            }
            else
                throw new ApplicationException("Unable to retrieve Visual Studio ProjectItem");

			if (item == null)
				throw new ApplicationException("Unable to retrieve Visual Studio ProjectItem");

			// perform some clean-up, making sure we delete any old (stale) target-files
			foreach (EnvDTE.ProjectItem childItem in item.ProjectItems)
			{
				if (!(childItem.Name.EndsWith(GetDefaultExtension()) || newFileNames.Contains(childItem.Name)))
					// then delete it
					childItem.Delete();
			}

            // now we can start our work, iterate across all the 'elements' in our source file 
			foreach (var element in this)
			{
				// obtain a name for this target file
				string fileName = GetFileName(element);
				// add it to the tracking cache
				newFileNames.Add(fileName);
				// fully qualify the file on the filesystem
				string strFile =
					Path.Combine(wszInputFilePath.Substring(0, wszInputFilePath.LastIndexOf(Path.DirectorySeparatorChar)), fileName);
				// create the file
				FileStream fs = File.Create(strFile);

				try
				{
					// generate our target file content
					byte[] data = GenerateContent(element);

					// write it out to the stream
					fs.Write(data, 0, data.Length);

					fs.Close();

					// add the newly generated file to the solution, as a child of the source file...
					var itm = item.ProjectItems.AddFromFile(strFile);

					/*
					 * Here you may wish to perform some addition logic
					 * such as, setting a custom tool for the target file if it
					 * is intented to perform its own generation process.
					 * Or, set the target file as an 'Embedded Resource' so that
					 * it is embedded into the final Assembly.
                         
					EnvDTE.Property prop = itm.Properties.Item("CustomTool");
					//// set to embedded resource
					itm.Properties.Item("BuildAction").Value = 3;
					if (String.IsNullOrEmpty((string)prop.Value) || !String.Equals((string)prop.Value, typeof(AnotherCustomTool).Name))
					{
						prop.Value = typeof(AnotherCustomTool).Name;
					}
					*/
				}
				catch (Exception ex)
				{
					LastException = ex;
					fs.Close();
					if (File.Exists(strFile))
						File.Delete(strFile);
				}
			}

        	// generate our summary content for our 'single' file
            var summaryData = GenerateSummaryContent();

            if (summaryData == null)
            {
                rgbOutputFileContents = IntPtr.Zero;

                pcbOutput = 0;
            }
            else
            {
                // return our summary data, so that Visual Studio may write it to disk.
                rgbOutputFileContents = Marshal.AllocCoTaskMem(summaryData.Length);

                Marshal.Copy(summaryData, 0, rgbOutputFileContents, summaryData.Length);

                pcbOutput = summaryData.Length;
            }
        }

        #region IObjectWithSite Members

        public void GetSite(ref Guid riid, out IntPtr ppvSite)
        {
            if (site == null)
            {
                throw new Win32Exception(-2147467259);
            }

            var objectPointer = Marshal.GetIUnknownForObject(site);

            try
            {
                Marshal.QueryInterface(objectPointer, ref riid, out ppvSite);
                if (ppvSite == IntPtr.Zero)
                {
                    throw new Win32Exception(-2147467262);
                }
            }
            finally
            {
                if (objectPointer != IntPtr.Zero)
                {
                    Marshal.Release(objectPointer);
                }
            }
        }

        public void SetSite(object pUnkSite)
        {
            site = pUnkSite;
        }

        #endregion

    }
}

