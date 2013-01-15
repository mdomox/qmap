﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data.SqlClient;
using Microsoft.Synchronization;
using Microsoft.Synchronization.Data;

namespace ConsoleApplication1
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine("Wrong number of args");
                printUsage();
                return;
            }

            SqlConnection server = new SqlConnection();
            SqlConnection client = new SqlConnection();
            string serverconn = "";
            string clientconn = "";
            bool deprovison = false;
            bool reprovision = false;
            string tablename = "";
            string direction = "Download";

            bool hasserver = args.Any(x => x.Contains("--server"));
            if (!hasserver)
            {
                Console.Error.WriteLine("We need a server connection string");
                printUsage();
                return;
            }

            // If there is no client arg given then we assume that we are talking
            // working on the server tables
            bool hastable = args.Any(x => x.Contains("--table"));
            if (!hastable)
            {
                Console.Error.WriteLine("We need a table to work on");
                printUsage();
                return;
            }

            foreach (var arg in args)
            {
                var pairs = arg.Split(new char[] { '=' }, 2,
                                      StringSplitOptions.None);
                var name = pairs[0];
                string parm = "";
                if (pairs.Length == 2)
                    parm = pairs[1];
                switch (name)
                {
                    case "--server":
                        serverconn = parm;
                        server.ConnectionString = parm;
                        break;
                    case "--client":
                        clientconn = parm;
                        client.ConnectionString = parm;
                        break;
                    case "--table":
                        tablename = parm;
                        break;
                    case "--direction":
                        direction = parm;
                        break;
                    case "--deprovision":
                        deprovison = true;
                        break;
                    case "--reprovision":
                        reprovision = true;
                        break;
                    default:
                        break;
                }
            }

            // If there is no client arg given then we assume that we are
            // working on the server tables
            bool hasclient = args.Any(x => x.Contains("--client"));
            if (!hasclient)
            {
                client = server;
                Console.WriteLine("No client given. Client is now server connection");
            }

            Console.WriteLine("Running using these settings");
            Console.WriteLine(" Server:" + server.ConnectionString);
            Console.WriteLine(" Client:" + client.ConnectionString);
            Console.WriteLine(" Table:" + tablename);
            Console.WriteLine(" Direction:" + direction);
            string mode = (deprovison ? "Deprovison" : reprovision ? "Reprovision" : "Provision");
            Console.WriteLine(" Mode:" + mode);

            if (reprovision)
            {
                Deprovison(server, client, tablename);
                Provision(server, client, tablename, direction);
            }
            else if (deprovison)
            {
                Deprovison(server, client, tablename);
            }
            else
            {
                Provision(server, client, tablename, direction);
            }
        }

        private static void Deprovison(SqlConnection server, SqlConnection client, string tablename)
        {
            if (client.ConnectionString != server.ConnectionString)
            {
                Console.WriteLine(client.ConnectionString != server.ConnectionString);
                client.Open();
                Console.WriteLine("Droping table...");
                Deprovisioning.DropTable(client, tablename);
                client.Close();
            }
            bool pass = Deprovisioning.DeprovisonScope(client, tablename);
            if (!pass)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Error.WriteLine("Deprovision failed for " + tablename);
                Console.ResetColor();
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("Deprovision complete");
                Console.ResetColor();
            }
        }

        private static void Provision(SqlConnection server, SqlConnection client,
                                      string tablename, string direction)
        {
            try
            {
                Provisioning.ProvisionTable(server, client, tablename);
            }
            catch (SyncConstraintConflictNotAllowedException)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                string message = string.Format("Scope called {0} already exists. Please use --deprovision first", tablename);
                Console.Error.WriteLine(message);
                Console.ResetColor();
            }
            Console.WriteLine("Provision complete");
            if (server.ConnectionString != client.ConnectionString)
            {
                if (client.State == System.Data.ConnectionState.Closed)
                    client.Open();
                Console.WriteLine("Adding to scopes table on client");
                Provisioning.AddScopeToScopesTable(client, tablename,
                                                   utils.StringToEnum<SyncDirectionOrder>(direction));
            }
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("Complete");
            Console.ResetColor();
        }

        static void printUsage()
        {
            Console.WriteLine(@"provisioner --server={connectionstring} --table={tablename} --srid={SRID} [options]
[options]

--client={connectionstring} : The connection string to the client database. 
                              If blank will be set to server connection.
--direction=UploadAndDownload|DownloadAndUpload|Upload|Download : The direction that the table will sync.
                            if blank will be set to OneWay.
--deprovision : Deprovision the table rather then provision. WARNING: Will drop
                the table on the client if client and server are different! Never
                drops server tables.");
        }
    }
}
