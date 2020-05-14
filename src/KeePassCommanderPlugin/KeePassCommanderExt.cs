﻿using KeePass.Plugins;
using KeePassLib;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;

namespace KeePassCommander
{
    public sealed class KeePassCommanderExt : Plugin
    {
        private IPluginHost KeePassHost = null;
        private const string BeginOfResponse = "\t\t\t[--- begin of response ---]\t\t\t";
        private const string EndOfResponse = "\t\t\t[--- end of response ---]\t\t\t";

        private string ServerPipeName;

        public override bool Initialize(IPluginHost host)
        {
            Terminate();

            ServerPipeName = "KeePassCommander." + Process.GetCurrentProcess().SessionId;
            KeePassHost = host;
            StartServer();
            return true;
        }

        public override void Terminate()
        {
            if (KeePassHost == null) return;

            StopServer();
            KeePassHost = null;
        }

        private void StartServer()
        {
            Thread ServerThread = new Thread(ThreadStartServer);
            ServerThread.Start(this);
        }

        private void StopServer()
        {
            Stop = true;

            lock (ServerLock)
            {
                try
                {
                    using (NamedPipeClientStream npcs = new NamedPipeClientStream(".", ServerPipeName, PipeDirection.InOut, PipeOptions.None))
                    {
                        npcs.Connect(100);
                    }
                }
                catch { }

                foreach (ServerClient parm in ServerClients)
                {
                    try
                    {
                        parm.Pipe.Close();
                    }
                    catch { }
                }
            }
        }

        private object ServerLock = new object();
        private Boolean Stop = false;
        private NamedPipeServerStream ServerPipe = null;
        private List<ServerClient> ServerClients = new List<ServerClient>();

        private static void ThreadStartServer(object data)
        {
            KeePassCommanderExt me = data as KeePassCommanderExt;
            if (me != null)
                try
                {
                    me.RunServer();
                }
                catch { }
        }

        private void RunServer()
        {
            if (Stop) return;
            lock (ServerLock)
            {
                if (Stop) return;

                ServerPipe = new NamedPipeServerStream(ServerPipeName,
                    PipeDirection.InOut,
                    10,
                    PipeTransmissionMode.Byte,
                    PipeOptions.None);
            }

            try
            {
                ServerPipe.WaitForConnection();

                if (Stop)
                {
                    ServerPipe.Dispose();
                    ServerPipe = null;
                    return;
                }
                lock (ServerLock)
                {
                    if (Stop)
                    {
                        ServerPipe.Dispose();
                        ServerPipe = null;
                        return;
                    }

                    ServerClient client = new ServerClient(ServerPipe);
                    ServerClients.Add(client);

                    ServerPipe = null;
                    StartServer();
                    RunClient(client);
                }
            }
            catch { }
        }

        private class ServerClient
        {
            public NamedPipeServerStream Pipe { get; private set; }

            public ServerClient(NamedPipeServerStream Pipe)
            {
                this.Pipe = Pipe;
            }
        }

        private void RunClient(ServerClient client)
        {
            try
            {
                StreamReader reader = new StreamReader(client.Pipe, Encoding.UTF8);
                StreamWriter writer = new StreamWriter(client.Pipe, Encoding.UTF8);

                string output = String.Empty;
                string command = reader.ReadLine();
                string[] parms = command.Split('\t');
                if (parms.Length > 0)
                {
                    if (parms[0] == "get")
                        output = CommandGet(parms);
                    else if (parms[0] == "getfield")
                        output = CommandGetField(parms);
                    else if (parms[0] == "getattachment")
                        output = CommandGetAttachment(parms);
                    else if (parms[0] == "getnote")
                        output = CommandGetNote(parms);
                }

                writer.WriteLine(output);
                writer.WriteLine(EndOfResponse);
                writer.Flush();

                client.Pipe.Flush();
                client.Pipe.Close();
                client.Pipe.Dispose();
            }
            catch { }

            if (Stop) return;
            lock (ServerLock)
            {
                if (Stop) return;

                ServerClients.Remove(client);
            }
        }

