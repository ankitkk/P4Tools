using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Perforce.P4;
using Perforce;
using CommandLine;
using System.Threading; 

namespace P4Sync
{
    // Define a class to receive parsed values
    class Options
    {

        [Option('s', "server", Required = true,
        HelpText = "Perforce server")]
        public string Server { get; set; }

        [Option('u', "user", Required = true,
        HelpText = "Perforce user")]
        public string User { get; set; }

        [Option('p', "passwd", Required = true,
        HelpText = "Perforce password")]
        public string Passwd { get; set; }

        [Option('c', "clientspec", Required = true,
        HelpText = "Peforce clientspec")]
        public string ClientSpec { get; set; }


        [Option('t', "threads", DefaultValue = 5,
        HelpText = "Peforce clientspec")]
        public int Threads { get; set; }

    }


    class P4Sync
    {
        Options options; 
        IList<FileSpec> FilesToSync;
        int ThreadId; 

        public P4Sync( Options InOptions, IList<FileSpec> InFilesToSync, int InThreadId) 
        {
            ThreadId = InThreadId;
            options = InOptions; 
            FilesToSync = new List<FileSpec>(InFilesToSync);
            Thread thread = new Thread(new ThreadStart(Sync));
            thread.Start(); 
        }

        public void Sync()
        {

            try
            {

                // new connection for every thread.
                Perforce.P4.Server server = new Perforce.P4.Server(new Perforce.P4.ServerAddress(options.Server));
                Perforce.P4.Repository rep = new Perforce.P4.Repository(server);
                Perforce.P4.Connection con = rep.Connection;


                con.UserName = options.User;
                con.Client = new Perforce.P4.Client();
                con.Client.Name = options.ClientSpec;
                con.Connect(null);
                Perforce.P4.Credential Creds = con.Login(options.Passwd, null, null);
                con.InfoResultsReceived += new P4Server.InfoResultsDelegate(con_InfoResultsReceived);
                con.ErrorReceived += new P4Server.ErrorDelegate(con_ErrorReceived);
                con.TextResultsReceived += new P4Server.TextResultsDelegate(con_TextResultsReceived);


                // get server metadata and check version
                // (using null for options parameter)
                ServerMetaData p4info = rep.GetServerMetaData();
                ServerVersion version = p4info.Version;
                string release = version.Major;

                // lets get all the files which need to be synced.  
                SyncFilesCmdOptions syncOpts = new SyncFilesCmdOptions(SyncFilesCmdFlags.None, -1);
                IList<FileSpec> syncedFiles = con.Client.SyncFiles(syncOpts, FilesToSync.ToArray());
            }
            catch (Exception ex)
            {
                System.Console.WriteLine("Perforce error - Thread exiting "); 
            }
        }

        void con_TextResultsReceived(string data)
        {
            System.Console.WriteLine("Info: " + data); 
        }

        void con_ErrorReceived(int severity, string data)
        {
            System.Console.WriteLine("Error: " + data); 
        }

        void con_InfoResultsReceived(int level, string data)
        {
            System.Console.WriteLine("Info:  " +data); 
        }
    }
   

    class Program
    {

        public static IEnumerable<IEnumerable<T>> Chunk<T>(IEnumerable<T> source, int chunksize)
        {
            while (source.Any())
            {
                yield return source.Take(chunksize);
                source = source.Skip(chunksize);
            }
        }

        static void Main(string[] args)
        {
            var options = new Options();
            if (!CommandLine.Parser.Default.ParseArguments(args, options))
            {
                System.Console.WriteLine("Wrong Parameters");
            }

            Perforce.P4.Server server = new Perforce.P4.Server(new Perforce.P4.ServerAddress(options.Server));
            Perforce.P4.Repository rep = new Perforce.P4.Repository(server);
            Perforce.P4.Connection con = rep.Connection;

            con.UserName = options.User;
            con.Client = new Perforce.P4.Client();
            con.Client.Name = options.ClientSpec;
            con.Connect(null);
            Perforce.P4.Credential Creds = con.Login(options.Passwd, null, null);
            con.InfoResultsReceived += new P4Server.InfoResultsDelegate(con_InfoResultsReceived);
            con.ErrorReceived += new P4Server.ErrorDelegate(con_ErrorReceived);
            con.TextResultsReceived += new P4Server.TextResultsDelegate(con_TextResultsReceived);

            // lets get all the files which need to be synced.  
            SyncFilesCmdOptions syncOpts = new SyncFilesCmdOptions(SyncFilesCmdFlags.Preview, -1);
            IList<FileSpec> syncedFiles = con.Client.SyncFiles(syncOpts, null);

            con.Disconnect();

            if (syncedFiles != null)
            {
                var ChunkedLists = Chunk(syncedFiles.AsEnumerable(), syncedFiles.Count() / options.Threads).ToList();
                List<P4Sync> Syncs = new List<P4Sync>();
                System.Console.WriteLine("We have {0} files to sync. Creating {1} Threads", syncedFiles.Count(), ChunkedLists.Count()); 
                for (int i = 0; i < ChunkedLists.Count(); i++)
                {
                    Syncs.Add(new P4Sync(options, ChunkedLists[i].ToList(), i));
                }
            }
        }

        static void con_TextResultsReceived(string data)
        {
            System.Console.WriteLine("Info: " + data);
        }

        static void con_ErrorReceived(int severity, string data)
        {
            System.Console.WriteLine("Error: " + data);
        }

        static void con_InfoResultsReceived(int level, string data)
        {
            System.Console.WriteLine("Info:  " + data);
        }
    }
}
