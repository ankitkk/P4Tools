using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Perforce.P4;
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


        [Option('t', "threads", DefaultValue = 3,
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

                // new connection for everythread.
                Perforce.P4.Server server = new Perforce.P4.Server(new Perforce.P4.ServerAddress(options.Server));
                Perforce.P4.Repository rep = new Perforce.P4.Repository(server);
                Perforce.P4.Connection con = rep.Connection;

                con.UserName = options.User;
                con.Client = new Perforce.P4.Client();
                con.Client.Name = options.ClientSpec;
                con.Connect(null);
                Perforce.P4.Credential Creds = con.Login(options.Passwd, null, null);

                // get server metadata and check version
                // (using null for options parameter)
                ServerMetaData p4info = rep.GetServerMetaData();
                ServerVersion version = p4info.Version;
                string release = version.Major;

                // lets get all the files which need to be synced.  
                SyncFilesCmdOptions syncOpts = new SyncFilesCmdOptions(SyncFilesCmdFlags.Force, -1);


                int done = 0;

                foreach (var file in FilesToSync)
                {
                    bool loop = true;
                    System.Console.WriteLine(" [ {0}/{1}  ] {2} on Thread {3} ", done, FilesToSync.Count() - done, file.ClientPath, ThreadId);
                    while (loop)
                    {
                        try
                        {
                            IList<FileSpec> syncedFiles = con.Client.SyncFiles(syncOpts, file);
                            loop = false;
                        }
                        catch (Exception ex)
                        {
                            System.Console.WriteLine("retrying " + file.DepotPath + ex.ToString());
                            loop = true;
                        }
                    }
                    done++;

                }

                con.Disconnect();
            }
            catch (Exception ex)
            {
                System.Console.WriteLine("Peforce error - Thread exiting "); 
            }
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

            // get server metadata and check version
            // (using null for options parameter)
            ServerMetaData p4info = rep.GetServerMetaData();
            ServerVersion version = p4info.Version;
            string release = version.Major;

            // lets get all the files which need to be synced.  
            SyncFilesCmdOptions syncOpts = new SyncFilesCmdOptions(SyncFilesCmdFlags.Preview, -1);
            IList<FileSpec> syncedFiles = con.Client.SyncFiles(syncOpts, null);

            con.Disconnect();

            if (syncedFiles != null)
            {
                var ChunkedLists = Chunk(syncedFiles.AsEnumerable(), syncedFiles.Count() / options.Threads).ToList();
                List<P4Sync> Syncs = new List<P4Sync>();

                for (int i = 0; i < options.Threads; i++)
                {
                    Syncs.Add(new P4Sync(options, ChunkedLists[i].ToList(), i));
                }
            }
        }
    }
}