        private string GetPasswordEntryField(PwEntry entry, string FieldName)
        {
            const string StrRefStart = @"{REF:";
            const string StrRefEnd = @"}";
            const StringComparison ScMethod = StringComparison.OrdinalIgnoreCase;

            string input = entry.Strings.ReadSafe(FieldName);

            //replace field references
            //See also KeePass source SprEngine.cs function FillRefPlaceholders
            if (input.IndexOf(StrRefStart, 0, ScMethod) < 0)
            {
                //nothing to do
                return input;
            }

            KeePass.Util.Spr.SprContext ctx = new KeePass.Util.Spr.SprContext(entry,
                KeePassHost.MainWindow.DocumentManager.SafeFindContainerOf(entry),
                KeePass.Util.Spr.SprCompileFlags.Deref);


            int nOffset = 0;
            while (true)
            {
                int nStart = input.IndexOf(StrRefStart, nOffset, ScMethod);
                if (nStart < 0) break;
                int nEnd = input.IndexOf(StrRefEnd, nStart + 1, ScMethod);
                if (nEnd <= nStart) break;

                string strFullRef = input.Substring(nStart, nEnd - nStart + 1);
                char chScan, chWanted;
                PwEntry peFound = KeePass.Util.Spr.SprEngine.FindRefTarget(strFullRef, ctx, out chScan, out chWanted);

                if (peFound != null)
                {
                    string strInsData;
                    if (chWanted == 'T')
                        strInsData = peFound.Strings.ReadSafe(PwDefs.TitleField);
                    else if (chWanted == 'U')
                        strInsData = peFound.Strings.ReadSafe(PwDefs.UserNameField);
                    else if (chWanted == 'A')
                        strInsData = peFound.Strings.ReadSafe(PwDefs.UrlField);
                    else if (chWanted == 'P')
                        strInsData = peFound.Strings.ReadSafe(PwDefs.PasswordField);
                    else if (chWanted == 'N')
                        strInsData = peFound.Strings.ReadSafe(PwDefs.NotesField);
                    else if (chWanted == 'I')
                        strInsData = peFound.Uuid.ToHexString();
                    else { nOffset = nStart + 1; continue; }

                    input = input.Substring(0, nStart) + strInsData + input.Substring(nEnd + StrRefEnd.Length);
                }
                else { nOffset = nStart + 1; continue; }
            }

            return input;
        }

        private void FindTitles(Dictionary<string, List<PwEntry>> search)
        {
            if (search.Count == 0) return;

            foreach (var doc in KeePassHost.MainWindow.DocumentManager.Documents)
            {
                PwDatabase db = doc.Database;

                if (db.IsOpen)
                {
                    var items = db.RootGroup.GetObjects(true, true);
                    foreach (var item in items)
                    {
                        if (item is PwEntry)
                        {
                            PwEntry entry = item as PwEntry;

                            string title = GetPasswordEntryField(entry, PwDefs.TitleField);
                            if (search.ContainsKey(title))
                            {
                                search[title].Add(entry);
                            }
                        }
                    }
                }
            }
        }

        private string CommandGet(string[] parms)
        {
            StringBuilder result = new StringBuilder();
            result.AppendLine(BeginOfResponse + "[get][default-1-column]");

            Dictionary<string, List<PwEntry>> titles = new Dictionary<string, List<PwEntry>>();
            {
                for (int i = 1; i < parms.Length; i++)
                {
                    string name = parms[i].Trim();
                    if (name.Length > 0)
                    {
                        titles.Add(name, new List<PwEntry>());
                    }
                }
                FindTitles(titles);
            }

            foreach (var keypair in titles)
            {
                foreach (PwEntry entry in keypair.Value)
                {
                    try
                    {
                        string url = GetPasswordEntryField(entry, PwDefs.UrlField);
                        string urlscheme = String.Empty;
                        string urlhost = String.Empty;
                        string urlport = String.Empty;
                        string urlpath = String.Empty;

                        try
                        {
                            Uri uri = new Uri(url);
                            urlscheme = uri.Scheme;
                            urlhost = uri.Host;
                            if (uri.Port != -1) urlport = uri.Port.ToString();
                            urlpath = uri.AbsolutePath;
                        }
                        catch { }

                        result.AppendLine(GetPasswordEntryField(entry, PwDefs.TitleField) + "\t" +
                                          GetPasswordEntryField(entry, PwDefs.UserNameField) + "\t" +
                                          GetPasswordEntryField(entry, PwDefs.PasswordField) + "\t" +
                                          url + "\t" +
                                          urlscheme + "\t" +
                                          urlhost + "\t" +
                                          urlport + "\t" +
                                          urlpath + "\t" +
                                          Convert.ToBase64String(Encoding.UTF8.GetBytes(GetPasswordEntryField(entry, PwDefs.NotesField))) + "\t");
                    }
                    catch { }
                }
            }

            return result.ToString();
        }

