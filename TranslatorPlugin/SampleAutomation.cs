/////////////////////////////////////////////////////////////////////
// Copyright (c) Autodesk, Inc. All rights reserved
// Written by Forge Partner Development
//
// Permission to use, copy, modify, and distribute this software in
// object code form for any purpose and without fee is hereby granted,
// provided that the above copyright notice appears in all copies and
// that both that copyright notice and the limited warranty and
// restricted rights notice below appear in all supporting
// documentation.
//
// AUTODESK PROVIDES THIS PROGRAM "AS IS" AND WITH ALL FAULTS.
// AUTODESK SPECIFICALLY DISCLAIMS ANY IMPLIED WARRANTY OF
// MERCHANTABILITY OR FITNESS FOR A PARTICULAR USE.  AUTODESK, INC.
// DOES NOT WARRANT THAT THE OPERATION OF THE PROGRAM WILL BE
// UNINTERRUPTED OR ERROR FREE.
/////////////////////////////////////////////////////////////////////

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using Newtonsoft.Json;
using Inventor;
using Autodesk.Forge.DesignAutomation.Inventor.Utils;

namespace TranslatorPlugin
{
    [ComVisible(true)]
    public class SampleAutomation
    {
        private InventorServer m_server;
        public SampleAutomation(InventorServer app) { m_server = app; }

        public void Run(Document doc)
        {
            try
            {                 
                var docDir = System.IO.Directory.GetCurrentDirectory();
                LogTrace(doc.DocumentType.ToString());
                // save output file
                var documentType = doc.DocumentType;
                if (documentType == DocumentTypeEnum.kPartDocumentObject)
                {
                    // the name must be in sync with OutputIpt localName in Activity
                    var fileName = System.IO.Path.Combine(docDir, "outputFile.ipt");

                    // save file                                                                
                    doc.SaveAs(fileName, false);
                }
                else if (documentType == DocumentTypeEnum.kAssemblyDocumentObject)
                {
                     
                    // the name must be in sync with OutputIpt localName in Activity
                    var fileName = System.IO.Path.Combine(docDir, "inputFile\\" + doc.DisplayName);

                    // save file                                                                
                    doc.SaveAs(fileName, false);
                    m_server.SaveOptions.TranslatorReportLocation = ReportLocationEnum.kNoReport;
                    
                    //int cnt = doc.File.AllReferencedFiles.Count;
                    LogTrace("List of reference files ");
                    foreach (Inventor.File f in doc.File.AllReferencedFiles)
                    {
                        var refrencefile = m_server.Documents.Open(f.FullFileName, false);
                        refrencefile.SaveAs(System.IO.Path.Combine(docDir, "inputFile" , System.IO.Path.GetFileName(f.FullFileName)),true);
                                               
                        LogTrace(f.FullFileName);
                    }
                    
                    
                }
            }
            catch (Exception e) { LogTrace("Processing failed: {0}", e.ToString()); }
        }          

        /// <summary>
        /// This will appear on the Design Automation output
        /// </summary>
        private static void LogTrace(string format, params object[] args) { Trace.TraceInformation(format, args); }
    }

     
    
}