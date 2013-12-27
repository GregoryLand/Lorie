/**************************************************************
 * Program: Lorei
 * Class:
 * Description:
 *  This class handles all of the logic required for lorei to
 *  opperate.  The class is currently designed around a 
 *  Dictonary that is used to store and look up created
 *  processes based on a file path to the launched exe.
 *  This may or maynot be the best solution but it works well.
 *     drawbacks: 1) Can only have one instance of a program 
 *                open at a time. But since i havent created a 
 *                way to identify more then one program instance
 *                at a time this is a mute point.
 *  The class primarily focuses on the creation and handleing of 
 *  the grammer classes used to control diffrent programs.  The
 *  grammer classes are what alow the speech api to understand 
 *  english. This class also hosts the LUA scripting engine 
 *  used to read and setup the grammers required for a 
 *  specific program this provides a large ammout of flexability 
 *  and change to programs without the need to recompile lorei. 
 *  This class acts as a interface providing LUA with the 
 *  functions that can be used in the script files. 
 *  
 * 
 **************************************************************/
using System;
using System.Collections.Generic;
using System.Text;
using System.Speech.Recognition;
using System.Speech.Synthesis;
using System.Diagnostics;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using LuaInterface;

namespace Lorei
{
    class LoreiLanguageProcesser
    {
        /************ Constructors ************/
        public LoreiLanguageProcesser()
        {
            // Setup LUA functions
            SetupLuaFunctions();

            // Setup Variables
            SetupSpeechSynthesizer();

            // Setup Engine
            SetupSpeechRecognitionEngine();

            // Start Lorei
            LoreiStartListening();
        }

        /************ Destructors ************/
        
        /************ Methods ************/
        public void LoreiStartListening()
        {
            if (!m_Enabled)
            {
                m_speechRecognizer.RecognizeAsync(RecognizeMode.Multiple);
                if (StateChanged != null) StateChanged(this, true);
                m_Enabled = true;
            }
        }
        public void LoreiStopListening()
        {
            if (m_Enabled)
            {
                m_speechRecognizer.RecognizeAsyncCancel();
                if (StateChanged != null) StateChanged(this, false);
                m_Enabled = false;
            }
        }

        // Event Handlers
        private void m_speechRecognizer_SpeechRecognized(object sender, SpeechRecognizedEventArgs e)
        {
            // Interaction Message
            m_speechSynthesizer.SpeakAsync("Ok!");
            
            // Parse Speech
            ParseSpeech(e);
        }
        private void m_speechRecognizer_SpeechRecognitionRejected(object sender, SpeechRecognitionRejectedEventArgs e)
        {
            // If we knew any words
            if (e.Result.Words.Count > 0)
            {
                m_speechSynthesizer.SpeakAsync("What?");
            }
        }

        /************ Helper Methods ************/
        // Helper Methods For Lua Engine
        private void SetupLuaFunctions()
        {
            // Start up the lua engine
            m_luaEngine = new Lua();
   
            // Setup global functions
            m_luaEngine.RegisterFunction("SendMessage", this, this.GetType().GetMethod("DispatchMessageToWindow"));
            m_luaEngine.RegisterFunction("LaunchProgram", this, this.GetType().GetMethod("LaunchProgram"));
            RegisterFunctionTemplate("ExitProgram");
            RegisterFunctionTemplate("ExitStubbornProgram");
            RegisterFunctionTemplate("RegisterLoreiName");
            RegisterFunctionTemplate("RegisterLoreiFunction");
            RegisterFunctionTemplate("RegisterProgramWithScript");
            RegisterFunctionTemplate("RegisterLoreiProgramName");
            RegisterFunctionTemplate("RegisterLoreiProgramCommand");
            RegisterFunctionTemplate("SayMessage");
        }
        private void RegisterFunctionTemplate(string functionName)
        {
            m_luaEngine.RegisterFunction(functionName, this, this.GetType().GetMethod(functionName));
        }