        private string CommandGetField(string[] parms)
        {
            StringBuilder result = new StringBuilder();
            result.AppendLine(BeginOfResponse + "[getfield][default-2-column]");

            Dictionary<string, List<PwEntry>> titles = new Dictionary<string, List<PwEntry>>();
            {
                string name = (parms.Length >= 2 ? parms[1].Trim() : String.Empty);
                if (!string.IsNullOrEmpty(name))
                {
                    titles.Add(name, new List<PwEntry>());
                }
                FindTitles(titles);
            }

            List<string> fieldnames = new List<string>();
            {
                for (int i = 2; i < parms.Length; i++)
                {
                    string name = parms[i].Trim();
                    if (name.Length > 0)
                    {
                        fieldnames.Add(name);
                    }
                }
            }

            foreach (var keypair in titles)
            {
                foreach (PwEntry entry in keypair.Value)
                {
                    result.Append(PwDefs.TitleField);
                    result.Append("\t");
                    result.Append(GetPasswordEntryField(entry, PwDefs.TitleField));
                    result.Append("\t");

                    foreach (string fieldname in fieldnames)
                    {
                        try
                        {
                            string value = GetPasswordEntryField(entry, fieldname);

                            result.Append(fieldname);
                            result.Append("\t");
                            result.Append(value);
                            result.Append("\t");
                        }
                        catch { }
                    }

                    result.AppendLine();
                }
            }

            return result.ToString();
        }

        private string CommandGetAttachment(string[] parms)
        {
            StringBuilder result = new StringBuilder();
            result.AppendLine(BeginOfResponse + "[getattachment][default-2-column]");

            Dictionary<string, List<PwEntry>> titles = new Dictionary<string, List<PwEntry>>();
            {
                string name = (parms.Length >= 2 ? parms[1].Trim() : String.Empty);
                if (!string.IsNullOrEmpty(name))
                {
                    titles.Add(name, new List<PwEntry>());
                }
                FindTitles(titles);
            }

            List<string> attachmentnames = new List<string>();
            {
                for (int i = 2; i < parms.Length; i++)
                {
                    string name = parms[i].Trim();
                    if (name.Length > 0)
                    {
                        attachmentnames.Add(name);
                    }
                }
            }

            foreach (var keypair in titles)
            {
                foreach (PwEntry entry in keypair.Value)
                {
                    result.Append(PwDefs.TitleField);
                    result.Append("\t");
                    result.Append(GetPasswordEntryField(entry, PwDefs.TitleField));
                    result.Append("\t");

                    foreach (string attachmentname in attachmentnames)
                    {
                        try
                        {
                            byte[] value = entry.Binaries.Get(attachmentname).ReadData();

                            result.Append(attachmentname);
                            result.Append("\t");
                            result.Append(Convert.ToBase64String(value));
                            result.Append("\t");
                        }
                        catch { }
                    }

                    result.AppendLine();
                }
            }

            return result.ToString();
        }

        private string CommandGetNote(string[] parms)
        {
            StringBuilder result = new StringBuilder();
            result.AppendLine(BeginOfResponse + "[getnote][default-1-column]");

            Dictionary<string, List<PwEntry>> titles = new Dictionary<string, List<PwEntry>>();
            {
                string name = (parms.Length >= 2 ? parms[1].Trim() : String.Empty);
                if (!string.IsNullOrEmpty(name))
                {
                    titles.Add(name, new List<PwEntry>());
                }
                FindTitles(titles);
            }

            foreach (var keypair in titles)
            {
                foreach (PwEntry entry in keypair.Value)
                {
                    result.Append(GetPasswordEntryField(entry, PwDefs.TitleField));
                    result.Append("\t");
                    try
                    {
                        result.Append(Convert.ToBase64String(Encoding.UTF8.GetBytes(GetPasswordEntryField(entry, PwDefs.NotesField))));
                        result.Append("\t");
                    }
                    catch { }
                    result.AppendLine();
                }
            }

            return result.ToString();
        }
    }
}
