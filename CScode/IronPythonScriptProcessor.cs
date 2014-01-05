﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using IronPython.Hosting;
using Microsoft.Scripting.Hosting;

namespace Lorei
{
    /* This class handles the IronPython scripting.  Iron Python has access to the entire 
     * .net framework as well as any api calls we decide to provide to it.
     * Currently it looks in the script folders for any python files and loads each 
     * python file into its own Script Scope.  The way this is setup means scripts 
     * will not interfere with each other.
     */
    class IronPythonScriptProcessor : ScriptProcessor
    {
        /************ Constructors ************/
        public IronPythonScriptProcessor(LoreiLanguageProcessor p_owner, ApiDictionary apiDictionary)
        {
            m_owner = p_owner;
            m_apiDictionary = apiDictionary;

            // Setup Assemblies
            // Could try catch here but without this mscorlib.dll we are screwed 6 ways till Sunday
            m_pythonEngine.LoadAssembly( System.Reflection.Assembly.Load("mscorlib.dll") );
            m_pythonEngine.LoadAssembly( typeof(System.Speech.Recognition.SpeechRecognizedEventArgs).Assembly );

            // Import required items
            //m_pythonEngine.ImportModule("System.Speech");

            // Setup python
            m_pythonEngine.Globals.SetVariable("LoreiApi", m_owner);  // This passes Lorei to the python world
            m_pythonEngine.Globals.SetVariable("ApiDictionary", m_apiDictionary); //This passes the rest of the api to python

            this.ExecuteEachScript();
        }

        /************ Methods ************/
        // Script Processor Interface
        public void ParseSpeech(System.Speech.Recognition.SpeechRecognizedEventArgs e)
        {
            // Run threw all scripts and call the parse speech function on each one.
            foreach (dynamic x in m_listOfScopes)
            {
                if (x.ContainsVariable("ParseSpeech"))
                {
                    // TODO: At some point we will have to go threw and see what exceptions can happen here
                    // for now just catch them and rethrow.
                    try
                    {
                        x.ParseSpeech(e);
                    }
                    catch (Exception oCrap)
                    {
                        throw oCrap;
                    }
                }
            }
        }

        /************ Helper Methods ************/
        public void ExecuteEachScript()
        {
            String[] scripts = System.IO.Directory.GetFiles("Scripts/");

            foreach (String script in scripts)
            {
                if (script.EndsWith(".py"))
                {
                    m_listOfScopes.Add( m_pythonEngine.ExecuteFile(script) );
                }
            }
        }

        /************ Data ************/
        // Python Scripting Info
        Microsoft.Scripting.Hosting.ScriptRuntime m_pythonEngine = Python.CreateRuntime();
        List<dynamic> m_listOfScopes = new List<dynamic>();

        // Language Processor Info
        public LoreiLanguageProcessor m_owner;
        public ApiDictionary m_apiDictionary;
    }
}