        // Helper Methods For Speech Recognition Engine
        private void SetupSpeechRecognitionEngine()
        {
            // Setup Speech Engine
            m_speechRecognizer = new SpeechRecognitionEngine();

            if (m_speechRecognizer == null)
            {
                m_speechSynthesizer.SpeakAsync("Speech Recognizer Creation Failed is Null");
            }
            else m_speechSynthesizer.SpeakAsync("Speech Recognizer Created");

            // Bind to default audio device
            m_speechRecognizer.SetInputToDefaultAudioDevice();

            // Call Lua Junk
            m_luaEngine.DoFile("Scripts/setup.lua");

            // Setup Grammars
            LoadSpeechInformation();
            
            // Setup Event Handlers
            SetupEventHandlers();
        }
        private void LoadSpeechInformation()
        {
            // Data
            Choices keywords;
            Choices functions;
            Choices programs;
            Choices actions;
            Choices programActions;

            // Setup Grammars
            m_FunctionExecution  = new GrammarBuilder();
            m_ProgramControl     = new GrammarBuilder();

            // Setup Keywords
            keywords  = new Choices(m_Keywords.ToArray());
            // Setup Function List
            functions = new Choices(m_Functions.ToArray());
            // Setup List of Programs;
            programs  = new Choices(m_Programs.ToArray()); 
            // Program Functions
            programActions = new Choices(m_ProgramActions.ToArray());

            // Setup Grammar
            m_FunctionExecution.Append(keywords);
            m_FunctionExecution.Append(functions);
            m_FunctionExecution.Append(programs);
            m_ProgramControl.Append(keywords);
            m_ProgramControl.Append(programs);
            m_ProgramControl.Append(programActions);
            
            // Setup Engine
            m_FunctionGrammar = new Grammar(m_FunctionExecution);
            m_ProgramGrammar  = new Grammar(m_ProgramControl);
            m_FunctionGrammar.Name = "m_FunctionGrammar";
            m_ProgramGrammar.Name  = "m_ProgramGrammar";

            m_speechRecognizer.LoadGrammar(m_FunctionGrammar);
            m_speechRecognizer.LoadGrammar(m_ProgramGrammar);
        }
        private void SetupEventHandlers()
        {
            m_speechRecognizer.SpeechRecognized += new EventHandler<SpeechRecognizedEventArgs>(m_speechRecognizer_SpeechRecognized);
            m_speechRecognizer.SpeechRecognitionRejected += new EventHandler<SpeechRecognitionRejectedEventArgs>(m_speechRecognizer_SpeechRecognitionRejected);
        }

        // Helper Methods For Speech Synthesis Engine
        private void SetupSpeechSynthesizer()
        {
            m_speechSynthesizer = new SpeechSynthesizer();
        }

        // Helper Methods For Parsing Speech and Lua accessable macros
        private void ParseSpeech(SpeechRecognizedEventArgs e)
        {
            // Check if disabled by voice
            // HACK::::::HACK::::::HACK
            if (m_disabledByVoice == true)
            {
                if (e.Result.Words[1].Text != "Enable") return;
                if (e.Result.Words[2].Text != "Speech") return;
                m_disabledByVoice = false;
                return;
            }
            // ENDHACK:::::ENDHACK::::ENDHACK

            // Let the world know we parsed a command
            m_lastCommand = e.Result.Text;
            this.TextReceived(this, e);

            // This function calles the approprate function in the lua file so that 
            // programs can be controled from the lua script and lorei needs to know
            // nothing about the code that is ran there.
            // This creates a nice clean seperation between dispatch and
            // operation. Lorei is the dispatcher and LUA is the actual thing doing
            // all of the work. Most of the time the scripts just call back to lorei
            // functions to do all of the actual "Work" but it still prevents a lot of
            // boiler plate code.  This replaces alot of stupid code. Look below to see
            // what i replaced.  Every program would have had to be hard coded into lorei
            // and this system works much much better.
            
            try {
                switch (e.Result.Grammar.Name)
                {
                    case "m_FunctionGrammar":
                        string s = e.Result.Words[2].Text + ".To" + e.Result.Words[1].Text + "()";
                        m_luaEngine.DoString(s);
                        break;
                    case "m_ProgramGrammar":

                        // Rebuild the string from words to pass to the Lua engine.
                        string textToPass = "";
                        for (int x = 2; x < e.Result.Words.Count; x++)
                        {
                            if (x > 2) textToPass += " ";
                            textToPass += e.Result.Words[x].Text;
                        }

                        // If this throws make sure your scripts are correct.  capitalizion must match on RegisterLoreiProgramName and the Lua object itself
                        m_luaEngine.GetFunction(e.Result.Words[1].Text + ".OnSpeechReceved").Call(textToPass);//e.Result.Words[2].Text);
                        break;
                    default:
                        break;
                }
            }
            catch (Exception exception)
            {
                Console.WriteLine(exception.StackTrace);
            }
        }
        public void LaunchProgram(String p_program)
        {
            if (!m_runningPrograms.ContainsKey(p_program) )
            {
                // If the program isnt running start it
                // Add the program to the dictonary and continue as normal
                try
                {
                    m_runningPrograms.Add(p_program, Process.Start(p_program));
                }
                catch (Exception exception)
                {
                    Console.WriteLine(exception.StackTrace);
                    Console.WriteLine(p_program);
                    m_speechSynthesizer.SpeakAsync("I cannot find the specified file");
                }
            }
            else
            {
                if(m_runningPrograms[p_program].HasExited)
                {
                    // Program has exited and can be restarted
                    m_runningPrograms[p_program].Start();
                }
                else
                {
                    // Program is still running and shouldnt me messed with
                    m_speechSynthesizer.SpeakAsync("Program Is Already Running");
                }
            }
         
        }
        public void ExitProgram(String p_program)
        {
            // Check and see if the process exists
            Process procToKill;
            if ( m_runningPrograms.TryGetValue(p_program, out procToKill) )
            {
                // Check to make sure process is still alive.
                procToKill.Refresh();

                if (!procToKill.HasExited)
                {
                    // Close the main window so the program exits
                    procToKill.CloseMainWindow();
                }
                // Then remove the process from the process map so we can
                // launch the program again later.
                m_runningPrograms.Remove(p_program);
            }
            // work done go home 
        }
        public void ExitStubbornProgram(String p_program)
        {
            Process procToKill;
            if (m_runningPrograms.TryGetValue(p_program, out procToKill))
            {
                // Check to make sure process is still alive.
                procToKill.Refresh();

                if (!procToKill.HasExited)
                {
                    // Close the main window so the program exits
                    procToKill.Kill();
                }
                // Then remove the process from the process map so we can
                // launch the program again later.
                m_runningPrograms.Remove(p_program);
            }
        }
        public void DispatchMessageToWindow(String p_program, int p_myMessage, int p_myWParam, int p_myLParam)
        {
            // This is cool
            Process myProcess;
            int     myMessage = p_myMessage;
            int     myWParam  = p_myWParam;
            int     myLParam  = p_myLParam;

            // Check and make shure the program exists because trying with a bad handle would be bad.....
            if (m_runningPrograms.TryGetValue(p_program, out myProcess))
            {
               // With out this call to refresh the process never updates the information 
               // about windows handles so when a program creates its main window it never changes
               // the information in this class.  Evil Evil Evil thing....
                myProcess.Refresh();  // This command took me days to find gota love msdn docs
                    
               // Import the win32 send message function so we can drop messages into the program
               LoreiLanguageProcesser.PostMessage(myProcess.MainWindowHandle, myMessage, myWParam, myLParam);
            }
            return;
        }

