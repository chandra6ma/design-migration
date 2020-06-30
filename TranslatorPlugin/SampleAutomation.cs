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
                //string strCLSID = "{93D506C4-8355-4E28-9C4E-C2B5F1EDC6AE}";
                //string strFileName = doc.FullDocumentName;

                //ApplicationAddIns oAddIns = m_server.ApplicationAddIns;

                //// Find the NX translator, get the CLSID and activate it.
                //TranslatorAddIn oTransAddIn = (Inventor.TranslatorAddIn)oAddIns.ItemById[strCLSID];
                //oTransAddIn.Activate();

                ////Get the transient object and take it as a factory to produce other objects.
                //TransientObjects transientObj = m_server.TransientObjects; 

                //// Prepare the first parameter for Open(), the file name.
                //DataMedium file = transientObj.CreateDataMedium();
                //file.FileName = strFileName; 

                //// Prepare the second parameter for Open(), the open type.
                //TranslationContext context = (Inventor.TranslationContext)transientObj.CreateTranslationContext();
                //context.Type = IOMechanismEnum.kDataDropIOMechanism;


                //// Prepare the third parameter for Open(), the options.
                //NameValueMap options = transientObj.CreateNameValueMap();
                //options.Value["SaveComponentDuringLoad"] = false;
                //options.Value["SaveLocationIndex"] = 0;
                //options.Value["ComponentDestFolder"] = "";
                //options.Value["SaveAssemSeperateFolder"] = false;
                //options.Value["AssemDestFolder"] = "";
                //options.Value["ImportSolid"] = true ;
                //options.Value["ImportSurface"] = true ;
                //options.Value["ImportWire"] = true ;
                //options.Value["ImportWorkPlane"] = true ;
                //options.Value["ImportWorkAxe"] = true;
                //options.Value["ImportWorkPoint"] = true ;
                //options.Value["ImportPoint"] = true ;
                //options.Value["ImportAASP"] = false ;
                //options.Value["ImportAASPIndex"] = 0;
                //options.Value["CreateSurfIndex"] = 1;
                //options.Value["GroupNameIndex"] = 0;
                //options.Value["GroupName"] = "";
                //options.Value["ImportUnit"] = 0;
                //options.Value["CheckDuringLoad"] = false ;
                //options.Value["AutoStitchAndPromote"] = true ;
                //options.Value["AdvanceHealing"] = false ;


                //options.Value["CHKSearchFolder"] = true ;


                ////100 is the search folder maximum that you can specify.
                ////Assign separate search folder one by one.

                //string[]  searchFolder = new string[100];

                //options.Value["SearchFolder"] = searchFolder;


                ////Prepare the fourth parameter for Open(), the final document

                //Object sourceObj;

                ////Open the NX file.
                //oTransAddIn.Open(file, context, options, out sourceObj);
                //m_server.Documents.Open(doc.FullDocumentName);
                // generate outputs
                //var docDir = System.IO.Path.GetDirectoryName(doc.FullFileName);
                var docDir = System.IO.Directory.GetCurrentDirectory();
                LogTrace(doc.DocumentType.ToString());
                // save output file
                var documentType = doc.DocumentType;
                //if (documentType == DocumentTypeEnum.kPartDocumentObject)
                //{
                    // the name must be in sync with OutputIpt localName in Activity
                    var fileName = System.IO.Path.Combine(docDir, "outputFile.ipt");

                    // save file                                                                
                    doc.SaveAs(fileName, false);
                //}
            }
            catch (Exception e) { LogTrace("Processing failed: {0}", e.ToString()); }
        }          

        /// <summary>
        /// This will appear on the Design Automation output
        /// </summary>
        private static void LogTrace(string format, params object[] args) { Trace.TraceInformation(format, args); }
    }

     
    
}