        // Lua General Script Methods
        public void SayMessage(string p_Message)
        {
            // This makes the speach engine for the program say things
            // this function is made accessable to lua so scripts can say stuff
            m_speechSynthesizer.SpeakAsync(p_Message);
        }

        // Lua Helper Methods for Registration
        public void RegisterProgramWithScript(string p_ProgramName) 
        {
            if (m_RegistrationComplete) return;

            m_luaEngine.DoFile("Scripts/" + p_ProgramName + ".lua");
        }
        private void RegisterTemplate(string p_String, List<string> p_list)
        {
            // This function is a template to create other functions because
            // im lazy and copy paste is a bad idea so i make metafunctions...
            if (m_RegistrationComplete) return;

            // Check each element in list to see if new item exists already
            foreach (string x in p_list)
            {
                if (x == p_String) return;
            }

            p_list.Add(p_String);
        }
        public void RegisterLoreiName(string p_NameForLorei) 
        {
            // Do the work.... shame to need a function for this
            RegisterTemplate(p_NameForLorei, m_Keywords);
        }
        public void RegisterLoreiFunction(string p_NameOfFunction) 
        {
            RegisterTemplate(p_NameOfFunction, m_Functions);
        }
        public void RegisterLoreiProgramName(string p_NameOfProgram) 
        {
            RegisterTemplate(p_NameOfProgram, m_Programs);
        }
        public void RegisterLoreiProgramCommand(string p_NameOfProgramCommand) 
        {
            RegisterTemplate(p_NameOfProgramCommand, m_ProgramActions);
        }
        public void RegistrationDone() 
        {
            // Check to see if i should be called
            if (m_RegistrationComplete) return;

            // Set flag so we disable all register functions
            m_RegistrationComplete = true;
        }

        // Imported Stuff
        //[DllImport("user32.dll", CallingConvention = CallingConvention.Winapi, CharSet = CharSet.Auto)]
        //private static extern IntPtr SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);
        [DllImport("user32.dll", CallingConvention = CallingConvention.Winapi, CharSet = CharSet.Auto)]
        private static extern IntPtr PostMessage(IntPtr hWnd, int msg, int wParam, int lParam);

        /************ Constants ************/

        /************ Events ************/
        public event ParseSpeech TextReceived;
        public event ProcesserSwitchChanged StateChanged;

        /************ Accessers ************/
        public bool Active 
        {
            set
            {
                if (value == true) LoreiStartListening();
                else LoreiStopListening();
            }
            get
            {
                return m_Enabled;
            }
        }
        public string LastCommand
        {
            get
            {
                return m_lastCommand;
            }
            
        }

        /************ Data ************/
        private List<String> m_Keywords       = new List<string>();
        private List<String> m_Functions      = new List<string>();
        private List<String> m_Programs       = new List<string>();
        private List<String> m_Aliases        = new List<string>();
        private List<String> m_ProgramActions = new List<string>();
        
        // Speech Components
        //private SpeechRecognizer  m_speechRecognizer;
        private SpeechRecognitionEngine m_speechRecognizer;
        private SpeechSynthesizer m_speechSynthesizer;
        private GrammarBuilder    m_FunctionExecution;
        private GrammarBuilder    m_ProgramControl;
        private Grammar m_FunctionGrammar;
        private Grammar m_ProgramGrammar;

        // Program Control Data
        private Dictionary<String, Process> m_runningPrograms = new Dictionary<string,Process>();
        private bool m_disabledByVoice = false;
        private bool m_Enabled = false;
        private string m_lastCommand;

        // Lua Engine Data
        Lua m_luaEngine;
        bool m_RegistrationComplete = false;
    }
}
